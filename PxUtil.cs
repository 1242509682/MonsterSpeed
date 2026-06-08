using Microsoft.Xna.Framework;

namespace MonsterSpeed;

public static class PxUtil
{
    /// <summary>将 "x,y" 格数字符串转为像素 Vector2</summary>
    public static Vector2 GetVector2(this string str, Vector2 def = default)
    {
        if (string.IsNullOrWhiteSpace(str) || str == "0,0") return def;
        try
        {
            var parts = str.Split(',');
            if (parts.Length != 2) return def;
            float x = float.Parse(parts[0].Trim());
            float y = float.Parse(parts[1].Trim());
            return new Vector2(x * 16f, y * 16f);
        }
        catch { return def; }
    }

    /// <summary>解析浮点范围 "min,max"</summary>
    public static (bool ok, float min, float max) ParseRng(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (false, 0, 0);
        var parts = s.Split(',');
        if (parts.Length != 2) return (false, 0, 0);
        if (float.TryParse(parts[0].Trim(), out float a) && float.TryParse(parts[1].Trim(), out float b))
            return (true, Math.Min(a, b), Math.Max(a, b));
        return (false, 0, 0);
    }

    /// <summary>解析整数范围 "min,max"</summary>
    public static (bool ok, int min, int max) ParseRngInt(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (false, 0, 0);
        var parts = s.Split(',');
        if (parts.Length != 2) return (false, 0, 0);
        if (int.TryParse(parts[0].Trim(), out int a) && int.TryParse(parts[1].Trim(), out int b))
            return (true, Math.Min(a, b), Math.Max(a, b));
        return (false, 0, 0);
    }
}