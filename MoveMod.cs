using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;

namespace MonsterSpeed;

public class MoveModeData
{
    [JsonProperty("行动模式说明", Order = -1)]
    public string Text = "0不启用, 1怪物停留原地, 2环绕模式(0顺时针、1逆时针、2根据交替间隔切换顺时针与逆时针), 3徘徊模式, 4突进模式, 5对视模式,目标锁定:仅对2-5有效)";
    [JsonProperty("模式类型", Order = 0)]
    public MoveMode Mode { get; set; } = MoveMode.None;
    [JsonProperty("触发条件", Order = 1)]
    public string Condition { get; set; } = "默认配置";
    [JsonProperty("平滑系数", Order = 2)]
    public float SmoothFactor { get; set; } = 0.15f;

    [JsonProperty("环绕方向", Order = 10)]
    public OrbitDirection OrbitDir { get; set; } = OrbitDirection.Clockwise;
    [JsonProperty("交替间隔", Order = 11)]
    public float DirTimer { get; set; } = 180f;
    [JsonProperty("环绕半径格数", Order = 12)]
    public float OrbitRadius { get; set; } = 25f;
    [JsonProperty("环绕速度", Order = 13)]
    public float OrbitSpeed { get; set; } = 2.5f;
    [JsonProperty("环绕移速", Order = 14)]
    public float OrbitMoveSpeed { get; set; } = 15f;

    [JsonProperty("徘徊半径格数", Order = 20)]
    public float WanderRadius { get; set; } = 30f;
    [JsonProperty("徘徊速度", Order = 21)]
    public float WanderSpeed { get; set; } = 10f;
    [JsonProperty("徘徊间隔", Order = 22)]
    public int WanderChangeInterval { get; set; } = 120;
    [JsonProperty("接近距离格数", Order = 23)]
    public float WanderCloseDistance { get; set; } = 1f;

    [JsonProperty("突进速度", Order = 30)]
    public float DashSpeed { get; set; } = 50f;
    [JsonProperty("预备时间", Order = 31)]
    public int DashWindup { get; set; } = 30;
    [JsonProperty("突进时间", Order = 32)]
    public int DashDuration { get; set; } = 20;
    [JsonProperty("突进冷却", Order = 33)]
    public int DashCooldown { get; set; } = 180;
    [JsonProperty("后退距离格数", Order = 34)]
    public float DashRetreatDistance { get; set; } = 2f;
    [JsonProperty("后退速度系数", Order = 35)]
    public float DashRetreatSpeedFactor { get; set; } = 0.3f;

    [JsonProperty("对视距离格数", Order = 40)]
    public float FaceDistance { get; set; } = 30f;
    [JsonProperty("换位距离格数", Order = 41)]
    public float SwitchDistance { get; set; } = 20f;
    [JsonProperty("对视速度", Order = 42)]
    public float FaceSpeed { get; set; } = 10f;
    [JsonProperty("对视平滑", Order = 43)]
    public float FaceSmooth { get; set; } = 0.1f;
    [JsonProperty("换位概率", Order = 44)]
    public int FaceSwitchChance { get; set; } = 4;
}

public class MoveModeState
{
    public float OrbitAngle { get; set; }
    public Vector2 SmoothVel { get; set; }
    public OrbitDirection OrbitDir { get; set; }
    public int OrbitAltTimer { get; set; }

    public Vector2 WanderTarget { get; set; }
    public int WanderTimer { get; set; }
    public Vector2 WanderVel { get; set; }

    public DashState DashState { get; set; } = DashState.Windup;
    public int DashTimer { get; set; }
    public Vector2 DashDir { get; set; } = Vector2.UnitX;
    public Vector2 DashVel { get; set; }
    public Vector2 DashStart { get; set; }

    public int FaceDir { get; set; }
    public Vector2 FaceVel { get; set; }

    public MoveModeState()
    {
        SmoothVel = Vector2.Zero;
        WanderTarget = Vector2.Zero;
        WanderVel = Vector2.Zero;
        DashVel = Vector2.Zero;
        DashStart = Vector2.Zero;
        FaceVel = Vector2.Zero;
    }
}

public enum MoveMode { None, Stay, Orbit, Wander, Dash, FaceTarget }
public enum OrbitDirection { Clockwise, CounterClockwise, Alternate }
public enum DashState { Windup, Dashing, Cooldown }

internal class MoveMod
{
    #region 行动模式管理
    public static void MoveModes(NPC npc, Configuration.NpcData nd, StringBuilder? msg, string file, ref bool handled)
    {
        if (string.IsNullOrEmpty(file)) return;
        var data = MoveFile.GetData(file);
        if (data == null || data.Mode == MoveMode.None) return;
        var st = StateApi.GetState(npc);
        if (st == null) return;

        if (!string.IsNullOrEmpty(data.Condition))
        {
            bool ok = true;
            var cond = ConditionFile.GetCondData(data.Condition);
            Conditions.Condition(nd, npc, cond, ref ok);
            if (!ok) return;
        }

        switch (data.Mode)
        {
            case MoveMode.Stay: StayMode(npc, data); handled = true; msg?.Append($"{Tool.TextGradient(" 行动模式:停留\n")}"); break;
            case MoveMode.Orbit: OrbitMode(npc, data, st); handled = true; msg?.Append($"{Tool.TextGradient(" 行动模式:环绕\n")}"); break;
            case MoveMode.Wander: WanderMode(npc, data, st); handled = true; msg?.Append($"{Tool.TextGradient(" 行动模式:徘徊\n")}"); break;
            case MoveMode.Dash: DashMode(npc, data, st, ref handled); msg?.Append($"{Tool.TextGradient(" 行动模式:突进\n")}"); break;
            case MoveMode.FaceTarget: FaceMode(npc, data, st); handled = true; msg?.Append($"{Tool.TextGradient(" 行动模式:对视\n")}"); break;
        }
    }
    #endregion

    #region 停留
    private static void StayMode(NPC npc, MoveModeData d)
    {
        npc.velocity = Vector2.Lerp(npc.velocity, Vector2.Zero, d.SmoothFactor);
        if (npc.velocity.Length() < 0.1f) npc.velocity = Vector2.Zero;
    }
    #endregion

    #region 环绕
    private static void OrbitMode(NPC npc, MoveModeData d, NpcState ts)
    {
        var st = ts.MoveState;
        Vector2 tar = Main.player[npc.target].Center;
        float radPx = d.OrbitRadius * 16f;
        float spd = d.OrbitSpeed;
        float mvSpd = d.OrbitMoveSpeed;

        if (d.OrbitDir == OrbitDirection.Alternate)
        {
            st.OrbitAltTimer++;
            if (st.OrbitAltTimer >= d.DirTimer)
            {
                st.OrbitDir = st.OrbitDir == OrbitDirection.Clockwise ? OrbitDirection.CounterClockwise : OrbitDirection.Clockwise;
                st.OrbitAltTimer = 0;
            }
        }
        else st.OrbitDir = d.OrbitDir;

        Vector2 orbitPos = tar + st.OrbitAngle.ToRotationVector2() * radPx;
        Vector2 dir = (orbitPos - npc.Center).SafeNormalize(Vector2.Zero);
        if (dir == Vector2.Zero) dir = Vector2.UnitX;
        Vector2 vel = dir * mvSpd;
        st.SmoothVel = Vector2.Lerp(st.SmoothVel, vel, d.SmoothFactor);
        npc.velocity = st.SmoothVel;

        float delta = st.OrbitDir == OrbitDirection.Clockwise ? 1f : -1f;
        st.OrbitAngle += delta * spd * 0.01f;
        st.OrbitAngle = NormAng(st.OrbitAngle);
    }
    #endregion

    #region 徘徊
    private static void WanderMode(NPC npc, MoveModeData d, NpcState ts)
    {
        var st = ts.MoveState;
        Vector2 tar = Main.player[npc.target].Center;
        st.WanderTimer++;

        if (st.WanderTimer >= d.WanderChangeInterval ||
            Vector2.Distance(st.WanderTarget, npc.Center) < d.WanderCloseDistance * 16f)
        {
            float min = d.WanderRadius * 0.5f * 16f;
            float max = d.WanderRadius * 16f;
            float dis = min + Main.rand.NextFloat() * (max - min);
            st.WanderTarget = tar + Main.rand.NextVector2Unit() * dis;
            st.WanderTimer = 0;
        }

        Vector2 dir = (st.WanderTarget - npc.Center).SafeNormalize(Vector2.Zero);
        if (dir == Vector2.Zero) dir = Vector2.UnitX;
        Vector2 tarVel = dir * d.WanderSpeed;
        st.WanderVel = Vector2.Lerp(st.WanderVel, tarVel, d.SmoothFactor);
        npc.velocity = st.WanderVel;
    }
    #endregion

    #region 突进
    private static void DashMode(NPC npc, MoveModeData d, NpcState ts, ref bool handled)
    {
        var st = ts.MoveState;
        st.DashTimer++;

        switch (st.DashState)
        {
            case DashState.Windup:
                if (st.DashTimer == 1) st.DashStart = npc.Center;
                Vector2 tar = Main.player[npc.target].Center;
                Vector2 retreatDir = (st.DashStart - tar).SafeNormalize(Vector2.Zero);
                if (retreatDir == Vector2.Zero) retreatDir = Vector2.UnitX;
                Vector2 retreatTarget = st.DashStart + retreatDir * (d.DashRetreatDistance * 16f);
                Vector2 vel = (retreatTarget - npc.Center).SafeNormalize(Vector2.Zero) * d.DashSpeed * d.DashRetreatSpeedFactor;
                st.DashVel = Vector2.Lerp(st.DashVel, vel, d.SmoothFactor);
                npc.velocity = st.DashVel;
                handled = true;

                if (st.DashTimer >= d.DashWindup)
                {
                    st.DashState = DashState.Dashing;
                    st.DashTimer = 0;
                    st.DashDir = (tar - npc.Center).SafeNormalize(Vector2.Zero);
                    if (st.DashDir == Vector2.Zero) st.DashDir = Vector2.UnitX;
                }
                break;

            case DashState.Dashing:
                Vector2 dashVel = st.DashDir * d.DashSpeed;
                st.DashVel = Vector2.Lerp(st.DashVel, dashVel, d.SmoothFactor * 3f);
                npc.velocity = st.DashVel;
                handled = true;
                if (st.DashTimer >= d.DashDuration)
                { st.DashState = DashState.Cooldown; st.DashTimer = 0; }
                break;

            case DashState.Cooldown:
                st.DashVel = Vector2.Lerp(st.DashVel, Vector2.Zero, d.SmoothFactor);
                npc.velocity = st.DashVel;
                handled = true;
                if (st.DashTimer >= d.DashCooldown)
                { st.DashState = DashState.Windup; st.DashTimer = 0; st.DashStart = Vector2.Zero; }
                break;
        }
    }
    #endregion

    #region 对视
    private static void FaceMode(NPC npc, MoveModeData d, NpcState ts)
    {
        var st = ts.MoveState;
        Vector2 tar = Main.player[npc.target].Center;
        Vector2 facePos = CalcFacePos(tar, st.FaceDir, d.FaceDistance);
        Vector2 dir = (facePos - npc.Center).SafeNormalize(Vector2.Zero);
        if (dir == Vector2.Zero) dir = Vector2.UnitX;
        Vector2 tarVel = dir * d.FaceSpeed;
        st.FaceVel = Vector2.Lerp(st.FaceVel, tarVel, d.FaceSmooth);
        npc.velocity = st.FaceVel;

        if (Vector2.Distance(facePos, npc.Center) <= d.SwitchDistance * 16f && Main.rand.Next(d.FaceSwitchChance) == 0)
        {
            int old = st.FaceDir;
            int newDir;
            do { newDir = Main.rand.Next(0, 8); } while (newDir == old);
            st.FaceDir = newDir;
        }
    }

    private static Vector2 CalcFacePos(Vector2 center, int dir, float dist)
        => center + (dir * MathHelper.PiOver4).ToRotationVector2() * (dist * 16f);
    #endregion

    private static float NormAng(float a)
    {
        while (a > MathHelper.TwoPi) a -= MathHelper.TwoPi;
        while (a < 0) a += MathHelper.TwoPi;
        return a;
    }
}