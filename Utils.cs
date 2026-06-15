using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TShockAPI;

namespace MonsterSpeed;

public class Utils
{
    #region 单色与随机色
    public static Color color => new(240, 250, 150); // 单色
    public static Color color2 => new(Main.rand.Next(180, 250), // 随机色
                                      Main.rand.Next(180, 250),
                                      Main.rand.Next(180, 250));
    #endregion

    #region 渐变色方法
    public static string Grad(string text, TSPlayer? plr = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // 检查是否包含颜色标签或物品图标标签
        if (text.Contains("[c/") || text.Contains("[i:"))
            return MixedText(text);
        else
            return ApplyGrad(text);
    }
    #endregion

    #region 混合文本（包含颜色标签、物品图标标签和普通文本）
    private static readonly Regex _tagRegex = new Regex(@"(\[c/([0-9a-fA-F]+):([^\]]+)\]|\[i(?:/s\d+)?:\d+\])", RegexOptions.Compiled);
    private static string MixedText(string text)
    {
        var res = new StringBuilder();
        // 匹配颜色标签 [c/颜色:文本] 或 物品图标标签 [i:物品ID] 或 [i/s数量:物品ID]
        var regex = new Regex(@"(\[c/([0-9a-fA-F]+):([^\]]+)\]|\[i(?:/s\d+)?:\d+\])");
        var matches = _tagRegex.Matches(text);
        if (matches.Count == 0) return ApplyGrad(text);
        int idx = 0;
        foreach (Match match in matches.Cast<Match>())
        {
            // 添加标签前的普通文本（应用渐变）
            if (match.Index > idx)
                res.Append(ApplyGrad(text.Substring(idx, match.Index - idx)));

            // 添加标签本身（保持不变）
            res.Append(match.Value);
            idx = match.Index + match.Length;
        }

        // 添加最后一个标签后的普通文本
        if (idx < text.Length) res.Append(ApplyGrad(text.Substring(idx)));
        return res.ToString();
    }
    #endregion

    #region 应用文本渐变方法
    private static string ApplyGrad(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var res = new StringBuilder();
        var start = new Color(166, 213, 234); // 起始色：浅蓝
        var end = new Color(245, 247, 175);   // 结束色：浅黄

        int cnt = text.Count(c => c != '\n' && c != '\r'); // 有效字符数
        if (cnt == 0) return text;

        int idx = 0;
        foreach (char c in text)
        {
            if (c == '\n' || c == '\r') { res.Append(c); continue; }
            float ratio = (float)idx / (cnt - 1);
            var clr = Color.Lerp(start, end, ratio);
            res.Append($"[c/{clr.Hex3()}:{c}]");
            idx++;
        }
        return res.ToString();
    }
    #endregion

    #region 返回物品图标方法
    public static string Icon(int itemID) => $"[i:{itemID}]";
    public static string Icon(int itemID, int stack) => $"[i/s{stack}:{itemID}]";
    #endregion

    #region 将字符串转换为哈希码作为字典键
    public static int GetHashKey(string str) => str.GetHashCode();
    #endregion
}
