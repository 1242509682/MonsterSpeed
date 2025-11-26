using Newtonsoft.Json;
using Terraria;
using Terraria.Utilities;
using TShockAPI;

namespace MonsterSpeed;

#region 指示物核心类
public class MstMarkerMod
{
    [JsonProperty("查标志", Order = -1)]
    public string Flag { get; set; } = "";
    [JsonProperty("怪物ID", Order = 0)]
    public int MstID { get; set; } = 0;
    [JsonProperty("范围内格数", Order = 1)]
    public int Range { get; set; } = 0;
    [JsonProperty("指示物条件", Order = 2)]
    public Dictionary<string, string[]> MarkerConds { get; set; } = new Dictionary<string, string[]>();
    [JsonProperty("指示物修改", Order = 3)]
    public Dictionary<string, string[]> MarkerMods { get; set; } = new Dictionary<string, string[]>();
}
#endregion

public static class MarkerUtil
{
    #region 设置指示物
    public static bool SetMarker(NpcState state, string mName, string[] ops, ref UnifiedRandom rand, NPC npc = null)
    {
        if (state == null || string.IsNullOrEmpty(mName) || ops == null)
            return false;
    
        try
        {
            if (state.Markers == null)
                state.Markers = new Dictionary<string, int>();
    
            // 清除操作检查
            if (ops.Any(o => o?.Trim().ToLower() == "clear"))
            {
                state.Markers.Remove(mName);
                return true;
            }
    
            int curr = state.Markers.GetValueOrDefault(mName, 0);
            int final = curr;
    
            // 为每个操作序列创建独立的引用追踪集合
            var Refs = new HashSet<string>();
            
            foreach (string op in ops)
            {
                if (!string.IsNullOrEmpty(op))
                    final = ApplyOp(curr, op, state, ref rand, Refs, npc);
            }
    
            if (final < 0) final = 0;
            state.Markers[mName] = final;
            return true;
        }
        catch (System.Exception ex)
        {
            TShock.Log.ConsoleError($"设置指示物失败: {mName}, 错误: {ex.Message}");
            return false;
        }
    }
    #endregion
    
    #region 应用操作
    private static int ApplyOp(int curr, string op, NpcState st, ref UnifiedRandom rnd, HashSet<string> Refs = null, NPC npc = null)
    {
        if (string.IsNullOrEmpty(op)) return curr;
    
        try
        {
            string cmd = op.Trim();

            // 新增：npc属性引用
            if (cmd.StartsWith("[") && cmd.EndsWith("]"))
            {
                string propName = cmd.Trim('[', ']');
                return GetNPCPropertyValue(st, propName, npc);
            }

            // 随机范围处理
            if (cmd.StartsWith("random:") || cmd.StartsWith("rm:"))
            {
                string prefix = cmd.StartsWith("random:") ? "random:" : "rm:";
                string range = cmd.Substring(prefix.Length).Trim();
                var (ok, min, max) = PxUtil.ParseRange(range, s => int.Parse(s.Trim()));
    
                if (ok)
                {
                    if (min == max) return min;
                    return min < max ? rnd.Next(min, max + 1) : rnd.Next(max, min + 1);
                }
                return curr;
            }
    
            // 引用处理 - 添加循环引用检测
            if (cmd.StartsWith("ref:") || cmd.StartsWith("using:") || cmd.StartsWith("use:"))
            {
                string prefix = GetRefPrefix(cmd);
                string expr = cmd.Substring(prefix.Length).Trim();
                if (string.IsNullOrEmpty(expr)) return curr;
    
                string[] parts = expr.Split('*');
                string refName = parts[0].Trim();
                if (string.IsNullOrEmpty(refName)) return curr;
    
                // 检查循环引用
                Refs ??= new HashSet<string>();
                if (Refs.Contains(refName))
                {
                    TShock.Log.ConsoleError($"检测到循环引用: {refName} -> {refName}");
                    return curr; // 返回当前值，避免无限递归
                }
                
                Refs.Add(refName);
    
                float factor = 1f;
                if (parts.Length >= 2 && float.TryParse(parts[1].Trim(), out float fVal))
                    factor = fVal;
    
                int refVal = GetMarker(st, refName);
                
                // 处理完成后移除引用追踪
                Refs.Remove(refName);
                
                return (int)(refVal * factor);
            }
    
            // 数学运算
            return ParseMath(curr, cmd, st, Refs);
        }
        catch (System.Exception ex)
        {
            TShock.Log.ConsoleError($"操作失败: {op}, 错误: {ex.Message}");
            return curr;
        }
    }
    #endregion
    
    #region 解析数学
    private static int ParseMath(int curr, string expr, NpcState st, HashSet<string> Refs = null)
    {
        if (string.IsNullOrEmpty(expr)) return curr;
    
        try
        {
            string[] ops = { "+=", "-=", "*=", "/=", "%=", "=", "+", "-", "*", "/", "%" };
    
            foreach (string op in ops)
            {
                if (expr.StartsWith(op))
                {
                    string valStr = expr.Substring(op.Length).Trim();
                    int val = ParseVal(valStr, st, Refs);
    
                    return op switch
                    {
                        "+=" => curr + val,
                        "-=" => curr - val,
                        "*=" => curr * val,
                        "/=" => val != 0 ? curr / val : curr,
                        "%=" => val != 0 ? curr % val : curr,
                        "=" => val,
                        "+" => curr + val,
                        "-" => curr - val,
                        "*" => curr * val,
                        "/" => val != 0 ? curr / val : curr,
                        "%" => val != 0 ? curr % val : curr,
                        _ => curr
                    };
                }
            }
    
            if (int.TryParse(expr, out int dVal))
                return dVal;
    
            return curr;
        }
        catch (System.Exception ex)
        {
            TShock.Log.ConsoleError($"数学解析失败: {expr}, 错误: {ex.Message}");
            return curr;
        }
    }
    #endregion

    #region NPC属性值获取
    private static int GetNPCPropertyValue(NpcState state, string propName, NPC npc)
    {
        if (npc == null) return 0;

        return propName.ToLower() switch
        {
            "序号" or "index" => npc.whoAmI,
            "被击" or "struck" => state?.Struck ?? 0,
            "击杀" or "killplay" => state?.KillPlay ?? 0,
            "耗时" or "time" => state?.ActiveTime ?? 0,
            "x坐标" or "x" => (int)npc.Center.X,
            "y坐标" or "y" => (int)npc.Center.Y,
            "血量" or "life" => npc.life,
            "ai0" => (int)npc.ai[0],
            "ai1" => (int)npc.ai[1],
            "ai2" => (int)npc.ai[2],
            "ai3" => (int)npc.ai[3],
            _ => GetMarker(state, propName) // 默认为指示物名称
        };
    }
    #endregion

    #region 解析数值
    private static int ParseVal(string vStr, NpcState st, HashSet<string> Refs = null)
    {
        if (string.IsNullOrEmpty(vStr)) return 0;
    
        try
        {
            if (vStr.StartsWith("ref:") || vStr.StartsWith("using:") || vStr.StartsWith("use:"))
            {
                string prefix = GetRefPrefix(vStr);
                string expr = vStr.Substring(prefix.Length).Trim();
                if (string.IsNullOrEmpty(expr)) return 0;
    
                string[] parts = expr.Split('*');
                string refName = parts[0].Trim();
                if (string.IsNullOrEmpty(refName)) return 0;
    
                // 检查循环引用
                Refs ??= new HashSet<string>();
                if (Refs.Contains(refName))
                {
                    TShock.Log.ConsoleError($"检测到循环引用: {refName} -> {refName}");
                    return 0; // 返回0，避免无限递归
                }
                
                Refs.Add(refName);
    
                float factor = 1f;
                if (parts.Length >= 2 && float.TryParse(parts[1].Trim(), out float fVal))
                    factor = fVal;
    
                int refVal = GetMarker(st, refName);
                
                // 处理完成后移除引用追踪
                Refs.Remove(refName);
                
                return (int)(refVal * factor);
            }
    
            if (int.TryParse(vStr, out int val))
                return val;
    
            return 0;
        }
        catch (System.Exception ex)
        {
            TShock.Log.ConsoleError($"数值解析失败: {vStr}, 错误: {ex.Message}");
            return 0;
        }
    }
    #endregion

    #region 获取指示物
    public static int GetMarker(NpcState st, string name, int defVal = 0)
    {
        return st?.Markers?.GetValueOrDefault(name, defVal) ?? defVal;
    }
    #endregion

    #region 检查条件
    public static bool CheckMarkers(NpcState st, Dictionary<string, string[]> conds, NPC npc = null!)
    {
        if (st == null || conds == null || conds.Count == 0)
            return true;

        foreach (var cond in conds)
        {
            if (!CheckSingle(st, cond.Key, cond.Value, npc))
                return false;
        }
        return true;
    }
    #endregion

    #region 检查单个
    private static bool CheckSingle(NpcState st, string mName, string[] exprs, NPC npc)
    {
        if (string.IsNullOrEmpty(mName) || exprs == null)
            return true;

        try
        {
            int curr = GetMarker(st, mName);

            foreach (string expr in exprs)
            {
                if (!string.IsNullOrEmpty(expr) && !EvalCond(curr, expr, st, npc))
                    return false;
            }
            return true;
        }
        catch (System.Exception ex)
        {
            TShock.Log.ConsoleError($"检查失败: {mName}, 错误: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region 评估条件
    private static bool EvalCond(int curr, string expr, NpcState st, NPC npc)
    {
        if (string.IsNullOrEmpty(expr)) return true;

        try
        {
            string cond = expr.Trim();

            string[] cmpOps = { "==", "!=", ">=", "<=", ">", "<", "=" };

            foreach (string op in cmpOps)
            {
                if (cond.StartsWith(op))
                {
                    string vStr = cond.Substring(op.Length).Trim();
                    int req = ParseVal(vStr, st);

                    return op switch
                    {
                        "==" => curr == req,
                        "!=" => curr != req,
                        ">=" => curr >= req,
                        "<=" => curr <= req,
                        ">" => curr > req,
                        "<" => curr < req,
                        "=" => curr == req,
                        _ => curr >= req
                    };
                }
            }

            int defReq = ParseVal(cond, st);
            return curr >= defReq;
        }
        catch (System.Exception ex)
        {
            TShock.Log.ConsoleError($"条件评估失败: {expr}, 错误: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region 批量设置
    public static int SetMarkers(NpcState st, Dictionary<string, string[]> ops, ref UnifiedRandom rnd, NPC npc = null!)
    {
        if (st == null || ops == null) return 0;

        int count = 0;
        foreach (var op in ops)
        {
            if (SetMarker(st, op.Key, op.Value, ref rnd, npc))
                count++;
        }
        return count;
    }
    #endregion

    #region 注入AI
    public static int InjectToAI(NpcState st, Dictionary<int, string> injMap, Projectile proj)
    {
        if (st == null || injMap == null || proj == null) return 0;

        int count = 0;
        foreach (var inj in injMap)
        {
            int idx = inj.Key;
            if (idx < 0 || idx >= proj.ai.Length) continue;

            try
            {
                string[] parts = inj.Value.Split('*');
                string mName = parts[0];
                float factor = 1f;

                if (parts.Length >= 2 && float.TryParse(parts[1], out float fVal))
                    factor = fVal;

                proj.ai[idx] = GetMarker(st, mName) * factor;
                count++;
            }
            catch (System.Exception ex)
            {
                TShock.Log.ConsoleError($"AI注入失败: {inj.Value}, 错误: {ex.Message}");
            }
        }
        return count;
    }
    #endregion

    #region 设置怪物指示物
    public static int SetMstMarkers(List<MstMarkerMod> mods, NPC npc, ref UnifiedRandom rnd)
    {
        if (mods == null || npc == null) return 0;

        var sState = StateUtil.GetState(npc);
        if (sState == null) return 0;

        int total = 0;

        foreach (var mod in mods)
        {
            if (mod.MarkerMods == null || mod.MarkerMods.Count == 0)
                continue;

            if (mod.MstID == 488) // 跳过傀儡稻草人
                continue;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (!ValidTarget(i, mod, npc)) continue;

                var tState = StateUtil.GetState(Main.npc[i]);
                if (tState == null) continue;

                int modded = SetMarkers(tState, mod.MarkerMods, ref rnd, npc);
                total += modded;
            }
        }

        return total;
    }
    #endregion

    #region 验证目标
    private static bool ValidTarget(int idx, MstMarkerMod mod, NPC npc)
    {
        if (idx < 0 || idx >= Main.maxNPCs) return false;

        var tNpc = Main.npc[idx];

        if (!PxUtil.IsValidMst(tNpc, npc)) return false;
        if (mod.MstID != 0 && tNpc.netID != mod.MstID) return false;
        if (mod.Range > 0 && !PxUtil.InRangeTiles(npc.Center, tNpc.Center, mod.Range)) return false;

        var tState = StateUtil.GetState(tNpc);
        if (tState == null) return false;

        if (mod.MarkerConds != null && mod.MarkerConds.Count > 0 &&
            !CheckMarkers(tState, mod.MarkerConds, npc))
            return false;

        // 修改：适配 List<NpcData> 结构
        return string.IsNullOrEmpty(mod.Flag) ||
               (MonsterSpeed.Config?.NpcDatas?.Any(npcData =>
                   npcData.Type.Contains(tNpc.netID) && npcData.Flag == mod.Flag) == true);
    }
    #endregion

    #region 工具方法
    private static string GetRefPrefix(string cmd)
    {
        if (cmd.StartsWith("ref:")) return "ref:";
        if (cmd.StartsWith("using:")) return "using:";
        if (cmd.StartsWith("use:")) return "use:";
        return "";
    }
    #endregion

}