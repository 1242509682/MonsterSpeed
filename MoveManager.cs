using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.DataStructures;
using static MonsterSpeed.Utils;

namespace MonsterSpeed;

/// <summary>行动模式配置数据</summary>
public class MoveData
{
    [JsonProperty("行动模式说明", Order = -1)]
    public string Text = "0不启用, 1怪物停留原地, 2环绕模式(0顺时针、1逆时针、2根据交替间隔切换顺时针与逆时针), 3徘徊模式, 4突进模式, 5对视模式,目标锁定:仅对2-5有效)";
    [JsonProperty("模式类型", Order = 0)]
    public MoveMode Mode { get; set; } = MoveMode.None;
    [JsonProperty("触发条件", Order = 1)]
    public string Cond { get; set; } = "默认配置";
    [JsonProperty("平滑系数", Order = 2)]
    public float Smooth { get; set; } = 0.15f;

    [JsonProperty("环绕方向", Order = 10)]
    public OrbitDir ODir { get; set; } = OrbitDir.Clockwise;
    [JsonProperty("交替间隔", Order = 11)]
    public float DirTimer { get; set; } = 180f;
    [JsonProperty("环绕半径格数", Order = 12)]
    public float ORad { get; set; } = 25f;
    [JsonProperty("环绕速度", Order = 13)]
    public float OSpd { get; set; } = 2.5f;
    [JsonProperty("环绕移速", Order = 14)]
    public float OMove { get; set; } = 15f;

    [JsonProperty("徘徊半径格数", Order = 20)]
    public float WRad { get; set; } = 30f;
    [JsonProperty("徘徊速度", Order = 21)]
    public float WSpd { get; set; } = 10f;
    [JsonProperty("徘徊间隔", Order = 22)]
    public int WInt { get; set; } = 120;
    [JsonProperty("接近距离格数", Order = 23)]
    public float WClose { get; set; } = 1f;

    [JsonProperty("突进速度", Order = 30)]
    public float DSpd { get; set; } = 50f;
    [JsonProperty("预备时间", Order = 31)]
    public int DWind { get; set; } = 30;
    [JsonProperty("突进时间", Order = 32)]
    public int DDur { get; set; } = 20;
    [JsonProperty("突进冷却", Order = 33)]
    public int DCool { get; set; } = 180;
    [JsonProperty("后退距离格数", Order = 34)]
    public float DRet { get; set; } = 2f;
    [JsonProperty("后退速度系数", Order = 35)]
    public float DRetSpd { get; set; } = 0.3f;

    [JsonProperty("对视距离格数", Order = 40)]
    public float FDist { get; set; } = 30f;
    [JsonProperty("换位距离格数", Order = 41)]
    public float FSwitch { get; set; } = 20f;
    [JsonProperty("对视速度", Order = 42)]
    public float FSpd { get; set; } = 10f;
    [JsonProperty("对视平滑", Order = 43)]
    public float FSmooth { get; set; } = 0.1f;
    [JsonProperty("换位概率", Order = 44)]
    public int FChance { get; set; } = 4;
}

/// <summary>行动模式运行时状态</summary>
public class MoveState
{
    public float OAng { get; set; }                  // 环绕模式当前角度（弧度）
    public Vector2 SVel { get; set; }                // 平滑速度（用于环绕/停留等）
    public OrbitDir ODir { get; set; }               // 当前环绕方向
    public int OAltTimer { get; set; }               // 交替方向计时器

    public Vector2 WTarg { get; set; }               // 徘徊目标点
    public int WTimer { get; set; }                  // 徘徊计时
    public Vector2 WVel { get; set; }                // 徘徊速度平滑值

    public DState Dash { get; set; } = DState.Windup; // 突进当前阶段
    public int DTimer { get; set; }                  // 突进计时器
    public Vector2 DDir { get; set; } = Vector2.UnitX; // 突进方向
    public Vector2 DVel { get; set; }                // 突进速度平滑值
    public Vector2 DStart { get; set; }              // 突进起始位置

    public int FDir { get; set; }                    // 对视方向（0~7代表8个方位）
    public Vector2 FVel { get; set; }                // 对视速度平滑值

    public MoveState()
    {
        SVel = Vector2.Zero;
        WTarg = Vector2.Zero;
        WVel = Vector2.Zero;
        DVel = Vector2.Zero;
        DStart = Vector2.Zero;
        FVel = Vector2.Zero;
    }
}

/// <summary>行动模式类型</summary>
public enum MoveMode { None, Stay, Orbit, Wander, Dash, FaceTarget }
/// <summary>环绕方向</summary>
public enum OrbitDir { Clockwise, CounterClockwise, Alternate }
/// <summary>突进阶段</summary>
public enum DState { Windup, Dashing, Cooldown }

/// <summary>行动模式执行器</summary>
internal class MoveManager
{
    #region 主入口
    /// <summary>执行行动模式</summary>
    public static void MoveWork(NPC npc, Configuration.NpcData nd, StringBuilder? msg, string file, ref bool handled)
    {
        // 如果文件名为空，直接返回
        if (string.IsNullOrEmpty(file)) return;

        // 从文件加载行动模式配置数据
        var data = MoveFile.GetData(file);
        if (data == null || data.Mode == MoveMode.None) return;

        // 获取该 NPC 的状态对象
        var st = StateApi.GetState(npc);
        if (st == null) return;

        // 条件检查：如果配置了触发条件，且条件不满足，则不执行模式
        if (!string.IsNullOrEmpty(data.Cond))
        {
            bool ok = true;
            var cond = ConditionFile.GetCondData(data.Cond);
            Conditions.CondWork(nd, npc, cond, ref ok);
            if (!ok) return;
        }

        // 确定是否需要目标（停留模式不需要）
        NPCAimedTarget tar = default;
        if (data.Mode != MoveMode.Stay)
        {
            tar = npc.GetTargetData(false);
            // 如果目标无效，直接返回，避免后续使用无效数据
            if (tar.Invalid) return;
        }

        // 根据模式类型分发到对应的处理方法
        switch (data.Mode)
        {
            case MoveMode.Stay:
                Stay(npc, data);
                handled = true;
                msg?.Append($"{Grad(" 行动模式:停留\n")}");
                break;

            case MoveMode.Orbit:
                Orbit(npc, data, st, tar);
                handled = true;
                msg?.Append($"{Grad(" 行动模式:环绕\n")}");
                break;

            case MoveMode.Wander:
                Wander(npc, data, st, tar);
                handled = true;
                msg?.Append($"{Grad(" 行动模式:徘徊\n")}");
                break;

            case MoveMode.Dash:
                Dash(npc, data, st, tar, ref handled);
                msg?.Append($"{Grad(" 行动模式:突进\n")}");
                break;

            case MoveMode.FaceTarget:
                Face(npc, data, st, tar);
                handled = true;
                msg?.Append($"{Grad(" 行动模式:对视\n")}");
                break;
        }
    }
    #endregion

    #region 辅助方法：预测目标位置
    /// <summary>预测目标在指定帧后的位置（利用 tar.Velocity）</summary>
    private static Vector2 PredictPos(NPCAimedTarget tar, int frames = 30)
    {
        // 如果速度为零或接近零，直接返回当前位置
        if (tar.Velocity.LengthSquared() < 0.01f) return tar.Center;
        // 预测位置 = 当前中心 + 速度 * (帧数 / 60) 秒
        return tar.Center + tar.Velocity * (frames / 60f);
    }
    #endregion

    #region 停留模式
    /// <summary>停留模式：使 NPC 逐渐减速至静止</summary>
    private static void Stay(NPC npc, MoveData d)
    {
        // 使用平滑插值将速度逐步归零
        npc.velocity = Vector2.Lerp(npc.velocity, Vector2.Zero, d.Smooth);
        // 如果速度已经很小，直接置零，避免抖动
        if (npc.velocity.Length() < 0.1f) npc.velocity = Vector2.Zero;
    }
    #endregion

    #region 环绕模式
    /// <summary>环绕模式：围绕目标玩家做圆周运动，加入速度预测</summary>
    private static void Orbit(NPC npc, MoveData d, NpcState ts, NPCAimedTarget tar)
    {
        // 获取移动状态中的环绕子状态
        var st = ts.MoveState;
        // 利用目标速度预测未来位置，使环绕更智能（预测 15 帧）
        Vector2 center = PredictPos(tar, 15);
        // 将半径从格数转为像素
        float rad = d.ORad * 16f;

        // 如果是交替方向模式，处理方向切换
        if (d.ODir == OrbitDir.Alternate)
        {
            st.OAltTimer++; // 计时增加
            // 当计时达到间隔值时，切换方向并重置计时
            if (st.OAltTimer >= d.DirTimer)
            {
                st.ODir = st.ODir == OrbitDir.Clockwise ? OrbitDir.CounterClockwise : OrbitDir.Clockwise;
                st.OAltTimer = 0;
            }
        }
        else
        {
            // 非交替模式，直接使用配置方向
            st.ODir = d.ODir;
        }

        // 计算环绕目标位置：预测中心 + 角度向量 * 半径
        Vector2 pos = center + st.OAng.ToRotationVector2() * rad;
        // 计算从 NPC 当前位置到目标位置的方向向量
        Vector2 dir = (pos - npc.Center).SafeNormalize(Vector2.Zero);
        if (dir == Vector2.Zero) dir = Vector2.UnitX; // 防零向量

        // 期望速度 = 方向 * 环绕移速
        Vector2 vel = dir * d.OMove;
        // 平滑插值到期望速度
        st.SVel = Vector2.Lerp(st.SVel, vel, d.Smooth);
        npc.velocity = st.SVel;

        // 更新角度：根据方向增加或减少角度增量
        float delta = st.ODir == OrbitDir.Clockwise ? 1f : -1f;
        st.OAng += delta * d.OSpd * 0.01f;
        // 归一化角度到 [0, 2π)
        while (st.OAng > MathHelper.TwoPi) st.OAng -= MathHelper.TwoPi;
        while (st.OAng < 0) st.OAng += MathHelper.TwoPi;
    }
    #endregion

    #region 徘徊模式
    /// <summary>徘徊模式：在目标玩家周围随机游走，并受目标速度影响</summary>
    private static void Wander(NPC npc, MoveData d, NpcState ts, NPCAimedTarget tar)
    {
        var st = ts.MoveState;
        // 考虑目标速度，使徘徊中心略微跟随目标移动（系数0.3）
        Vector2 center = tar.Center + tar.Velocity * 0.3f;
        st.WTimer++; // 每帧增加计时

        // 条件：计时达到间隔 或 已经接近当前目标点
        if (st.WTimer >= d.WInt ||
            npc.Center.Distance(st.WTarg) < d.WClose * 16f)
        {
            // 随机生成新的目标点：在玩家周围半径范围内随机选一个点
            float min = d.WRad * 0.5f * 16f;   // 最小半径（像素）
            float max = d.WRad * 16f;          // 最大半径（像素）
            float dis = min + Main.rand.NextFloat() * (max - min);
            // 随机方向 * 距离 + 玩家中心
            st.WTarg = center + Main.rand.NextVector2Unit() * dis;
            st.WTimer = 0; // 重置计时
        }

        // 计算朝向目标点的方向
        Vector2 dir = (st.WTarg - npc.Center).SafeNormalize(Vector2.Zero);
        if (dir == Vector2.Zero) dir = Vector2.UnitX;
        // 期望速度 = 方向 * 徘徊速度
        Vector2 tV = dir * d.WSpd;
        // 平滑过渡到期望速度
        st.WVel = Vector2.Lerp(st.WVel, tV, d.Smooth);
        npc.velocity = st.WVel;
    }
    #endregion

    #region 突进模式
    /// <summary>突进模式：包含蓄力、冲刺、冷却三个阶段，利用目标预测和碰撞箱</summary>
    private static void Dash(NPC npc, MoveData d, NpcState ts, NPCAimedTarget tar, ref bool handled)
    {
        var st = ts.MoveState;
        st.DTimer++; // 当前阶段计时器递增

        switch (st.Dash)
        {
            // ----- 蓄力阶段 -----
            case DState.Windup:
                // 如果是第一次进入蓄力，记录起始位置
                if (st.DTimer == 1) st.DStart = npc.Center;

                // 从目标数据中获取中心坐标
                Vector2 center = tar.Center;
                // 计算后退方向：从目标指向起始位置的方向（即远离目标的方向）
                Vector2 rDir = (st.DStart - center).SafeNormalize(Vector2.Zero);
                if (rDir == Vector2.Zero) rDir = Vector2.UnitX;
                // 计算后退目标点 = 起始位置 + 后退方向 * 后退距离(格转像素)
                Vector2 rTarg = st.DStart + rDir * (d.DRet * 16f);
                // 期望速度 = 指向后退目标点的方向 * 突进速度 * 后退速度系数
                Vector2 v = (rTarg - npc.Center).SafeNormalize(Vector2.Zero) * d.DSpd * d.DRetSpd;
                // 平滑插值
                st.DVel = Vector2.Lerp(st.DVel, v, d.Smooth);
                npc.velocity = st.DVel;
                handled = true; // 标记已处理，阻止原版 AI

                // 如果蓄力时间达到预设值，切换到冲刺阶段
                if (st.DTimer >= d.DWind)
                {
                    st.Dash = DState.Dashing;
                    st.DTimer = 0; // 重置计时器
                    // 计算冲刺方向：利用目标预测位置（预测 20 帧）提高命中率
                    Vector2 predPos = PredictPos(tar, 20);
                    st.DDir = (predPos - npc.Center).SafeNormalize(Vector2.Zero);
                    if (st.DDir == Vector2.Zero) st.DDir = Vector2.UnitX;
                }
                break;

            // ----- 冲刺阶段 -----
            case DState.Dashing:
                // 冲刺速度 = 方向 * 突进速度
                Vector2 dV = st.DDir * d.DSpd;
                // 快速平滑到冲刺速度（系数3倍，使冲刺更迅猛）
                st.DVel = Vector2.Lerp(st.DVel, dV, d.Smooth * 3f);
                npc.velocity = st.DVel;
                handled = true;
                // 如果冲刺时间达到预设值，切换到冷却阶段
                if (st.DTimer >= d.DDur)
                {
                    st.Dash = DState.Cooldown;
                    st.DTimer = 0;
                }
                break;

            // ----- 冷却阶段 -----
            case DState.Cooldown:
                // 逐渐减速至静止
                st.DVel = Vector2.Lerp(st.DVel, Vector2.Zero, d.Smooth);
                npc.velocity = st.DVel;
                handled = true;
                // 冷却时间到，回到蓄力阶段
                if (st.DTimer >= d.DCool)
                {
                    st.Dash = DState.Windup;
                    st.DTimer = 0;
                    st.DStart = Vector2.Zero;
                }
                break;
        }
    }
    #endregion

    #region 对视模式
    /// <summary>对视模式：围绕目标玩家保持一定距离并停留在特定方位，利用碰撞箱调整距离</summary>
    private static void Face(NPC npc, MoveData d, NpcState ts, NPCAimedTarget tar)
    {
        var st = ts.MoveState;
        // 基于目标碰撞箱宽度动态调整对视距离（大目标增加距离）
        float extraDist = tar.Width / 16f * 0.5f; // 额外增加半个宽度的格数
        float effectiveDist = d.FDist + extraDist;

        Vector2 cen = tar.Center;
        // 计算当前朝向位置：玩家中心 + 方位角度向量 * 有效对视距离(格转像素)
        // st.FDir 是0~7的整数，乘以45度得到角度（MathHelper.PiOver4 = 45度）
        Vector2 face = cen + (st.FDir * MathHelper.PiOver4).ToRotationVector2() * (effectiveDist * 16f);

        // 计算指向目标位置的方向
        Vector2 dir = (face - npc.Center).SafeNormalize(Vector2.Zero);
        if (dir == Vector2.Zero) dir = Vector2.UnitX;
        // 期望速度 = 方向 * 对视速度
        Vector2 tV = dir * d.FSpd;
        // 平滑过渡到期望速度
        st.FVel = Vector2.Lerp(st.FVel, tV, d.FSmooth);
        npc.velocity = st.FVel;

        // 如果已经接近目标位置，且随机概率触发，则随机更换一个方位
        if (npc.Center.Distance(face) <= d.FSwitch * 16f && Main.rand.Next(d.FChance) == 0)
        {
            int old = st.FDir;
            int nDir;
            // 随机生成新的方位，直到与旧方位不同
            do { nDir = Main.rand.Next(0, 8); } while (nDir == old);
            st.FDir = nDir;
        }
    }
    #endregion
}