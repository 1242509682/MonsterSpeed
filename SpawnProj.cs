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
/// <summary>
/// 弹幕发射配置数据
/// </summary>
public class SpawnProjData
{
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

    [JsonProperty("速度向量XY/格", Order = 8)]
    public string VelXY { get; set; } = "0,0";
    [JsonProperty("发射位置偏移XY/格", Order = 9)]
    public string SpawnXY { get; set; } = "0,0";
    [JsonProperty("面向修正XY", Order = 10)]
    public string FaceFix { get; set; } = "0,0";
    [JsonProperty("速度修正XY", Order = 11)]
    public string SpdFix { get; set; } = "0,0";

    [JsonProperty("以玩家为中心", Order = 13)]
    public bool TarCenter = false;
    [JsonProperty("以弹为位", Order = 25)]
    public bool UseProjPos { get; set; } = false;
    [JsonProperty("锁定范围", Order = 26)]
    public int LockRange { get; set; } = 0;
    [JsonProperty("锁定速度", Order = 27)]
    public float LockSpd { get; set; } = 0f;

    [JsonProperty("发射角度", Order = 20)]
    public string AngleCfg { get; set; } = "0";

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

    [JsonProperty("指示物", Order = 70)]
    public Dictionary<string, int> ScriptMarkers { get; set; } = new();

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

/// <summary>
/// 弹幕生成与发射核心类
/// </summary>
public class SpawnProj
{
    #region 主发射方法(发射弹幕主入口)
    /// <summary>
    /// 根据NPC状态和配置发射弹幕
    /// </summary>
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

        ChkUpdates(set, npc, projs);
    }
    #endregion

    #region 弹幕生成核心
    /// <summary>
    /// 根据配置生成弹幕
    /// </summary>
    private static void GenProj(SpawnProjData data, NPC npc, NPCAimedTarget tar, NpcState st)
    {
        if (data == null || npc == null || st == null) return;
        var plr = Main.player[npc.target];
        if (plr == null || data.Life <= 0 || data.Type <= 0) return;

        // 计算基础位置
        Vector2 pos = data.TarCenter ? plr.Center : npc.Center;

        // 应用面向修正
        if (!string.IsNullOrWhiteSpace(data.FaceFix) && data.FaceFix != "0,0")
            pos += data.FaceFix.GetVector2() * new Vector2(npc.direction, npc.directionY);

        // 计算基础速度
        Vector2 vel = GetVel(data, npc, tar);

        // 应用基础角度
        int idx = st.SendCnt.ContainsKey(st.SendProjIdx) ? st.SendCnt[st.SendProjIdx] : 0;
        vel = ApplyAng(vel, data.AngleCfg, idx, data.Stack);

        // 获取目标位置（用于以弹为位）
        Vector2 tgtPos = Vector2.Zero;
        if (data.UseProjPos || data.LockRange > 0)
        {
            int tgtIdx = GetLockTgt(npc, data);
            tgtPos = GetTgtPos(npc, data, tgtIdx);
        }

        // 获取AI值
        float a0 = data.AI.TryGetValue(0, out float v0) ? v0 : 0f;
        float a1 = data.AI.TryGetValue(1, out float v1) ? v1 : 0f;
        float a2 = data.AI.TryGetValue(2, out float v2) ? v2 : 0f;

        // 根据模式生成弹幕
        GenByMode(data, npc, pos, vel, tgtPos, a0, a1, a2, st);

        // 更新状态
        st.SendCnt[st.SendProjIdx]++;
        st.SendCD[st.SendProjIdx] = data.Interval;
        st.SPCount++;
    }
    #endregion

    #region 模式分发（统一入口）
    /// <summary>
    /// 根据配置选择圆形/扇形/线性模式，无模式则直接生成单发
    /// </summary>
    private static void GenByMode(SpawnProjData d, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgt,
        float ai0, float ai1, float ai2, NpcState st)
    {
        if (d.CircleCnt > 0 && d.CircleRad > 0)
            GenCircle(d, npc, pos, vel, tgt, ai0, ai1, ai2, st);
        else if (d.SprCnt > 0 && d.SprAngInc != 0)
            GenSpread(d, npc, pos, vel, tgt, ai0, ai1, ai2, st);
        else if (d.LineCnt > 0)
            GenLine(d, npc, pos, vel, tgt, ai0, ai1, ai2, st);
        else
            Create(d, npc, pos, vel, tgt, ai0, ai1, ai2, st, "", 0f, 0);
    }
    #endregion

    #region 圆形模式
    /// <summary>
    /// 圆形弹幕阵列，支持在圆上每个点再嵌套扇形或线性
    /// </summary>
    private static void GenCircle(SpawnProjData d, NPC npc, Vector2 bpos, Vector2 bvel, Vector2 tgt,
        float ai0, float ai1, float ai2, NpcState st)
    {
        float rad = d.CircleRad * 16f;
        float stAng = MathHelper.ToRadians(d.CircleStAng);
        float inc = d.CircleAngInc == 0 ? MathHelper.TwoPi / d.CircleCnt : MathHelper.ToRadians(d.CircleAngInc);
        for (int i = 0; i < d.CircleCnt; i++)
        {
            float ang = stAng + i * inc;
            Vector2 pos = bpos + new Vector2((float)Math.Cos(ang) * rad, (float)Math.Sin(ang) * rad);
            if (d.SprCnt > 0 && d.SprAngInc != 0)
                GenSpreadAt(d, npc, pos, bvel, tgt, ai0, ai1, ai2, st, ang);
            else if (d.LineCnt > 0)
                GenLineAt(d, npc, pos, bvel, tgt, ai0, ai1, ai2, st, "circle_line", ang, i);
            else
                Create(d, npc, pos, bvel, tgt, ai0, ai1, ai2, st, "circle", ang, i);
        }
    }
    #endregion

    #region 扇形模式
    /// <summary>
    /// 扇形弹幕，基于基准速度方向向外扩散
    /// </summary>
    private static void GenSpread(SpawnProjData d, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgt,
        float ai0, float ai1, float ai2, NpcState st)
    {
        float baseAng = (float)Math.Atan2(vel.Y, vel.X);
        float inc = MathHelper.ToRadians(d.SprAngInc);
        float len = vel.Length();
        if (len == 0) return;
        for (int i = 0; i < d.SprCnt; i++)
        {
            float ang = baseAng + i * inc;
            Vector2 nvel = new Vector2((float)Math.Cos(ang) * len, (float)Math.Sin(ang) * len);
            if (d.LineCnt > 0)
                GenLineAt(d, npc, pos, nvel, tgt, ai0, ai1, ai2, st, "spread_line", ang, i);
            else
                Create(d, npc, pos, nvel, tgt, ai0, ai1, ai2, st, "spread", ang, i);
        }
    }

    /// <summary>
    /// 在指定位置生成扇形（供圆形嵌套调用）
    /// </summary>
    private static void GenSpreadAt(SpawnProjData d, NPC npc, Vector2 pos, Vector2 bvel, Vector2 tgt,
        float ai0, float ai1, float ai2, NpcState st, float baseAng)
    {
        float inc = MathHelper.ToRadians(d.SprAngInc);
        float len = bvel.Length();
        if (len == 0) return;
        for (int i = 0; i < d.SprCnt; i++)
        {
            float ang = baseAng + i * inc;
            Vector2 nvel = new Vector2((float)Math.Cos(ang) * len, (float)Math.Sin(ang) * len);
            if (d.LineCnt > 0)
                GenLineAt(d, npc, pos, nvel, tgt, ai0, ai1, ai2, st, "spread_line", ang, i);
            else
                Create(d, npc, pos, nvel, tgt, ai0, ai1, ai2, st, "cir_spr", ang, i);
        }
    }
    #endregion

    #region 线性模式
    /// <summary>
    /// 线性弹幕，沿指定方向等距排列（独立使用）
    /// </summary>
    private static void GenLine(SpawnProjData d, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgt,
        float ai0, float ai1, float ai2, NpcState st)
    {
        Vector2 off = ParseOff(d.LineOff);
        for (int i = 0; i < d.LineCnt; i++)
        {
            Vector2 npos = pos + off * i;
            Create(d, npc, npos, vel, tgt, ai0, ai1, ai2, st, "line", 0f, i);
        }
    }

    /// <summary>
    /// 在指定位置生成线性弹幕（供其他模式嵌套使用，flag可变）
    /// </summary>
    private static void GenLineAt(SpawnProjData d, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgt,
        float ai0, float ai1, float ai2, NpcState st, string flag, float ang, int idx)
    {
        Vector2 off = ParseOff(d.LineOff);
        for (int i = 0; i < d.LineCnt; i++)
        {
            Vector2 npos = pos + off * i;
            Create(d, npc, npos, vel, tgt, ai0, ai1, ai2, st, flag, ang, idx);
        }
    }
    #endregion

    #region 创建弹幕
    /// <summary>
    /// 实际生成单个弹幕，应用所有修正和Buff
    /// </summary>
    private static void Create(SpawnProjData d, NPC npc, Vector2 pos, Vector2 vel, Vector2 tgt,
        float ai0, float ai1, float ai2, NpcState st, string flag, float ang, int idx)
    {
        if (d.Type <= 0) return;
        vel = FixVel(d, pos, vel, tgt, idx);

        // 弹幕的 NewProjectileModifier 是单次更新 并非周期性 所以可以在创建时就定义
        int pid = Projectile.NewProjectile(
            npc.GetSpawnSourceForNPCFromNPCAI(),
            pos.X, pos.Y, vel.X, vel.Y, d.Type,
            Math.Max(0, d.Damage), d.KnockBack, Main.myPlayer, ai0, ai1, ai2, modifer: p => 
            {
                p.damage = d.Damage;
                p.knockBack = d.KnockBack;
                p.hostile = true;   // 对玩家造成伤害
                p.friendly = false; // 不对其他NPC造成伤害（可选）
                p.netImportant = true; // 标记为重要确保网络同步
                p.netUpdate = true;
                p.netUpdate2 = true;
            }); 

        if (pid < 0 || pid >= Main.maxProjectiles) return;
        var proj = Main.projectile[pid];
        if (d.Life > 0) proj.timeLeft = d.Life;

        // 范围Buff
        if (d.BuffRng > 0 && d.BuffTypes != null && d.BuffTypes.Count > 0 && d.BuffTime > 0)
        {
            float maxDist = (d.BuffRng * 16f) * (d.BuffRng * 16f);
            foreach (TSPlayer plr in TShock.Players)
            {
                if (plr == null || !plr.Active || plr.Dead || plr.TPlayer == null || plr.TPlayer.statLife <= 0) continue;
                if (proj.Center.DistanceSQ(plr.TPlayer.Center) > maxDist) continue;
                foreach (int buff in d.BuffTypes)
                {
                    if (plr.TPlayer.FindBuffIndex(buff) == -1)
                        plr.SetBuff(buff, d.BuffTime, false);
                }
            }
        }

        // 注册更新弹幕（注意：CircleRad转换为像素）
        if (d.UpdProj != null && d.UpdProj.Count > 0 &&
            pid >= 0 && pid < UpdateState.Length &&
            !AddState(pid, npc.whoAmI, d.Type, flag, ang, idx, pos, vel,
                      d.CircleRad * 16f, d.LineOff, d.SprAngInc))
            TShock.Log.ConsoleWarn($"[怪物加速] 注册弹幕更新状态失败: {pid}");
    }
    #endregion

    #region 辅助方法（短命名 ≤10字符）
    /// <summary>查找下一个有效的弹幕组</summary>
    private static int FindNxt(List<SpawnProjData> projs, int start)
    {
        if (projs == null || projs.Count == 0) return -1;
        for (int i = 0; i < projs.Count; i++)
        {
            int idx = (start + i) % projs.Count;
            if (projs[idx].Type > 0) return idx;
        }
        return -1;
    }

    /// <summary>获取锁定目标玩家索引</summary>
    private static int GetLockTgt(NPC npc, SpawnProjData d)
    {
        if (d.LockRange <= 0) return npc.target;
        int tgt = npc.target;
        float min = float.MaxValue;
        for (int i = 0; i < 255; i++)
        {
            var plr = Main.player[i];
            if (plr == null || !plr.active || plr.dead) continue;
            float dist = Vector2.Distance(npc.Center, plr.Center);
            if (dist <= d.LockRange * 16f && dist < min)
            {
                min = dist;
                tgt = i;
            }
        }
        return tgt;
    }

    /// <summary>获取目标位置（考虑偏移）</summary>
    private static Vector2 GetTgtPos(NPC npc, SpawnProjData d, int tgtIdx)
    {
        var plr = Main.player[tgtIdx];
        if (plr == null || !plr.active) return npc.Center;
        Vector2 off = d.SpawnXY.GetVector2(); // 已是像素
        return plr.Center + off;
    }

    /// <summary>获取基础速度向量</summary>
    private static Vector2 GetVel(SpawnProjData d, NPC npc, NPCAimedTarget tar)
    {
        Vector2 vel = d.VelXY.GetVector2();
        if (vel != Vector2.Zero) return vel;
        if (d.Velocity > 0)
        {
            var dir = tar.Center - npc.Center;
            if (dir != Vector2.Zero)
                vel = dir.SafeNormalize(Vector2.Zero) * d.Velocity;
        }
        if (!string.IsNullOrWhiteSpace(d.SpdFix) && d.SpdFix != "0,0")
            vel += d.SpdFix.GetVector2();
        return vel;
    }

    /// <summary>应用角度配置（支持单值或范围）</summary>
    private static Vector2 ApplyAng(Vector2 v, string cfg, int idx, int total)
    {
        if (string.IsNullOrWhiteSpace(cfg) || cfg == "0" || v == Vector2.Zero) return v;
        try
        {
            string[] parts = cfg.Split(',');
            if (parts.Length == 1 && float.TryParse(parts[0], out float a))
                return v.RotatedBy(MathHelper.ToRadians(a));
            if (parts.Length == 2 && float.TryParse(parts[0], out float min) && float.TryParse(parts[1], out float max))
            {
                if (total > 0 && idx >= 0 && idx < total)
                {
                    float range = max - min;
                    float step = range / Math.Max(1, total - 1);
                    float ang = min + step * idx;
                    return v.RotatedBy(MathHelper.ToRadians(ang));
                }
                else
                {
                    float ang = (min + max) / 2f;
                    return v.RotatedBy(MathHelper.ToRadians(ang));
                }
            }
        }
        catch { }
        return v;
    }

    /// <summary>速度修正+锁定目标重定向</summary>
    private static Vector2 FixVel(SpawnProjData d, Vector2 pos, Vector2 vel, Vector2 tgt, int idx)
    {
        if (!d.UseProjPos || tgt == Vector2.Zero) return vel;
        Vector2 toTgt = tgt - pos;
        if (toTgt == Vector2.Zero) return vel;
        float spd = d.LockSpd > 0 ? d.LockSpd : vel.Length();
        Vector2 nv = toTgt.SafeNormalize(Vector2.Zero) * spd;
        if (!string.IsNullOrWhiteSpace(d.SpdFix) && d.SpdFix != "0,0")
            nv += d.SpdFix.GetVector2();
        return ApplyAng(nv, d.AngleCfg, idx, d.Stack);
    }

    /// <summary>解析偏移字符串（格数->像素）</summary>
    private static Vector2 ParseOff(string s) => s.GetVector2();
    #endregion
}