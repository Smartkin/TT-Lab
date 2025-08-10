using System.Collections.Generic;

namespace Twinsanity.AgentLab;

internal class AgentLabMemory
{
    private readonly Dictionary<string, object> _memory = new();

    public void Add(string key, object value)
    {
        _memory.Add(key, value);
    }

    public void Set(string key, object value)
    {
        _memory[key] = value;
    }

    public T Get<T>(string key)
    {
        return (T)_memory[key];
    }
}