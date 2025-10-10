namespace LlmChat.Tools;

public interface IToolRegistry
{
    ITool? Get(string name);
    IEnumerable<ITool> All();
}
