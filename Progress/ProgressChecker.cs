using Terraria;
using Terraria.GameContent.Events;
using System.Reflection;

namespace MonsterSpeed.Progress;

public class ProgressChecker
{
    public static bool IsProgress(ProgressType pt)
    {
        // 获取枚举成员信息
        var mi = typeof(ProgressType).GetMember(pt.ToString());
        if (mi.Length <= 0) return false;

        // 获取与该枚举成员关联的所有 ProgressMap 属性
        var attrs = mi[0].GetCustomAttributes(typeof(ProgressMap), false);

        foreach (var attr in attrs)
        {
            // 动态获取指定字段的当前值，并确保类型安全地进行比较
            var progressMap = (ProgressMap)attr;
            var fv = GetValue(progressMap.Filed);

            // 检查当前游戏状态是否符合所需的进度状态
            if (fv != null && progressMap.value != null &&
                Convert.ChangeType(fv, progressMap.value.GetType()).Equals(progressMap.value))
            {
                return true;
            }
        }

        // 如果没有找到匹配的游戏状态，则认为进度未解锁
        return false;
    }

    private static object GetValue(string fn)
    {
        // 尝试从 Main、NPC 或 DD2Event 类中获取公共静态字段
        Type[] typesToCheck = new[] { typeof(Main), typeof(NPC), typeof(DD2Event) };
        foreach (var type in typesToCheck)
        {
            var field = type.GetField(fn, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(null)!;
            }
        }

        // 如果找不到字段，抛出异常
        throw new ArgumentException($"字段 {fn} 未找到。");
    }
}
