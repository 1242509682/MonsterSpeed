using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

#region 更新弹幕数据
/// <summary>
/// 弹幕更新配置（每个阶段）
/// </summary>
public class UpdateProjData
{
    [JsonProperty("新弹幕ID(-1恢复,0不更新,>0替换)", Order = -2)]
    public int NewType = 0;
    [JsonProperty("更新间隔/毫秒", Order = -2)]
    public double UpdInterval = 500f;
    [JsonProperty("延长持续时间", Order = -1)]
    public int ExtraTime = 0;
    [JsonProperty("触发条件", Order = 1)]
    public string Condition { get; set; } = "默认配置";
    [JsonProperty("速度", Order = 2)]
    public float Velocity = 0f;
    [JsonProperty("速度向量XY/格", Order = 3)]
    public string VelXY { get; set; } = "0,0";
    [JsonProperty("半径格数", Order = 4)]
    public float Radius = 0f;
    [JsonProperty("位置偏移XY/格", Order = 5)]
    public string PosOffXY { get; set; } = "0,0";
    [JsonProperty("偏移角度", Order = 6)]
    public float Angle = 0f;
    [JsonProperty("旋转角度", Order = 7)]
    public float Rotate = 0f;

    [JsonProperty("追踪模式", Order = 20)]
    public HomingData HomingMode { get; set; } = new HomingData();

    [JsonProperty("弹幕AI", Order = 50)]
    public Dictionary<int, float> AI { get; set; } = new();
    [JsonProperty("速度注入AI", Order = 51)]
    public Dictionary<int, int> SpdToAI { get; set; } = new();
    [JsonProperty("注入后速度向量XY/格", Order = 52)]
    public string SpdToAIVel { get; set; } = "0,0";
    [JsonProperty("指示物修改", Order = 54)]
    public Dictionary<string, string[]> MarkerMods { get; set; } = new();
    [JsonProperty("指示物注入AI", Order = 55)]
    public Dictionary<int, string> MarkerToAI { get; set; } = new();
    [JsonProperty("弹点召怪ID", Order = 120)]
    public int[] SpawnNPCs { get; set; } = new int[0];
    [JsonProperty("弹点召怪数量", Order = 121)]
    public int SpawnCnt { get; set; } = 1;

    /// <summary>将字符串转为像素向量</summary>
    private Vector2 ParseVec(string s) => s.GetVector2();

    /// <summary>获取最终速度（优先向量，否则标量）</summary>
    public Vector2 GetFinalVel(Projectile proj)
    {
        Vector2 v = ParseVec(VelXY);
        if (v != Vector2.Zero) return v;
        if (Velocity != 0f)
            return proj.velocity.SafeNormalize(Vector2.Zero) * Velocity;
        return proj.velocity;
    }

    /// <summary>获取位置偏移向量</summary>
    public Vector2 GetPosOff() => ParseVec(PosOffXY);

    /// <summary>获取速度注入向量</summary>
    public Vector2 GetSpdToAIVec() => ParseVec(SpdToAIVel);

    /// <summary>将速度分量注入到AI字段</summary>
    public void ApplySpdToAI(Projectile proj, Vector2 vel, List<int> updList, int pid)
    {
        if (SpdToAI == null || SpdToAI.Count == 0) return;
        foreach (var kv in SpdToAI)
        {
            int idx = kv.Key;
            if (idx < 0 || idx >= proj.ai.Length) continue;
            float val = kv.Value switch
            {
                0 => (float)Math.Atan2(vel.Y, vel.X),
                1 => vel.Length(),
                2 => vel.X,
                3 => vel.Y,
                _ => 0f
            };
            proj.ai[idx] = val;
            if (!updList.Contains(pid)) updList.Add(pid);
        }
    }
}
#endregion

#region 更新弹幕状态
/// <summary>
/// 存储每个弹幕的更新状态（用于多阶段更新）
/// </summary>
public class UpdProjState
{
    public string Notes { get; set; }
    public int Index { get; set; }      // 弹幕索引
    public int Who { get; set; }        // 所属怪物ID
    public int Type { get; set; }       // 原始类型
    public int NewType { get; set; }    // 当前类型
    public int UpdIdx { get; set; } = 0; // 当前更新阶段索引

    // 模式标志和参数（用于智能半径偏移）
    public string Flag { get; set; } = ""; // 模式标志
    public float CirAng { get; set; } = 0f;   // 圆形模式中的角度
    public int CirIdx { get; set; } = 0;      // 圆形模式中的索引
    public Vector2 BasePos { get; set; }  // 原始位置（用于半径偏移）
    public Vector2 BaseVel { get; set; }  // 原始速度（用于扇形模式）
    public float CirRad { get; set; } = 0f;  // 圆形半径
    public string LineOff { get; set; } = "0,0"; // 线性偏移字符串
    public float SprInc { get; set; } = 0f; // 扇形角度增量

    public UpdProjState(int idx, int who, int type,
                        string flag = "", float ang = 0f, int cirIdx = 0,
                        Vector2? basePos = null, Vector2? baseVel = null,
                        float cirRad = 0f, string lineOff = "0,0", float sprInc = 0f)
    {
        Index = idx;
        Who = who;
        Type = type;
        NewType = type;
        Flag = flag;
        CirAng = ang;
        CirIdx = cirIdx;
        BasePos = basePos ?? Vector2.Zero;
        BaseVel = baseVel ?? Vector2.Zero;
        CirRad = cirRad;
        LineOff = lineOff;
        SprInc = sprInc;
    }
}
#endregion

/// <summary>
/// 弹幕更新管理类（支持多阶段更新、追踪、偏移等）
/// </summary>
public class UpProj
{
    public static Dictionary<int, DateTime> UpdateTimer = new(); //用于追踪更新弹幕的时间
    public static UpdProjState?[] UpdateState = new UpdProjState[Main.maxProjectiles];

    #region 检查所有更新弹幕
    /// <summary>
    /// 遍历所有活跃的更新弹幕，按时间触发更新
    /// </summary>
    public static void ChkUpdates(NpcData set, NPC npc, List<SpawnProjData> datas)
    {
        if (npc == null || !npc.active || datas == null || UpdateState == null) return;
        var st = StateApi.GetState(npc);
        if (st == null || st.SendProjIdx < 0 || st.SendProjIdx >= datas.Count) return;
        var data = datas[st.SendProjIdx];
        if (data?.UpdProj == null || data.UpdProj.Count == 0) return;

        // 修复：循环包含索引0
        for (int i = UpdateState.Length - 1; i >= 0; i--)
        {
            var ups = UpdateState[i];
            if (ups == null) continue;
            if (ups.Who != npc.whoAmI || ups.Index < 0 || ups.Index >= Main.maxProjectiles)
            { Remove(ups.Index); continue; }

            Projectile p = Main.projectile[ups.Index];
            if (p == null || !p.active || p.owner != Main.myPlayer || p.type != ups.NewType)
            { Remove(ups.Index); continue; }

            // 加载所有更新阶段配置
            var updList = new List<UpdateProjData>();
            foreach (string f in data.UpdProj)
            {
                var fdata = UpProjFile.GetData(f);
                if (fdata != null && fdata.Count > 0) updList.AddRange(fdata);
            }
            if (updList.Count == 0) continue;
            if (ups.UpdIdx < 0 || ups.UpdIdx >= updList.Count)
            { Remove(ups.Index); continue; }

            var cur = updList[ups.UpdIdx];
            // 条件检查
            if (!string.IsNullOrEmpty(cur.Condition))
            {
                bool ok = true;
                var cond = ConditionFile.GetCondData(cur.Condition);
                Conditions.Condition(npc, new StringBuilder(), set, cond, ref ok);
                if (!ok) continue;
            }

            // 间隔检查
            if (UpdateTimer.TryGetValue(ups.Index, out DateTime last) &&
                (DateTime.UtcNow - last).TotalMilliseconds >= cur.UpdInterval)
            {
                UpdSingle(npc, st, ups, cur, p);
                UpdateTimer[ups.Index] = DateTime.UtcNow;
            }
        }
    }
    #endregion

    #region 更新单个弹幕
    /// <summary>
    /// 执行单个弹幕的更新逻辑（速度、位置、AI、模式偏移等）
    /// </summary>
    private static void UpdSingle(NPC npc, NpcState st, UpdProjState ups, UpdateProjData cur, Projectile p)
    {
        if (npc == null || !npc.active || st == null) return;
        var updList = new List<int>();

        // 取消友善标记
        if (p.friendly) { p.friendly = false; Add(updList, ups.Index); }

        // 指示物注入AI
        if (cur.MarkerToAI != null && cur.MarkerToAI.Count > 0)
        { MarkerUtil.InjectToAI(st, cur.MarkerToAI, p); Add(updList, ups.Index); }

        // 类型转换
        if (cur.NewType != 0)
        {
            int nt = cur.NewType == -1 ? ups.Type : cur.NewType;
            if (nt > 0 && nt != ups.NewType) { p.type = nt; ups.NewType = nt; Add(updList, ups.Index); }
        }

        // 延长持续时间
        if (cur.ExtraTime != 0) { p.timeLeft += cur.ExtraTime; Add(updList, ups.Index); }

        // 速度计算
        Vector2 newVel = cur.GetFinalVel(p);
        // 追踪模式
        if (cur.HomingMode != null && cur.HomingMode.Homing)
            newVel = AutoHoming.ApplyAll(newVel, p, cur.HomingMode, npc, updList);

        // 角度旋转
        if (cur.Angle != 0f || cur.Rotate != 0f)
        {
            double ang = cur.Angle * Math.PI / 180;
            if (cur.Rotate != 0f) ang += cur.Rotate * Math.PI / 180;
            newVel = newVel.RotatedBy(ang);
            Add(updList, ups.Index);
        }

        // 位置偏移
        Vector2 off = cur.GetPosOff();
        if (off != Vector2.Zero)
        {
            Vector2 np = p.position + off;
            if ((np - p.position).LengthSquared() > 0.001f)
            { p.position = np; Add(updList, ups.Index); }
        }

        #region 智能半径偏移（利用模式标志）
        if (cur.Radius != 0f)
        {
            Vector2 delta = Vector2.Zero;
            float incRad = cur.Radius * 16f;
            switch (ups.Flag)
            {
                case "circle":
                    if (ups.BasePos != Vector2.Zero)
                    {
                        float totalRad = ups.CirRad + incRad;   // ups.CirRad 已是像素
                        float ang = ups.CirAng + st.SPCount * 0.1f;
                        delta = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * totalRad;
                        p.position = ups.BasePos + delta;
                    }
                    break;
                case "cir_spr":
                    if (ups.BasePos != Vector2.Zero && ups.BaseVel != Vector2.Zero)
                    {
                        float ba = (float)Math.Atan2(ups.BaseVel.Y, ups.BaseVel.X);
                        float ang = ba + ups.CirIdx * MathHelper.ToRadians(ups.SprInc);
                        Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                        delta = dir * incRad;
                        p.position = ups.BasePos + delta;
                    }
                    break;
                case "nest":
                case "line":
                    if (ups.BasePos != Vector2.Zero && ups.BaseVel != Vector2.Zero)
                    {
                        Vector2 dir = ups.LineOff.GetVector2().SafeNormalize(Vector2.Zero);
                        if (dir == Vector2.Zero) dir = ups.BaseVel.SafeNormalize(Vector2.Zero);
                        delta = dir * incRad * ups.CirIdx;
                        p.position = ups.BasePos + delta;
                    }
                    break;
                case "spread":
                    if (ups.BasePos != Vector2.Zero && ups.BaseVel != Vector2.Zero)
                    {
                        float ba = (float)Math.Atan2(ups.BaseVel.Y, ups.BaseVel.X);
                        delta = new Vector2((float)Math.Cos(ba), (float)Math.Sin(ba)) * incRad;
                        p.position += delta;
                    }
                    break;
                case "circle_line":
                    if (ups.BasePos != Vector2.Zero && ups.BaseVel != Vector2.Zero)
                    {
                        float ang = ups.CirAng + st.SPCount * 0.1f;
                        Vector2 cirOff = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * incRad;
                        Vector2 dir = ups.LineOff.GetVector2().SafeNormalize(Vector2.Zero);
                        if (dir == Vector2.Zero) dir = ups.BaseVel.SafeNormalize(Vector2.Zero);
                        Vector2 lineOff = dir * incRad * ups.CirIdx * 0.5f;
                        p.position = ups.BasePos + cirOff + lineOff;
                    }
                    break;
                case "spread_line":
                    if (ups.BasePos != Vector2.Zero && ups.BaseVel != Vector2.Zero)
                    {
                        float ba = (float)Math.Atan2(ups.BaseVel.Y, ups.BaseVel.X);
                        float sa = ba + ups.CirIdx * MathHelper.ToRadians(ups.SprInc);
                        Vector2 spdDir = new Vector2((float)Math.Cos(sa), (float)Math.Sin(sa));
                        Vector2 spdOff = spdDir * incRad;
                        Vector2 lineDir = new Vector2(-spdDir.Y, spdDir.X);
                        Vector2 lineOff = lineDir * incRad * ups.CirIdx * 0.5f;
                        p.position = ups.BasePos + spdOff + lineOff;
                    }
                    break;
                default:
                    float rndAng = (p.identity * 137.508f + st.SPCount * 0.5f) % 360f;
                    delta = new Vector2((float)Math.Cos(MathHelper.ToRadians(rndAng)),
                                        (float)Math.Sin(MathHelper.ToRadians(rndAng))) * incRad;
                    p.position += delta;
                    break;
            }
            if (delta != Vector2.Zero) Add(updList, ups.Index);
        }
        #endregion

        // 应用新速度
        if ((newVel - p.velocity).LengthSquared() > 0.001f)
        { p.velocity = newVel; Add(updList, ups.Index); }

        // 直接设置AI值
        if (cur.AI.Count > 0)
        {
            foreach (var kv in cur.AI)
                if (kv.Key >= 0 && kv.Key < p.ai.Length)
                { p.ai[kv.Key] = kv.Value; Add(updList, ups.Index); }
        }

        // 速度注入AI
        if (cur.SpdToAI != null && cur.SpdToAI.Count > 0)
        {
            cur.ApplySpdToAI(p, newVel, updList, ups.Index);
            Vector2 sv = cur.GetSpdToAIVec();
            if (sv != Vector2.Zero) { p.velocity = sv; Add(updList, ups.Index); }
        }

        // 进入下一阶段
        ups.UpdIdx++;
        SendUpd(updList);

        // 弹点召唤怪物
        if (cur.SpawnNPCs != null && cur.SpawnNPCs.Length > 0 && cur.SpawnCnt > 0)
        {
            foreach (int id in cur.SpawnNPCs)
                SpawnNPC(st, npc, p.Center, id, cur.SpawnCnt);
        }
    }
    #endregion

    #region 辅助方法
    /// <summary>添加弹幕ID到待同步列表</summary>
    public static void Add(List<int> list, int pid)
    {
        if (pid >= 0 && pid < Main.maxProjectiles && !list.Contains(pid))
            list.Add(pid);
    }

    /// <summary>获取弹幕的更新状态</summary>
    public static UpdProjState? GetState(int pid) => (pid >= 0 && pid < Main.maxProjectiles) ? UpdateState[pid] : null;

    /// <summary>清除某个怪物关联的所有弹幕状态</summary>
    public static void ClearStates(int who)
    {
        for (int i = 0; i < UpdateState.Length; i++)
            if (UpdateState[i] != null && UpdateState[i]?.Who == who)
            { UpdateState[i] = null; UpdateTimer.Remove(i); }
    }

    /// <summary>移除单个弹幕的更新状态</summary>
    public static void Remove(int idx)
    {
        if (idx >= 0 && idx < Main.maxProjectiles)
        { UpdateState[idx] = null; UpdateTimer.Remove(idx); }
    }

    /// <summary>添加新的弹幕更新状态（在弹幕生成时调用）</summary>
    public static bool AddState(int pid, int who, int type,
                                string flag = "", float ang = 0f, int cirIdx = 0,
                                Vector2? basePos = null, Vector2? baseVel = null,
                                float cirRad = 0f, string lineOff = "0,0", float sprInc = 0f)
    {
        if (pid < 0 || pid >= Main.maxProjectiles) return false;
        try
        {
            UpdateState[pid] = new UpdProjState(pid, who, type, flag, ang, cirIdx,
                                             basePos, baseVel, cirRad, lineOff, sprInc);
            UpdateTimer[pid] = DateTime.UtcNow;
            return true;
        }
        catch { return false; }
    }

    /// <summary>发送弹幕更新数据包给所有玩家</summary>
    private static void SendUpd(List<int> list)
    {
        foreach (int pid in list)
            if (pid >= 0 && pid < Main.maxProjectiles && Main.projectile[pid]?.active == true)
                TSPlayer.All.SendData(PacketTypes.ProjectileNew, null, pid, 0f, 0f, 0f, 0);
    }

    /// <summary>在弹幕位置生成怪物</summary>
    private static void SpawnNPC(NpcState st, NPC npc, Vector2 pos, int id, int cnt)
    {
        if (id <= 0 || id >= Terraria.ID.NPCID.Count || id == 113 || id == 488) return;
        int curCnt = Main.npc.Count(n => n.active && n.type == id);
        if (curCnt >= cnt) return;
        for (int i = 0; i < cnt; i++)
        {
            int idx = NPC.NewNPC(npc.GetSpawnSourceForNPCFromNPCAI(), (int)pos.X, (int)pos.Y, id);
            if (idx >= 0 && idx < Main.maxNPCs)
            { Main.npc[idx].active = true; Main.npc[idx].netUpdate = true; }
        }
        st.SNCount++;
    }
    #endregion
}