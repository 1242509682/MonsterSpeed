using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.DataStructures;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.UpProj;

namespace MonsterSpeed;

#region 弹幕数据类
public class SpawnProjData
{
    // 基础参数
    [JsonProperty("弹幕ID", Order = 0)]
    public int Type = 115;
    [JsonProperty("数量", Order = 1)]
    public int Stack = 5;
    [JsonProperty("间隔/帧", Order = 2)]
    public float Interval = 60f;
    [JsonProperty("持续时间/帧", Order = 3)]
    public int Life = 120;
    [JsonProperty("触发条件", Order = 4)]
    public string Condition { get; set; } = "默认配置";
    [JsonProperty("伤害", Order = 5)]
    public int Damage = 30;
    [JsonProperty("击退", Order = 6)]
    public int KnockBack = 5;
    [JsonProperty("速度", Order = 7)]
    public float Velocity = 10f;

    // 位置和速度参数（使用字符串节省配置项）
    [JsonProperty("速度向量XY/格", Order = 8)]
    public string VelXY { get; set; } = "0,0";
    [JsonProperty("发射位置偏移XY/格", Order = 9)]
    public string SpawnXY { get; set; } = "0,0";
    [JsonProperty("面向修正XY", Order = 10)]
    public string FaceFix { get; set; } = "0,0";
    [JsonProperty("速度修正XY", Order = 11)]
    public string SpdFix { get; set; } = "0,0";

    // 目标相关
    [JsonProperty("以玩家为中心", Order = 13)]
    public bool TarCenter = false;
    [JsonProperty("以弹为位", Order = 25)]
    public bool UseProjPos { get; set; } = false;
    [JsonProperty("锁定范围", Order = 26)]
    public int LockRange { get; set; } = 0;
    [JsonProperty("锁定速度", Order = 27)]
    public float LockSpd { get; set; } = 0f;

    // 角度配置
    [JsonProperty("发射角度", Order = 20)]
    public string AngleCfg { get; set; } = "0";

    // 模式配置（数量为0时自动禁用）
    [JsonProperty("圆形数量", Order = 51)]
    public int CircleCnt { get; set; } = 0;
    [JsonProperty("圆形半径", Order = 52)]
    public float CircleRad { get; set; } = 0f;
    [JsonProperty("圆形起始角", Order = 53)]
    public float CircleStAng { get; set; } = 0f;
    [JsonProperty("圆形角度增量", Order = 54)]
    public float CircleAngInc { get; set; } = 0f;
    [JsonProperty("扇形数量", Order = 55)]
    public int SprCnt { get; set; } = 0;
    [JsonProperty("扇形角度增量", Order = 56)]
    public float SprAngInc { get; set; } = 0f;
    [JsonProperty("线性数量", Order = 57)]
    public int LineCnt { get; set; } = 0;
    [JsonProperty("线性偏移XY", Order = 58)]
    public string LineOff { get; set; } = "0,0";

    // 其他参数
    [JsonProperty("指示物注入AI", Order = 73)]
    public Dictionary<int, string> MkrToAI { get; set; } = new();
    [JsonProperty("更新弹幕", Order = 81)]
    public List<string> UpdProj { get; set; } = new();
    [JsonProperty("弹幕AI", Order = 82)]
    public Dictionary<int, float> AI { get; set; } = new();
    [JsonProperty("弹幕范围Buff", Order = 111)]
    public List<int> BuffTypes { get; set; } = new();
    [JsonProperty("Buff帧数", Order = 112)]
    public int BuffTime { get; set; } = 30;
    [JsonProperty("Buff范围", Order = 113)]
    public int BuffRng { get; set; } = 30;
}
#endregion

internal class SpawnProj
{
    #region 主发射方法(发射弹幕主入口)
    public static void Spawn(NpcData set, List<SpawnProjData> projs, NPC npc)
    {
        if (projs == null || projs.Count == 0 || npc == null) return;

        var tar = npc.GetTargetData(true);
        var st = StateApi.GetState(npc);
        if (st == null) return;

        // 确保索引有效
        if (st.SendProjIdx < 0) st.SendProjIdx = 0;
        if (st.SendProjIdx >= projs.Count) return;

        var data = projs[st.SendProjIdx];

        // 初始化状态
        if (!st.SendCnt.ContainsKey(st.SendProjIdx)) st.SendCnt[st.SendProjIdx] = 0;
        if (!st.SendCD.ContainsKey(st.SendProjIdx)) st.SendCD[st.SendProjIdx] = 0f;

        // 检查发射计数
        if (st.SendCnt[st.SendProjIdx] >= data.Stack && data.Stack > 0)
        {
            // 只有一组时等待冷却
            if (projs.Count == 1)
            {
                if (st.SendCD[st.SendProjIdx] <= 0)
                {
                    st.SendCnt[st.SendProjIdx] = 0;
                    st.SPCount++;
                }
                else
                {
                    st.SendCD[st.SendProjIdx] -= 1f;
                    return;
                }
            }
            else
            {
                // 多组切换到下一个
                int nxtIdx = FindNxt(projs, st.SendProjIdx + 1);
                if (nxtIdx >= 0 && nxtIdx != st.SendProjIdx)
                {
                    st.SendProjIdx = nxtIdx;
                    st.SendCnt[nxtIdx] = 0;
                    st.SendCD[nxtIdx] = 0f;
                    st.SPCount++;
                    return;
                }
                else
                {
                    st.SendCnt[st.SendProjIdx] = 0;
                    st.SPCount++;
                }
            }
        }

        // 条件检查
        if (!string.IsNullOrEmpty(data.Condition))
        {
            bool allow = true;
            var cond = ConditionFile.GetCondData(data.Condition);
            Conditions.Condition(npc, new StringBuilder(), set, cond, ref allow);

            if (!allow)
            {
                int nxtIdx = FindNxt(projs, st.SendProjIdx + 1);
                if (nxtIdx >= 0 && nxtIdx != st.SendProjIdx)
                {
                    st.SendProjIdx = nxtIdx;
                    st.SendCnt[nxtIdx] = 0;
                    st.SendCD[nxtIdx] = 0f;
                }
                st.SPCount++;
                return;
            }
        }

        // 冷却检查
        if (st.SendCD[st.SendProjIdx] > 0f)
        {
            st.SendCD[st.SendProjIdx] -= 1f;
        }
        else
        {
            if (data.Stack > 0)
            {
                GenProj(data, npc, tar, st);
            }
            else
            {
                int nxtIdx = FindNxt(projs, st.SendProjIdx + 1);
                if (nxtIdx >= 0 && nxtIdx != st.SendProjIdx)
                {
                    st.SendProjIdx = nxtIdx;
                    st.SendCnt[nxtIdx] = 0;
                    st.SendCD[nxtIdx] = 0f;
                }
                st.SPCount++;
            }
        }

        CheckAllUpdate(set, npc, projs);
    }
    #endregion

    #region 弹幕生成核心
    private static void GenProj(SpawnProjData data, NPC npc, NPCAimedTarget tar, NpcState st)
    {
        if (data == null || npc == null || st == null) return;

        var plr = Main.player[npc.target];
        if (plr == null || data.Life <= 0 || data.Type <= 0) return;

        // 计算基础位置
        Vector2 pos = data.TarCenter ? plr.Center : npc.Center;

        // 应用面向修正
        if (!string.IsNullOrWhiteSpace(data.FaceFix) && data.FaceFix != "0,0")
        {
            try
            {
                var fix = data.FaceFix.GetVector2();
                pos += new Vector2(fix.X * npc.direction, fix.Y * npc.directionY);
            }
            catch { }
        }

        // 计算基础速度
        Vector2 vel = GetVel(data, npc, tar);

        // 应用基础角度
        int idx = st.SendCnt.ContainsKey(st.SendProjIdx) ? st.SendCnt[st.SendProjIdx] : 0;
        vel = ApplyAng(vel, data.AngleCfg, idx, data);

        // 获取AI值
        var ai0 = data.AI.TryGetValue(0, out float a0) ? a0 : 0f;
        var ai1 = data.AI.TryGetValue(1, out float a1) ? a1 : 0f;
        var ai2 = data.AI.TryGetValue(2, out float a2) ? a2 : 0f;

        // 获取目标位置（用于以弹为位）
        Vector2 tgtPos = Vector2.Zero;
        if (data.UseProjPos || data.LockRange > 0)
        {
            int tgtIdx = GetLockTgt(npc, data);
            tgtPos = GetTgtPos(npc, data, tgtIdx);
        }

        // 生成单个弹幕（支持嵌套模式）
        GenSingle(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st);

        // 更新状态
        st.SendCnt[st.SendProjIdx]++;
        st.SendCD[st.SendProjIdx] = data.Interval;
        st.SPCount++;
    }
    #endregion

    #region 生成单个弹幕（支持嵌套）
    private static void GenSingle(SpawnProjData data, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgtPos, float ai0, float ai1, float ai2, NpcState st)
    {
        // 没有启用任何模式，发射基础弹幕
        if (data.CircleCnt <= 0 && data.SprCnt <= 0 && data.LineCnt <= 0)
        {
            Create(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st);
            return;
        }

        // 启用圆形模式（优先）
        if (data.CircleCnt > 0 && data.CircleRad > 0)
        {
            ProcCir(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st);
            return;
        }

        // 启用扇形模式（圆形未启用时）
        if (data.SprCnt > 0 && data.SprAngInc != 0)
        {
            ProcSpr(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st);
            return;
        }

        // 启用线性模式（其他未启用时）
        if (data.LineCnt > 0)
        {
            ProcLin(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st);
            return;
        }

        // 默认发射基础弹幕
        Create(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st);
    }
    #endregion

    #region 创建弹幕
    private static void Create(SpawnProjData data, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgtPos,
                               float ai0, float ai1, float ai2, NpcState st,
                               string flag = "", float angle = 0f, int idx = 0)
    {
        if (data.Type <= 0) return;

        Vector2 finalVel = vel;

        // 以弹为位模式：重新计算速度指向目标
        if (data.UseProjPos && tgtPos != Vector2.Zero)
        {
            Vector2 toTgt = tgtPos - pos;
            if (toTgt != Vector2.Zero)
            {
                float spd = data.LockSpd > 0 ? data.LockSpd : vel.Length();
                float bAng = (float)Math.Atan2(toTgt.Y, toTgt.X);
                finalVel = new Vector2((float)Math.Cos(bAng) * spd, (float)Math.Sin(bAng) * spd);

                // 重新应用速度修正
                if (!string.IsNullOrWhiteSpace(data.SpdFix) && data.SpdFix != "0,0")
                {
                    try
                    {
                        var fix = data.SpdFix.GetVector2();
                        finalVel += fix;
                    }
                    catch { }
                }

                // 重新应用角度配置
                int idx2 = st.SendCnt.ContainsKey(st.SendProjIdx) ? st.SendCnt[st.SendProjIdx] : 0;
                finalVel = ApplyAng(finalVel, data.AngleCfg, idx2, data);
            }
        }

        // 创建弹幕
        int projIdx = Projectile.NewProjectile(
            npc.GetSpawnSourceForNPCFromNPCAI(),
            pos.X, pos.Y, finalVel.X, finalVel.Y,
            data.Type, Math.Max(0, data.Damage), data.KnockBack,
            Main.myPlayer, ai0, ai1, ai2
        );

        if (projIdx < 0 || projIdx >= Main.maxProjectiles) return;

        var proj = Main.projectile[projIdx];
        if (data.Life > 0) proj.timeLeft = data.Life;

        // 范围Buff
        if (data.BuffRng > 0 && data.BuffTypes != null && data.BuffTypes.Count > 0 && data.BuffTime > 0)
        {
            float rngPx = data.BuffRng * 16f;
            float maxDist = rngPx * rngPx;

            foreach (TSPlayer plr in TShock.Players)
            {
                if (plr == null || !plr.Active || plr.Dead || !PxUtil.IsValidPlr(plr)) continue;
                if (proj.Center.DistanceSQ(plr.TPlayer.Center) > maxDist) continue;

                foreach (var buff in data.BuffTypes)
                {
                    if (plr.TPlayer.FindBuffIndex(buff) == -1)
                        plr.SetBuff(buff, data.BuffTime, false);
                }
            }
        }

        // 指示物注入AI
        if (data.MkrToAI != null && data.MkrToAI.Count > 0 && st != null)
            MarkerUtil.InjectToAI(st, data.MkrToAI, proj);

        // 注册更新弹幕
        if (data.UpdProj != null && data.UpdProj.Count > 0 &&
            projIdx >= 0 && projIdx < UpdateState.Length &&
          !AddState(projIdx, npc.whoAmI, data.Type, flag, angle, idx, pos, finalVel,
                    data.CircleRad, data.LineOff, data.SprAngInc))
            TShock.Log.ConsoleWarn($"[怪物加速] 注册弹幕更新状态失败: {projIdx}");
    }
    #endregion

    #region 查找下一个有效弹幕组
    private static int FindNxt(List<SpawnProjData> projs, int start)
    {
        if (projs == null || projs.Count == 0) return -1;

        for (int i = 0; i < projs.Count; i++)
        {
            int idx = (start + i) % projs.Count;
            if (projs[idx].Type > 0)
                return idx;
        }
        return -1;
    }
    #endregion

    #region 获取锁定目标
    private static int GetLockTgt(NPC npc, SpawnProjData data)
    {
        if (data.LockRange <= 0) return npc.target;

        int tgt = npc.target;
        float minDist = float.MaxValue;

        for (int i = 0; i < 255; i++)
        {
            var plr = Main.player[i];
            if (plr == null || !plr.active || plr.dead) continue;

            float dist = Vector2.Distance(npc.Center, plr.Center);
            if (dist <= data.LockRange * 16f && dist < minDist)
            {
                minDist = dist;
                tgt = i;
            }
        }

        return tgt;
    }
    #endregion

    #region 获取目标位置
    private static Vector2 GetTgtPos(NPC npc, SpawnProjData data, int tgtIdx)
    {
        var plr = Main.player[tgtIdx];
        if (plr == null || !plr.active) return npc.Center;

        // 解析偏移字符串
        float offX = 0, offY = 0;
        if (!string.IsNullOrWhiteSpace(data.SpawnXY) && data.SpawnXY != "0,0")
        {
            try
            {
                var parts = data.SpawnXY.Split(',');
                if (parts.Length == 2)
                {
                    if (float.TryParse(parts[0], out float x)) offX = x * 16f;
                    if (float.TryParse(parts[1], out float y)) offY = y * 16f;
                }
            }
            catch { }
        }

        return plr.Center + new Vector2(offX, offY);
    }
    #endregion

    #region 模式处理方法
    /// <summary>
    /// 处理圆形模式（支持扇形和线性嵌套）
    /// </summary>
    private static void ProcCir(SpawnProjData data, NPC npc, Vector2 bPos, Vector2 bVel, Vector2 tgtPos, float ai0, float ai1, float ai2, NpcState st)
    {
        float radPx = PxUtil.ToPx(data.CircleRad);
        float stAng = MathHelper.ToRadians(data.CircleStAng);
        float angInc = MathHelper.ToRadians(data.CircleAngInc);

        // 角度增量为0时均匀分布
        if (angInc == 0) angInc = MathHelper.TwoPi / data.CircleCnt;

        for (int i = 0; i < data.CircleCnt; i++)
        {
            float ang = stAng + i * angInc;
            Vector2 pos = bPos + new Vector2((float)Math.Cos(ang) * radPx, (float)Math.Sin(ang) * radPx);

            // 传递圆形模式标志
            string flag = "circle";
            // 圆形弹幕可嵌套扇形模式
            if (data.SprCnt > 0 && data.SprAngInc != 0)
            {
                ProcSprInCir(data, npc, pos, bVel, tgtPos, ai0, ai1, ai2, st, i);
            }
            // 圆形弹幕可嵌套线性模式
            else if (data.LineCnt > 0)
            {
                ProcLinInCir(data, npc, pos, bVel, tgtPos, ai0, ai1, ai2, st, i, ang);
            }
            else
            {
                Create(data, npc, pos, bVel, tgtPos, ai0, ai1, ai2, st, flag, ang, i);
            }
        }
    }

    /// <summary>
    /// 圆形中的扇形模式
    /// </summary>
    private static void ProcSprInCir(SpawnProjData data, NPC npc, Vector2 pos, Vector2 bVel, Vector2 tgtPos, float ai0, float ai1, float ai2, NpcState st, int cirIdx)
    {
        float bAng = (float)Math.Atan2(bVel.Y, bVel.X);
        float angInc = MathHelper.ToRadians(data.SprAngInc);
        float len = bVel.Length();

        if (len == 0) return;

        for (int i = 0; i < data.SprCnt; i++)
        {
            float ang = bAng + i * angInc;
            Vector2 vel = new Vector2((float)Math.Cos(ang) * len, (float)Math.Sin(ang) * len);

            // 传递扇形模式标志
            string flag = "spread";
            // 扇形可嵌套线性模式
            if (data.LineCnt > 0)
            {
                ProcLinInSpr(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st, i);
            }
            else
            {
                Create(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st, flag, ang, i);
            }
        }
    }

    /// <summary>
    /// 处理扇形模式（独立）
    /// </summary>
    private static void ProcSpr(SpawnProjData data, NPC npc, Vector2 pos, Vector2 bVel, Vector2 tgtPos, float ai0, float ai1, float ai2, NpcState st)
    {
        float bAng = (float)Math.Atan2(bVel.Y, bVel.X);
        float angInc = MathHelper.ToRadians(data.SprAngInc);
        float len = bVel.Length();

        if (len == 0) return;

        for (int i = 0; i < data.SprCnt; i++)
        {
            float ang = bAng + i * angInc;
            Vector2 vel = new Vector2((float)Math.Cos(ang) * len, (float)Math.Sin(ang) * len);

            // 传递扇形模式标志
            string flag = "spread";
            // 扇形可嵌套线性模式
            if (data.LineCnt > 0)
            {
                ProcLinInSpr(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st, i);
            }
            else
            {
                Create(data, npc, pos, vel, tgtPos, ai0, ai1, ai2, st, flag, ang, i);
            }
        }
    }

    #region 圆形中的线性模式
    private static void ProcLinInCir(SpawnProjData data, NPC npc, Vector2 pos, Vector2 bVel, Vector2 tgtPos, float ai0, float ai1, float ai2, NpcState st, int cirIdx, float cirAng)
    {
        Vector2 off = ParseOffset(data.LineOff);

        for (int i = 0; i < data.LineCnt; i++)
        {
            Vector2 linPos = pos + off * i;
            // ✅ 使用传递的圆形角度
            string flag = "circle_line";
            Create(data, npc, linPos, bVel, tgtPos, ai0, ai1, ai2, st, flag, cirAng, i);
        }
    }
    #endregion

    #region 扇形中的线性模式
    private static void ProcLinInSpr(SpawnProjData data, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgtPos, float ai0, float ai1, float ai2, NpcState st, int sprIdx)
    {
        Vector2 off = ParseOffset(data.LineOff);
        for (int i = 0; i < data.LineCnt; i++)
        {
            Vector2 linPos = pos + off * i;
            // ✅ 传递扇形标志和角度
            string flag = "spread_line";
            float bAng = (float)Math.Atan2(vel.Y, vel.X);
            float angle = bAng + sprIdx * MathHelper.ToRadians(data.SprAngInc);
            Create(data, npc, linPos, vel, tgtPos, ai0, ai1, ai2, st, flag, angle, i);
        }
    }
    #endregion

    /// <summary>
    /// 处理线性模式（独立）
    /// </summary>
    private static void ProcLin(SpawnProjData data, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgtPos, float ai0, float ai1, float ai2, NpcState st)
    {
        Vector2 off = ParseOffset(data.LineOff);

        for (int i = 0; i < data.LineCnt; i++)
        {
            Vector2 linPos = pos + off * i;
            // ✅ 传递线性模式标志
            string flag = "line";
            Create(data, npc, linPos, vel, tgtPos, ai0, ai1, ai2, st, flag, 0f, i);
        }
    }
    #endregion

    #region 获取速度向量
    private static Vector2 GetVel(SpawnProjData data, NPC npc, NPCAimedTarget tar)
    {
        Vector2 vel = Vector2.Zero;

        // 优先使用速度向量
        if (!string.IsNullOrWhiteSpace(data.VelXY) && data.VelXY != "0,0")
        {
            try
            {
                var res = PxUtil.ParseFloatRange(data.VelXY);
                if (res.success)
                {
                    vel = PxUtil.ToPx(new Vector2(res.min, res.max));
                    return vel;
                }
            }
            catch { }
        }

        // 使用标量速度+方向
        if (data.Velocity > 0)
        {
            var dir = tar.Center - npc.Center;
            if (dir != Vector2.Zero) vel = dir.SafeNormalize(Vector2.Zero) * data.Velocity;
        }

        // 应用速度修正
        if (!string.IsNullOrWhiteSpace(data.SpdFix) && data.SpdFix != "0,0")
        {
            try
            {
                var fix = data.SpdFix.GetVector2();
                vel += fix;
            }
            catch { }
        }

        return vel;
    }
    #endregion

    #region 应用角度配置
    private static Vector2 ApplyAng(Vector2 vel, string cfg, int idx, SpawnProjData data)
    {
        if (string.IsNullOrWhiteSpace(cfg) || cfg == "0" || vel == Vector2.Zero) return vel;

        try
        {
            var parts = cfg.Split(',');

            // 单个角度值
            if (parts.Length == 1)
            {
                if (float.TryParse(parts[0], out float ang))
                    return vel.RotatedBy(MathHelper.ToRadians(ang));
            }
            // 角度范围 min,max
            else if (parts.Length == 2)
            {
                if (float.TryParse(parts[0], out float min) &&
                    float.TryParse(parts[1], out float max))
                {
                    if (data.Stack > 0 && idx >= 0 && idx < data.Stack)
                    {
                        float range = max - min;
                        float step = range / Math.Max(1, data.Stack - 1);
                        float ang = min + step * idx;
                        return vel.RotatedBy(MathHelper.ToRadians(ang));
                    }
                    else
                    {
                        float ang = (min + max) / 2f;
                        return vel.RotatedBy(MathHelper.ToRadians(ang));
                    }
                }
            }
        }
        catch { }

        return vel;
    }
    #endregion

    #region 解析偏移字符串
    public static Vector2 ParseOffset(string offStr)
    {
        if (string.IsNullOrWhiteSpace(offStr) || offStr == "0,0")
            return Vector2.Zero;

        try
        {
            var parts = offStr.Split(',');
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y))
                    return new Vector2(x, y) * 16f;
            }
        }
        catch { }

        return Vector2.Zero;
    }
    #endregion
}