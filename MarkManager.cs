using Newtonsoft.Json;
using Terraria;
using Terraria.Utilities;
using TShockAPI;
using static MonsterSpeed.MonsterSpeed;
using static MonsterSpeed.PxUtil;

namespace MonsterSpeed;

/// <summary>指示物修改配置（用于时间事件/弹幕更新等）</summary>
public class MarkData
{
    [JsonProperty("查标志")]
    public string Flag { get; set; } = "";  // 目标怪物需要匹配的标志
    [JsonProperty("怪物ID")]
    public int MstID { get; set; } = 0;  // 目标怪物ID（0表示不限制）
    [JsonProperty("范围内格数")]
    public int Range { get; set; } = 0;  // 搜索半径（格数），0表示全图
    [JsonProperty("指示物条件")]
    public Dictionary<string, string[]> MarkCond { get; set; } = new(); // 指示物条件（键为名称，值为条件表达式数组）
    [JsonProperty("指示物修改")]
    public Dictionary<string, string[]> MarkMod { get; set; } = new(); // 指示物修改操作（键为名称，值为操作表达式数组）
}

/// <summary>指示物工具类：提供设置、检查、注入AI等功能</summary>
public static class MarkManager
{
    // 静态只读运算符列表，避免每次调用时重新创建数组
    private static readonly string[] MathOps = ["+=", "-=", "*=", "/=", "%=", "=", "+", "-", "*", "/", "%"];
    private static readonly string[] CmpOps = ["==", "!=", ">=", "<=", ">", "<", "="];

    #region 核心方法
    /// <summary>设置单个指示物（支持清除、随机、引用、数学运算）</summary>
    /// <param name="st">NPC状态对象</param>
    /// <param name="name">指示物名称</param>
    /// <param name="ops">操作表达式数组（如 ["+1", "*2", "=5"]）</param>
    /// <param name="rnd">随机数生成器引用</param>
    /// <param name="npc">关联的NPC（用于属性引用）</param>
    /// <returns>是否成功设置</returns>
    public static bool SetMk(NpcState st, string name, string[] ops, ref UnifiedRandom rnd, NPC? npc = null)
    {
        // 参数有效性检查
        if (st == null || string.IsNullOrEmpty(name) || ops == null) return false;

        // 如果 Markers 字典为空，则初始化
        st.Markers ??= new Dictionary<string, int>();

        // 检查操作列表中是否包含 "clear" 指令（字符串转小写方便比较）
        if (ops.Any(o => o?.Trim().ToLower() == "clear"))
        {
            st.Markers.Remove(name); // 移除该指示物
            return true;
        }

        int cur = st.Markers.GetValueOrDefault(name, 0); // 当前值，不存在则为 0
        int final = cur;
        var refs = new HashSet<string>(); // 用于检测循环引用

        // 依次执行每个操作表达式
        foreach (string op in ops)
        {
            if (string.IsNullOrEmpty(op)) continue;
            final = ApplyOp(final, op, st, ref rnd, ref refs, npc);
        }

        if (final < 0) final = 0;          // 不允许负值
        st.Markers[name] = final;          // 更新指示物
        return true;
    }

    /// <summary>批量设置指示物</summary>
    /// <param name="st">NPC状态对象</param>
    /// <param name="ops">指示物修改字典（键为名称，值为操作数组）</param>
    /// <param name="rnd">随机数生成器引用</param>
    /// <param name="npc">关联的NPC</param>
    /// <returns>成功修改的指示物数量</returns>
    public static int SetMks(NpcState st, Dictionary<string, string[]> ops, ref UnifiedRandom rnd, NPC? npc = null)
    {
        if (st == null || ops == null) return 0;
        int cnt = 0;

        // 遍历每个指示物配置并调用 SetMk
        foreach (var kv in ops)
            if (SetMk(st, kv.Key, kv.Value, ref rnd, npc))
                cnt++;

        return cnt;
    }

    /// <summary>检查指示物条件是否全部满足</summary>
    /// <param name="st">NPC状态对象</param>
    /// <param name="conds">条件字典（键为指示物名称，值为条件表达式数组）</param>
    /// <param name="npc">关联的NPC</param>
    /// <returns>所有条件满足返回 true，否则 false</returns>
    public static bool ChkMks(NpcState st, Dictionary<string, string[]> conds, NPC? npc = null)
    {
        if (st == null || conds == null || conds.Count == 0) return true;
        // 遍历所有条件项
        foreach (var kv in conds)
        {
            string name = kv.Key;
            string[] exprs = kv.Value;
            if (string.IsNullOrEmpty(name) || exprs == null) continue;
            int cur = st.Get(name);          // 获取当前指示物值
            foreach (string expr in exprs)   // 每个条件表达式都必须满足
            {
                if (!Eval(cur, expr, st, npc))
                    return false;
            }
        }
        return true;
    }

    /// <summary>将指示物值注入弹幕 AI 字段</summary>
    /// <param name="st">NPC状态对象</param>
    /// <param name="map">注入映射（键为AI索引，值为指示物名称*系数）</param>
    /// <param name="proj">目标弹幕</param>
    /// <returns>成功注入的AI字段数量</returns>
    public static int InjectAI(NpcState st, Dictionary<int, string> map, Projectile proj)
    {
        if (st == null || map == null || proj == null) return 0;
        int cnt = 0;
        // 遍历注入映射表
        foreach (var kv in map)
        {
            int idx = kv.Key;
            if (idx < 0 || idx >= proj.ai.Length) continue; // 索引无效则跳过
            string[] parts = kv.Value.Split('*');
            string mName = parts[0];
            float factor = parts.Length > 1 && float.TryParse(parts[1], out float f) ? f : 1f;
            proj.ai[idx] = st.Get(mName) * factor; // 将指示物值乘以系数后赋给 AI
            cnt++;
        }
        return cnt;
    }
    #endregion

    #region 辅助方法
    /// <summary>在怪物群体中设置指示物（根据条件筛选），使用 NpcMap 缓存提升性能</summary>
    /// <param name="mods">指示物修改配置列表</param>
    /// <param name="npc">源 NPC（用于范围参考和自身排除）</param>
    /// <param name="rnd">随机数生成器</param>
    /// <returns>成功修改的指示物总数</returns>
    public static int SetMstMks(List<MarkData> mods, NPC npc, ref UnifiedRandom rnd)
    {
        if (mods == null || npc == null) return 0;
        int total = 0;
        var npcMap = NpcMap; // 只获取一次全局缓存

        foreach (var mod in mods)
        {
            // 如果该配置没有修改项，跳过
            if (mod.MarkMod == null || mod.MarkMod.Count == 0) continue;
            // 跳过假人（TargetDummy）
            if (mod.MstID == Terraria.ID.NPCID.TargetDummy) continue;

            // 倒序遍历 NpcMap，便于将来移除无效项（当前仅遍历）
            for (int i = npcMap.Count - 1; i >= 0; i--)
            {
                int idx = npcMap[i];
                var n = Main.npc[idx];

                // 目标必须存在、活跃且不是自身
                if (n == null || !n.active || n.whoAmI == npc.whoAmI) continue;

                // 如果配置了怪物ID，则必须匹配
                if (mod.MstID != 0 && n.netID != mod.MstID) continue;

                // 如果配置了范围，检查距离是否在范围内（格数转像素）
                if (mod.Range > 0)
                {
                    float rPx = mod.Range * 16f;
                    if (npc.Center.DistanceSQ(n.Center) > rPx * rPx) continue;
                }

                // 获取目标 NPC 的状态
                var st = StateApi.GetState(n);
                if (st == null) continue;

                // 检查指示物条件（如果有）
                if (mod.MarkCond != null && mod.MarkCond.Count > 0 && !ChkMks(st, mod.MarkCond, npc))
                    continue;

                // 如果配置了标志，则检查目标 NPC 的标志是否匹配（需在配置表中有记录）
                if (!string.IsNullOrEmpty(mod.Flag))
                {
                    bool flag = Config.NpcDatas?.Any(d => d.Type.Contains(n.netID) && st.Flag == mod.Flag) == true;
                    if (!flag) continue;
                }

                // 所有条件通过，在目标 NPC 上执行指示物修改
                total += SetMks(st, mod.MarkMod, ref rnd, npc);
            }
        }
        return total;
    }

    /// <summary>应用单个操作表达式</summary>
    /// <param name="cur">当前值</param>
    /// <param name="op">操作表达式字符串</param>
    /// <param name="st">NPC状态</param>
    /// <param name="rnd">随机数生成器</param>
    /// <param name="refs">已引用的指示物集合（循环检测）</param>
    /// <param name="npc">关联NPC</param>
    /// <returns>应用操作后的新值</returns>
    private static int ApplyOp(int cur, string op, NpcState st, ref UnifiedRandom rnd, ref HashSet<string> refs, NPC? npc)
    {
        if (string.IsNullOrEmpty(op)) return cur;
        string cmd = op.Trim();

        // 处理 [属性] 引用，例如 "[血量]" 或 "[ai0]"
        if (cmd.StartsWith("[") && cmd.EndsWith("]"))
        {
            string prop = cmd.Trim('[', ']');
            return GetProp(st, prop, npc); // 获取 NPC 属性值
        }

        // 处理随机数：random:min,max 或 rm:min,max
        if (cmd.StartsWith("random:") || cmd.StartsWith("rm:"))
        {
            string pref = cmd.StartsWith("random:") ? "random:" : "rm:";
            string rng = cmd.Substring(pref.Length).Trim();
            var (ok, min, max) = ParseRngInt(rng); // 解析范围
            if (!ok) return cur;
            // 生成随机数（包含两端）
            return min < max ? rnd.Next(min, max + 1) : rnd.Next(max, min + 1);
        }

        // 处理引用：ref:名称*系数 或 using:/use: 同义
        if (cmd.StartsWith("ref:") || cmd.StartsWith("using:") || cmd.StartsWith("use:"))
        {
            string pref = cmd.StartsWith("ref:") ? "ref:" : (cmd.StartsWith("using:") ? "using:" : "use:");
            string expr = cmd.Substring(pref.Length).Trim();
            if (string.IsNullOrEmpty(expr)) return cur;
            return ResolveRef(expr, st, ref refs);
        }

        // 数学运算（+=, -=, =, 等）
        return Calc(cur, cmd, st, ref refs);
    }

    /// <summary>解析引用表达式 (ref:/using:/use:名称*系数) 返回数值</summary>
    /// <param name="expr">引用表达式（不含前缀）</param>
    /// <param name="st">NPC状态</param>
    /// <param name="refs">已引用的指示物集合（循环检测）</param>
    /// <returns>引用的指示物值乘以系数</returns>
    private static int ResolveRef(string expr, NpcState st, ref HashSet<string> refs)
    {
        if (string.IsNullOrEmpty(expr)) return 0;
        string[] parts = expr.Split('*');
        string refName = parts[0].Trim();
        if (string.IsNullOrEmpty(refName)) return 0;

        // 检测循环引用
        if (refs.Contains(refName))
        {
            TShock.Log.ConsoleError($"循环引用: {refName}");
            return 0;
        }
        refs.Add(refName);
        float factor = parts.Length > 1 && float.TryParse(parts[1], out float f) ? f : 1f;
        int val = st.Get(refName);
        refs.Remove(refName);
        return (int)(val * factor);
    }

    /// <summary>执行数学运算（+=, -=, =, 等）</summary>
    /// <param name="cur">当前值</param>
    /// <param name="expr">数学表达式（如 "+=5"）</param>
    /// <param name="st">NPC状态</param>
    /// <param name="refs">已引用的指示物集合</param>
    /// <returns>运算结果</returns>
    private static int Calc(int cur, string expr, NpcState st, ref HashSet<string> refs)
    {
        if (string.IsNullOrEmpty(expr)) return cur;
        // 按优先级顺序尝试匹配运算符
        foreach (string op in MathOps)
        {
            if (!expr.StartsWith(op)) continue;
            string rest = expr.Substring(op.Length).Trim();
            int val = ParseVal(rest, st, ref refs); // 解析右侧数值（可能含引用）
            return op switch
            {
                "+=" => cur + val,
                "-=" => cur - val,
                "*=" => cur * val,
                "/=" => val != 0 ? cur / val : cur,
                "%=" => val != 0 ? cur % val : cur,
                "=" => val,
                "+" => cur + val,
                "-" => cur - val,
                "*" => cur * val,
                "/" => val != 0 ? cur / val : cur,
                "%" => val != 0 ? cur % val : cur,
                _ => cur
            };
        }
        // 如果没有运算符，则尝试直接解析为整数（赋值）
        return int.TryParse(expr, out int d) ? d : cur;
    }

    /// <summary>解析数值（支持引用）</summary>
    /// <param name="str">数字或引用表达式</param>
    /// <param name="st">NPC状态</param>
    /// <param name="refs">已引用的指示物集合</param>
    /// <returns>解析后的整数值</returns>
    private static int ParseVal(string str, NpcState st, ref HashSet<string> refs)
    {
        if (string.IsNullOrEmpty(str)) return 0;
        // 同样处理 ref:/using:/use: 引用
        if (str.StartsWith("ref:") || str.StartsWith("using:") || str.StartsWith("use:"))
        {
            string pref = str.StartsWith("ref:") ? "ref:" : (str.StartsWith("using:") ? "using:" : "use:");
            string expr = str.Substring(pref.Length).Trim();
            if (string.IsNullOrEmpty(expr)) return 0;
            return ResolveRef(expr, st, ref refs);
        }
        // 纯数字
        return int.TryParse(str, out int v) ? v : 0;
    }

    /// <summary>获取 NPC 属性值（用于 [属性] 表达式）</summary>
    /// <param name="st">NPC状态</param>
    /// <param name="prop">属性名称（如 "血量", "ai0"）</param>
    /// <param name="npc">NPC实例</param>
    /// <returns>属性对应的整数值</returns>
    private static int GetProp(NpcState st, string prop, NPC? npc)
    {
        if (npc == null) return 0;
        return prop.ToLower() switch
        {
            "序号" or "index" => npc.whoAmI,
            "被击" or "struck" => st.Struck,
            "击杀" or "killplay" => st.KillPlay,
            "耗时" or "time" => st.ActiveTime,
            "x坐标" or "x" => (int)npc.Center.X,
            "y坐标" or "y" => (int)npc.Center.Y,
            "血量" or "life" => npc.life,
            "ai0" => (int)npc.ai[0],
            "ai1" => (int)npc.ai[1],
            "ai2" => (int)npc.ai[2],
            "ai3" => (int)npc.ai[3],
            _ => st.Get(prop) // 否则认为是指示物名称，获取其值
        };
    }

    /// <summary>评估单个条件表达式（==, !=, >=, <=, >, <, =）</summary>
    /// <param name="cur">当前指示物值</param>
    /// <param name="expr">条件表达式（如 ">=5"）</param>
    /// <param name="st">NPC状态</param>
    /// <param name="npc">关联NPC</param>
    /// <returns>条件满足返回 true</returns>
    private static bool Eval(int cur, string expr, NpcState st, NPC? npc)
    {
        if (string.IsNullOrEmpty(expr)) return true;
        var rs = new HashSet<string>(); // 条件中可能含引用，临时集合
        // 尝试匹配所有比较运算符
        foreach (string op in CmpOps)
        {
            if (!expr.StartsWith(op)) continue;
            string rest = expr.Substring(op.Length).Trim();
            int req = ParseVal(rest, st, ref rs);
            return op switch
            {
                "==" => cur == req,
                "!=" => cur != req,
                ">=" => cur >= req,
                "<=" => cur <= req,
                ">" => cur > req,
                "<" => cur < req,
                "=" => cur == req,        // 单等号等同于 ==
                _ => cur >= req
            };
        }
        // 如果没有运算符，默认视为 >= 比较（即数值下限）
        int def = ParseVal(expr, st, ref rs);
        return cur >= def;
    }
    #endregion
}