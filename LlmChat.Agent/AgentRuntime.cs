using System.Runtime.CompilerServices;
using System.Text.Json;
using LlmChat.Abstractions;
using LlmChat.Agent.Planning;
using LlmChat.Memory;
using LlmChat.Tools;
using Microsoft.Extensions.Logging;

namespace LlmChat.Agent;

public sealed class AgentRuntime : IAgent
{
    private readonly IChatClient _llm;
    private readonly IIntentRouter _router;
    private readonly IMemoryStore _memory;
    private readonly IToolRegistry _tools;
    private readonly ILogger<AgentRuntime>? _logger;

    public AgentRuntime(IChatClient llm, IIntentRouter router, IMemoryStore memory, IToolRegistry tools, ILogger<AgentRuntime>? logger = null)
        => (_llm, _router, _memory, _tools, _logger) = (llm, router, memory, tools, logger);

    public async Task<AgentTurnResult> HandleAsync(AgentTurnRequest req, CancellationToken ct = default)
    {
        _logger?.LogDebug("Agent handling request: {Input}", req.UserInput);
        
        await _memory.AppendAsync(req.SessionId, "user", req.UserInput, ct);

        var recent = await _memory.GetRecentAsync(req.SessionId, 40, ct);
        var ctx = new AgentContext(req.SessionId, req.UserInput, recent, _tools);
        var plan = await _router.RouteAsync(ctx, ct);

        _logger?.LogDebug("Intent plan: RequiresTool={RequiresTool}, ToolName={ToolName}", plan.RequiresTool, plan.ToolName);

        var logs = new List<ToolCallLog>();
        object? toolData = null;

        if (plan.RequiresTool && plan.ToolName is not null)
        {
            var tool = _tools.Get(plan.ToolName);
            if (tool is not null)
            {
                var safeParams = plan.Parameters ?? new Dictionary<string, object>();
                _logger?.LogDebug("Executing tool: {ToolName}", tool.Name);
                _logger?.LogTrace("Tool parameters: {Parameters}", JsonSerializer.Serialize(safeParams));
                
                var res = await tool.InvokeAsync(req.SessionId, safeParams, ct);
                
                _logger?.LogDebug("Tool execution result: Ok={Ok}", res.Ok);
                _logger?.LogTrace("Tool data shape: {Shape}", res.Data is null ? "null" : res.Data.GetType().Name);
                
                logs.Add(new ToolCallLog(tool.Name, safeParams, res.Ok, res.Error));
                toolData = res.Ok ? res.Data : new { error = res.Error };
            }
        }

        // Build message list with brief system instructions
        var msgs = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant. Reply in natural language only. Be concise and accurate."),
        };

        // Include a compact transcript context (recent messages)
        foreach (var (role, content) in recent)
        {
            var chatRole = role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant :
                           role.Equals("system", StringComparison.OrdinalIgnoreCase) ? ChatRole.System : ChatRole.User;
            if (chatRole == ChatRole.System)
            {
                // Fold old system notes as a single system line to avoid conflicts
                msgs.Add(new ChatMessage(ChatRole.System, content));
            }
            else
            {
                msgs.Add(new ChatMessage(chatRole, content));
            }
        }

        // Current user input
        msgs.Add(new(ChatRole.User, req.UserInput));
        
        // Provide structured tool results (top-level JSON string) to the model
        if (toolData is not null)
        {
            var toolJson = SafeSerialize(toolData);
            var toolMessage = $"Tool result available as JSON. Use it to answer the user. JSON: {toolJson}";
            msgs.Add(new(ChatRole.System, toolMessage));
        }

        _logger?.LogTrace("Complete message chain for LLM: {Messages}", JsonSerializer.Serialize(msgs.Select(m => new { m.Role, Content = Truncate(m.Content, 200) })));

        var final = await _llm.CompleteAsync(new ChatRequest(msgs), ct);
        
        _logger?.LogDebug("LLM response (truncated): {Response}", Truncate(final.Text, 300));
        
        await _memory.AppendAsync(req.SessionId, "assistant", final.Text, ct);

        if (plan.ShouldWriteMemory && !string.IsNullOrWhiteSpace(plan.MemoryNote))
        {
            await _memory.AddFactAsync(req.SessionId, plan.MemoryNote!, ct);
        }

        return new AgentTurnResult(final.Text, logs);
    }

    public async IAsyncEnumerable<string> StreamAsync(AgentTurnRequest req, [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger?.LogDebug("Agent streaming request: {Input}", req.UserInput);
        
        await _memory.AppendAsync(req.SessionId, "user", req.UserInput, ct);

        var recent = await _memory.GetRecentAsync(req.SessionId, 40, ct);
        var ctx = new AgentContext(req.SessionId, req.UserInput, recent, _tools);
        var plan = await _router.RouteAsync(ctx, ct);

        _logger?.LogDebug("Intent plan: RequiresTool={RequiresTool}, ToolName={ToolName}", plan.RequiresTool, plan.ToolName);

        object? toolData = null;
        if (plan.RequiresTool && plan.ToolName is not null)
        {
            var tool = _tools.Get(plan.ToolName);
            if (tool is not null)
            {
                var safeParams = plan.Parameters ?? new Dictionary<string, object>();
                _logger?.LogDebug("Executing tool: {ToolName}", tool.Name);
                var res = await tool.InvokeAsync(req.SessionId, safeParams, ct);
                _logger?.LogDebug("Tool execution result: Ok={Ok}", res.Ok);
                toolData = res.Ok ? res.Data : new { error = res.Error };
            }
        }

        var msgs = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant. Reply in natural language only. Be concise and accurate."),
        };

        foreach (var (role, content) in recent)
        {
            var chatRole = role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant :
                           role.Equals("system", StringComparison.OrdinalIgnoreCase) ? ChatRole.System : ChatRole.User;
            if (chatRole == ChatRole.System)
                msgs.Add(new ChatMessage(ChatRole.System, content));
            else
                msgs.Add(new ChatMessage(chatRole, content));
        }

        msgs.Add(new(ChatRole.User, req.UserInput));
        
        if (toolData is not null)
        {
            var toolJson = SafeSerialize(toolData);
            var toolMessage = $"Tool result available as JSON. Use it to answer the user. JSON: {toolJson}";
            msgs.Add(new(ChatRole.System, toolMessage));
        }

        var buffer = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamAsync(new ChatRequest(msgs), ct))
        {
            buffer.Append(chunk);
            yield return chunk;
        }
        
        var finalResponse = buffer.ToString();
        _logger?.LogDebug("LLM final response (truncated): {Response}", Truncate(finalResponse, 300));
        
        await _memory.AppendAsync(req.SessionId, "assistant", finalResponse, ct);

        if (plan.ShouldWriteMemory && !string.IsNullOrWhiteSpace(plan.MemoryNote))
        {
            await _memory.AddFactAsync(req.SessionId, plan.MemoryNote!, ct);
        }
    }

    private static string SafeSerialize(object obj)
    {
        try { return JsonSerializer.Serialize(obj); }
        catch { return "{}"; }
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max] + "…");
}
