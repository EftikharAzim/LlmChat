using LlmChat.Abstractions;
using LlmChat.Tools;
using System.Text.Json;
namespace LlmChat.Agent.Planning;
public sealed class LlmIntentRouter : IIntentRouter
{
    private readonly IChatClient _llm;
    private readonly IToolRegistry _tools;

    public LlmIntentRouter(IChatClient llm, IToolRegistry tools) => (_llm, _tools) = (llm, tools);

    public async Task<IntentPlan> RouteAsync(AgentContext ctx, CancellationToken ct = default)
    {
        var toolSummaries = _tools.All().Select(t => new { t.Name, t.Description, t.InputSchemaJson });
        var sys = "You plan actions for an agent. Respond with STRICT JSON only.";
        var user = new
        {
            instruction = "Decide if a tool is needed; if yes, pick one and build parameters.",
            tools = toolSummaries,
            input = ctx.UserInput,
            schema = new
            {
                requiresTool = "bool",
                toolName = "string|null",
                parameters = "object|null",
                shouldWriteMemory = "bool",
                memoryNote = "string|null"
            }
        };

        var resp = await _llm.CompleteAsync(new ChatRequest(new[]
        {
            new ChatMessage(ChatRole.System, sys),
            new ChatMessage(ChatRole.User, JsonSerializer.Serialize(user))
        }), ct);

        var json = ExtractJson(resp.Text);
        var dto = JsonSerializer.Deserialize<PlanDto>(json) ?? new();

        bool valid = dto.requiresTool && !string.IsNullOrWhiteSpace(dto.toolName) && _tools.Get(dto.toolName!) is not null;
        return new IntentPlan(
            RequiresTool: valid,
            ToolName: valid ? dto.toolName : null,
            Parameters: dto.parameters ?? new Dictionary<string, object>(),
            ShouldWriteMemory: dto.shouldWriteMemory,
            MemoryNote: dto.memoryNote
        );
    }

    private static string ExtractJson(string s)
    {
        int i = s.IndexOf('{'); int j = s.LastIndexOf('}');
        return (i >= 0 && j >= i) ? s[i..(j + 1)] : "{}";
    }

    private sealed class PlanDto
    {
        public bool requiresTool { get; set; }
        public string? toolName { get; set; }
        public Dictionary<string, object>? parameters { get; set; }
        public bool shouldWriteMemory { get; set; }
        public string? memoryNote { get; set; }
    }
}
