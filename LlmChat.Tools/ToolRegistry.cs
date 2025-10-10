namespace LlmChat.Tools;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _map;
    public ToolRegistry(IEnumerable<ITool> tools) =>
        _map = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    public ITool? Get(string name) => _map.TryGetValue(name, out var t) ? t : null;
    public IEnumerable<ITool> All() => _map.Values;
}
