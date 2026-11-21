using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class LuaFloatStore
{
    private readonly Dictionary<string, float> _values = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SkillData>? _skillBacking;

    [MoonSharpHidden]
    public void BindSkillBacking(Dictionary<string, SkillData> skills) => _skillBacking = skills;

    public float? this[string key]
    {
        get
        {
            if (_skillBacking != null && _skillBacking.TryGetValue(key, out SkillData? skill))
                return skill.CurrentCooldown;
            return _values.TryGetValue(key, out float value) ? value : null;
        }
        set
        {
            if (_skillBacking != null && _skillBacking.TryGetValue(key, out SkillData? skill))
            {
                skill.CurrentCooldown = value ?? 0f;
                return;
            }

            if (value.HasValue)
                _values[key] = value.Value;
            else
                _values.Remove(key);
        }
    }

    [MoonSharpHidden]
    public bool TryGetValue(string key, out float value)
    {
        if (_skillBacking != null && _skillBacking.TryGetValue(key, out SkillData? skill))
        {
            value = skill.CurrentCooldown;
            return true;
        }
        return _values.TryGetValue(key, out value);
    }

    [MoonSharpHidden]
    public bool ContainsKey(string key) =>
        (_skillBacking != null && _skillBacking.ContainsKey(key)) || _values.ContainsKey(key);

    [MoonSharpHidden]
    public void Clear()
    {
        if (_skillBacking != null)
        {
            foreach (SkillData skill in _skillBacking.Values)
            {
                skill.CurrentCooldown = 0f;
            }
        }
        _values.Clear();
    }

    [MoonSharpHidden]
    public int Count => _skillBacking != null ? _skillBacking.Count + _values.Count : _values.Count;
}
