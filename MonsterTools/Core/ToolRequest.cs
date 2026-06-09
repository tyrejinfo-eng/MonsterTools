namespace MonsterTools.Core;

public class ToolRequest
{
    public Dictionary<string, object?> Args { get; }

    public ToolRequest(Dictionary<string, object?> args)
    {
        Args = args;
    }

    public T? Get<T>(string key)
    {
        if (!Args.TryGetValue(key, out var value) || value is null)
            return default;

        if (value is T t)
            return t;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}