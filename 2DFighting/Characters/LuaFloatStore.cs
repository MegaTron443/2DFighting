using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class LuaFloatStore
{
    private readonly Dictionary<string, float> _values = new(StringComparer.OrdinalIgnoreCase);

    public float? this[string key]
    {
        get => _values.TryGetValue(key, out float value) ? value : null;
        set
        {
            if (value.HasValue)
                _values[key] = value.Value;
            else
                _values.Remove(key);
        }
    }

    [MoonSharpHidden]
    public bool TryGetValue(string key, out float value) => _values.TryGetValue(key, out value);

    [MoonSharpHidden]
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    [MoonSharpHidden]
    public void Clear() => _values.Clear();

    [MoonSharpHidden]
    public int Count => _values.Count;
}
