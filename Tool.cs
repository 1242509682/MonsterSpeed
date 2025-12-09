using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TShockAPI;

namespace MonsterSpeed;

internal class Tool
{
    #region 渐变着色方法 + 物品图标解析
    public static string TextGradient(string text)
    {
        // 如果文本中已包含 [c/xxx:] 自定义颜色标签，则不做渐变，只替换图标
        if (text.Contains("[c/"))
        {
            return ReplaceIconsOnly(text);
        }

        var name = new StringBuilder();
        int length = text.Length;

        for (int i = 0; i < length; i++)
        {
            char c = text[i];

            // 检查是否是图标标签 [i:xxx]
            if (c == '[' && i + 2 < length && text[i + 1] == 'i' && text[i + 2] == ':')
            {
                int end = text.IndexOf(']', i);
                if (end != -1)
                {
                    string tag = text.Substring(i, end - i + 1);
                    string content = tag[3..^1]; // 去掉 "[i:" 和 "]"

                    if (int.TryParse(content, out int itemID))
                    {
                        name.Append(ItemIcon(itemID));
                    }
                    else
                    {
                        name.Append(tag); // 无效ID保留原标签
                    }

                    i = end; // 跳过整个标签
                }
                else
                {
                    name.Append(c);
                    i++;
                }
            }
            else
            {
                // 如果是空白字符，直接追加
                if (char.IsWhiteSpace(c))
                {
                    name.Append(c);
                }
                else
                {
                    // 渐变颜色计算
                    var start = new Color(166, 213, 234);
                    var endColor = new Color(245, 247, 175);
                    float ratio = (float)i / (length - 1);
                    var color = Color.Lerp(start, endColor, ratio);

                    name.Append($"[c/{color.Hex3()}:{c}]");
                }
            }
        }

        return name.ToString();
    }
    #endregion

    #region 只替换图标，不做渐变
    private static string ReplaceIconsOnly(string text)
    {
        var result = new StringBuilder();
        int index = 0;
        int length = text.Length;

        while (index < length)
        {
            char c = text[index];

            if (c == '[' && index + 2 < length && text[index + 1] == 'i' && text[index + 2] == ':')
            {
                int end = text.IndexOf(']', index);
                if (end != -1)
                {
                    string tag = text.Substring(index, end - index + 1);
                    string content = tag[3..^1];

                    if (int.TryParse(content, out int itemID))
                    {
                        result.Append(ItemIcon(itemID));
                    }
                    else
                    {
                        result.Append(tag);
                    }

                    index = end + 1;
                }
                else
                {
                    result.Append(c);
                    index++;
                }
            }
            else
            {
                result.Append(c);
                index++;
            }
        }

        return result.ToString();
    }
    #endregion

    #region 返回物品图标方法
    // 方法：ItemIcon，根据给定的物品对象返回插入物品图标的格式化字符串
    public static string ItemIcon(Item item)
    {
        return ItemIcon(item.type);
    }

    // 方法：ItemIcon，根据给定的物品ID返回插入物品图标的格式化字符串
    public static string ItemIcon(ItemID itemID)
    {
        return ItemIcon(itemID);
    }

    // 方法：ItemIcon，根据给定的物品整型ID返回插入物品图标的格式化字符串
    public static string ItemIcon(int itemID)
    {
        return $"[i:{itemID}]";
    }
    #endregion

    #region 将字符串转换为哈希码作为字典键
    public static int GetHashKey(string str)
    {
        return str.GetHashCode();
    }
    #endregion

    #region 检查DLL有效性
    public static bool IsValidDll(string dllPath)
    {
        try
        {
            var name = AssemblyName.GetAssemblyName(dllPath);
            return name != null;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleWarn($"[怪物加速] 检查程序集失败 {Path.GetFileName(dllPath)}: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region 格式化 using
    public static string FmtUsings(List<string> usgs)
    {
        if (usgs == null || usgs.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        foreach (var usg in usgs)
        {
            if (string.IsNullOrWhiteSpace(usg))
                continue;

            var trim = usg.Trim();

            // 已完整
            if (trim.StartsWith("using ") && trim.EndsWith(";"))
            {
                sb.AppendLine(trim);
            }
            // 需补充
            else
            {
                sb.AppendLine($"using {trim};");
            }
        }

        return sb.ToString();
    }
    #endregion

    #region 提取代码中已有的 using 指令
    public static List<string> GetExistUsings(string code)
    {
        var usings = new List<string>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var Directives = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.ToString())
                .ToList();

            usings.AddRange(Directives);
        }
        catch
        {
            // 解析失败时使用简单方法
            var lines = code.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
                {
                    usings.Add(trimmed);
                }
            }
        }

        return usings;
    }
    #endregion

    #region 过滤掉重复的 using
    public static string FilterUsings(string fmtUsgs, List<string> exist)
    {
        var lines = fmtUsgs.Split('\n');
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var trim = line.Trim();

            // 取命名空间部分
            string nsOnly = GetNs(trim);

            // 查重复
            bool existFlag = exist.Any(ex =>
                SameNs(ex.Trim(), trim) || SameNs(ex.Trim(), nsOnly));

            if (!existFlag)
                result.AppendLine(trim);
        }

        return result.ToString();
    }

    // 取命名空间
    public static string GetNs(string usgStmt)
    {
        if (string.IsNullOrWhiteSpace(usgStmt))
            return string.Empty;

        var trim = usgStmt.Trim();

        if (trim.StartsWith("using "))
            trim = trim.Substring(6);

        if (trim.EndsWith(";"))
            trim = trim.Substring(0, trim.Length - 1);

        return trim.Trim();
    }

    // 比命名空间
    public static bool SameNs(string ex, string now)
    {
        var exNs = GetNs(ex);
        var nowNs = GetNs(now);

        return string.Equals(exNs, nowNs, StringComparison.OrdinalIgnoreCase);
    }
    #endregion
}
