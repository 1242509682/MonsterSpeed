using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using System.Text;

namespace MonsterSpeed;

// 移动模式参数统一封装类
public class MoveModeData
{
    [JsonProperty("行动模式说明", Order = -1)]
    public string Text = "0不启用, 1怪物停留原地, 2环绕模式(0顺时针、1逆时针、2根据交替间隔切换顺时针与逆时针), 3徘徊模式, 4突进模式, 5对视模式,目标锁定:仅对2-5有效)";
    [JsonProperty("模式类型", Order = 0)]
    public MoveMode Mode { get; set; } = MoveMode.None;

    [JsonProperty("平滑系数", Order = 1)]
    public float SmoothFactor { get; set; } = 0.15f;

    [JsonProperty("指示物修改", Order = 3)]
    public Dictionary<string, string[]> MarkerMods { get; set; } = new Dictionary<string, string[]>();

    // 新增：目标锁定参数
    [JsonProperty("目标锁定", Order = 5)]
    public TarLockParams LockParams { get; set; } = new TarLockParams();

    // 环绕模式参数
    [JsonProperty("环绕方向", Order = 8)]
    public OrbitDirection OrbitDir { get; set; } = OrbitDirection.Clockwise;
    [JsonProperty("交替间隔", Order = 9)]
    public float DirTimer { get; set; } = 180f;
    [JsonProperty("环绕半径", Order = 10)]
    public float OrbitRadius { get; set; } = 25f;
    [JsonProperty("环绕速度", Order = 11)]
    public float OrbitSpeed { get; set; } = 2.5f;
    [JsonProperty("环绕移速", Order = 12)]
    public float OrbitMoveSpeed { get; set; } = 15f;

    // 徘徊模式参数
    [JsonProperty("徘徊半径", Order = 20)]
    public float WanderRadius { get; set; } = 30f;
    [JsonProperty("徘徊速度", Order = 21)]
    public float WanderSpeed { get; set; } = 10f;
    [JsonProperty("徘徊间隔", Order = 22)]
    public int WanderChangeInterval { get; set; } = 120;
    [JsonProperty("接近距离", Order = 23)]
    public float WanderCloseDistance { get; set; } = 1f;

    // 突进模式参数
    [JsonProperty("突进速度", Order = 39)]
    public float DashSpeed { get; set; } = 50f;
    [JsonProperty("预备时间", Order = 40)]
    public int DashWindup { get; set; } = 30;
    [JsonProperty("突进时间", Order = 41)]
    public int DashDuration { get; set; } = 20;
    [JsonProperty("突进冷却", Order = 42)]
    public int DashCooldown { get; set; } = 180;
    [JsonProperty("后退距离", Order = 43)]
    public float DashRetreatDistance { get; set; } = 2f;
    [JsonProperty("后退速度系数", Order = 44)]
    public float DashRetreatSpeedFactor { get; set; } = 0.3f;

    // 对视模式参数
    [JsonProperty("对视距离", Order = 50)]
    public float FaceDistance { get; set; } = 30f;
    [JsonProperty("换位距离", Order = 51)]
    public float SwitchDistance { get; set; } = 20f;
    [JsonProperty("对视速度", Order = 52)]
    public float FaceSpeed { get; set; } = 10f;
    [JsonProperty("对视平滑", Order = 53)]
    public float FaceSmooth { get; set; } = 0.1f;
    [JsonProperty("换位概率", Order = 54)]
    public int FaceSwitchChance { get; set; } = 4;

}

// 移动模式状态类
public class MoveModeState
{
    // 环绕模式状态
    public float OrbitAngle { get; set; }
    public Vector2 SmoothVelocity { get; set; }
    public OrbitDirection OrbitDir { get; set; }
    public int OrbitAlternateTimer { get; set; }

    // 徘徊模式状态
    public Vector2 WanderTarget { get; set; }
    public int WanderTimer { get; set; }
    public Vector2 WanderVelocity { get; set; }

    // 突进模式状态
    public DashState DashState { get; set; } = DashState.Windup;
    public int DashTimer { get; set; }
    public Vector2 DashDirection { get; set; } = Vector2.UnitX;
    public Vector2 DashVelocity { get; set; }
    public Vector2 DashStartPosition { get; set; }

    // 对视模式状态
    public int FaceDirection { get; set; }
    public Vector2 FaceVelocity { get; set; }

    // 构造函数初始化向量
    public MoveModeState()
    {
        SmoothVelocity = Vector2.Zero;
        WanderTarget = Vector2.Zero;
        WanderVelocity = Vector2.Zero;
        DashVelocity = Vector2.Zero;
        DashStartPosition = Vector2.Zero;
        FaceVelocity = Vector2.Zero;
    }
}

// 移动模式枚举
public enum MoveMode
{
    None,           // 无特殊移动 0
    Stay,           // 停留原地 1
    Orbit,          // 环绕模式 2
    Wander,         // 随机徘徊 3
    Dash,           // 突进模式 4
    FaceTarget      // 保持对视 5
}

// 环绕方向枚举
public enum OrbitDirection
{
    Clockwise,      // 顺时针
    CounterClockwise, // 逆时针
    Alternate       // 交替方向
}

// 突进状态枚举（简化版）
public enum DashState
{
    Windup,         // 预备阶段
    Dashing,        // 突进阶段
    Cooldown        // 冷却阶段
}

internal class MoveMod
{
    #region 目标获取方法（集成锁定模式）
    private static Vector2 GetTargetPosition(NPC npc, MoveModeData data)
    {
        // 如果配置了锁定模式，使用锁定目标
        if (data.LockParams != null && data.LockParams.LockRange > 0)
        {
            var lockTars = TarLockUtil.GetLockTars(npc, data.LockParams);
            if (lockTars.Count > 0)
            {
                return TarLockUtil.GetTargetPosition(lockTars[0], npc, data.LockParams);
            }
        }

        // 否则使用默认目标
        var defaultTar = npc.GetTargetData(true);
        return defaultTar.Invalid ? npc.Center : defaultTar.Center;
    }
    #endregion

    #region 处理移动模式管理
    public static void HandleMoveMode(NPC npc, TimerData Event, StringBuilder mess, ref bool handled)
    {
        if (Event.MoveData == null || Event.MoveData.Mode == MoveMode.None) return;

        var state = StateUtil.GetState(npc);
        if (state == null) return;

        switch (Event.MoveData.Mode)
        {
            case MoveMode.Stay:
                StayMode(npc, Event, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:停留\n")}");
                handled = true;
                break;
            case MoveMode.Orbit:
                OrbitMode(npc, Event, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:环绕\n")}");
                handled = true;
                break;
            case MoveMode.Wander:
                WanderMode(npc, Event, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:徘徊\n")}");
                handled = true;
                break;
            case MoveMode.Dash:
                DashMode(npc, Event, state, ref handled);
                mess.Append($"{Tool.TextGradient(" 行动模式:突进\n")}");
                break;
            case MoveMode.FaceTarget:
                FaceTargetMode(npc, Event, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:对视\n")}");
                handled = true;
                break;
        }

        // 应用移动模式的指示物修改
        if (Event.MoveData.MarkerMods != null && Event.MoveData.MarkerMods.Count > 0)
        {
            MarkerUtil.SetMarkers(state, Event.MoveData.MarkerMods, ref Main.rand, npc);
        }
    }
    #endregion

    #region 停留原地模式
    private static void StayMode(NPC npc, TimerData Event, NpcState state)
    {
        // 平滑减速到停止
        npc.velocity = Vector2.Lerp(npc.velocity, Vector2.Zero, Event.MoveData.SmoothFactor);
        if (npc.velocity.Length() < 0.1f)
        {
            npc.velocity = Vector2.Zero;
        }
    }
    #endregion

    #region 环绕模式（集成锁定）
    private static void OrbitMode(NPC npc, TimerData Event, NpcState TState)
    {
        var data = Event.MoveData;
        var state = TState.MoveState;

        // 使用锁定目标或默认目标
        Vector2 targetPos = GetTargetPosition(npc, data);

        // 转换为像素距离
        float Radius = data.OrbitRadius * 16f;
        float Speed = data.OrbitSpeed;
        float MoveSpeed = data.OrbitMoveSpeed;

        // 处理交替方向（保持不变）
        if (data.OrbitDir == OrbitDirection.Alternate)
        {
            state.OrbitAlternateTimer++;
            if (state.OrbitAlternateTimer >= data.DirTimer)
            {
                state.OrbitDir = state.OrbitDir == OrbitDirection.Clockwise
                    ? OrbitDirection.CounterClockwise
                    : OrbitDirection.Clockwise;
                state.OrbitAlternateTimer = 0;
            }
        }
        else
        {
            state.OrbitDir = data.OrbitDir;
        }

        // 使用Utils方法计算环绕位置
        Vector2 orbitPos = targetPos + state.OrbitAngle.ToRotationVector2() * Radius;

        // 计算移动方向
        Vector2 orbitDir = (orbitPos - npc.Center).SafeNormalize(Vector2.UnitX);
        Vector2 velocity = orbitDir * MoveSpeed;

        // 平滑速度过渡
        state.SmoothVelocity = Vector2.Lerp(state.SmoothVelocity, velocity, data.SmoothFactor);
        npc.velocity = state.SmoothVelocity;

        // 根据方向更新角度
        float direction = state.OrbitDir == OrbitDirection.Clockwise ? 1f : -1f;
        state.OrbitAngle += direction * Speed * 0.01f;

        // 角度归一化
        if (state.OrbitAngle > MathHelper.TwoPi)
        {
            state.OrbitAngle -= MathHelper.TwoPi;
        }
        else if (state.OrbitAngle < 0)
        {
            state.OrbitAngle += MathHelper.TwoPi;
        }
    }
    #endregion

    #region 随机徘徊模式（集成锁定）
    private static void WanderMode(NPC npc, TimerData Event, NpcState TState)
    {
        var data = Event.MoveData;
        var state = TState.MoveState;

        // 使用锁定目标或默认目标
        Vector2 targetPos = GetTargetPosition(npc, data);

        state.WanderTimer++;

        // 定期更换目标点
        if (state.WanderTimer >= data.WanderChangeInterval ||
            npc.Center.Distance(state.WanderTarget) < (data.WanderCloseDistance * 16f))
        {
            float minDistance = data.WanderRadius * 0.5f * 16f;
            float maxDistance = data.WanderRadius * 16f;
            float distance = minDistance + Main.rand.NextFloat() * (maxDistance - minDistance);
            state.WanderTarget = targetPos + Main.rand.NextVector2Unit() * distance;
            state.WanderTimer = 0;
        }

        // 向目标点移动，带平滑
        Vector2 wanderDir = (state.WanderTarget - npc.Center).SafeNormalize(Vector2.UnitX);
        Vector2 tarVel = wanderDir * data.WanderSpeed;
        state.WanderVelocity = Vector2.Lerp(state.WanderVelocity, tarVel, data.SmoothFactor);
        npc.velocity = state.WanderVelocity;
    }
    #endregion

    #region 突进模式（集成锁定）
    private static void DashMode(NPC npc, TimerData Event, NpcState TState, ref bool handled)
    {
        var data = Event.MoveData;
        var state = TState.MoveState;

        state.DashTimer++;

        // 使用锁定目标或默认目标
        Vector2 targetPos = GetTargetPosition(npc, data);

        switch (state.DashState)
        {
            case DashState.Windup: // 预备阶段
                // 在Windup阶段开始时记录起始位置
                if (state.DashTimer == 1)
                {
                    state.DashStartPosition = npc.Center;
                }

                // 计算后退方向（与目标方向相反）
                Vector2 retreatDir = (npc.Center - targetPos).SafeNormalize(Vector2.UnitX);
                // 使用配置的后退距离（转换为像素）
                float retreatDistance = data.DashRetreatDistance * 16f;
                Vector2 retreatTarget = state.DashStartPosition + retreatDir * retreatDistance;
                // 平滑后退移动，使用配置的后退速度系数
                Vector2 retreatVelocity = (retreatTarget - npc.Center).SafeNormalize(Vector2.UnitX) * data.DashSpeed * data.DashRetreatSpeedFactor;
                state.DashVelocity = Vector2.Lerp(state.DashVelocity, retreatVelocity, data.SmoothFactor);
                // 应用后退速度
                npc.velocity = state.DashVelocity;
                handled = true;

                if (state.DashTimer >= data.DashWindup)
                {
                    state.DashState = DashState.Dashing;
                    state.DashTimer = 0;
                    // 计算突进方向（朝向目标）
                    state.DashDirection = (targetPos - npc.Center).SafeNormalize(Vector2.UnitX);
                }
                break;

            case DashState.Dashing: // 突进阶段
                // 直接向目标方向突进
                Vector2 dashVel = state.DashDirection * data.DashSpeed;
                state.DashVelocity = Vector2.Lerp(state.DashVelocity, dashVel, data.SmoothFactor * 3f);
                npc.velocity = state.DashVelocity;

                handled = true;
                if (state.DashTimer >= data.DashDuration)
                {
                    state.DashState = DashState.Cooldown;
                    state.DashTimer = 0;
                }
                break;

            case DashState.Cooldown: // 冷却阶段
                // 平滑减速到停止
                state.DashVelocity = Vector2.Lerp(state.DashVelocity, Vector2.Zero, data.SmoothFactor);
                npc.velocity = state.DashVelocity;

                handled = true;
                if (state.DashTimer >= data.DashCooldown)
                {
                    // 重置状态，准备下一次突进
                    state.DashState = DashState.Windup;
                    state.DashTimer = 0;
                    state.DashStartPosition = Vector2.Zero;
                }
                break;
        }
    }
    #endregion

    #region 保持对视模式（集成锁定）
    private static void FaceTargetMode(NPC npc, TimerData Event, NpcState TState)
    {
        var data = Event.MoveData;
        var state = TState.MoveState;

        // 使用锁定目标或默认目标
        Vector2 targetPos = GetTargetPosition(npc, data);

        // 计算目标位置（保持在对视距离的八个方向之一），转换为像素距离
        Vector2 facePos = FacePosition(targetPos, state.FaceDirection, data.FaceDistance * 16f);

        // 平滑移动到目标位置，使用自定义速度
        Vector2 moveDir = (facePos - npc.Center).SafeNormalize(Vector2.UnitX);
        Vector2 tarVel = moveDir * data.FaceSpeed;
        state.FaceVelocity = Vector2.Lerp(state.FaceVelocity, tarVel, data.FaceSmooth);
        npc.velocity = state.FaceVelocity;

        // 计算与目标位置的距离
        float ToTarget = npc.Center.Distance(facePos);

        // 如果接近目标位置，考虑切换方向
        if (ToTarget <= (data.SwitchDistance * 16f))
        {
            // 使用配置的换位概率
            if (Main.rand.Next(data.FaceSwitchChance) == 0)
            {
                int oldDirection = state.FaceDirection;
                int newDirection;

                // 确保新方向不是原来的方向
                do
                {
                    newDirection = Main.rand.Next(0, 8);
                } while (newDirection == oldDirection);

                state.FaceDirection = newDirection;
            }
        }
    }

    // 计算八个方向的位置（保持不变）
    private static Vector2 FacePosition(Vector2 Center, int direction, float distance)
    {
        float angle = direction * MathHelper.PiOver4; // 45度间隔
        return Center + angle.ToRotationVector2() * distance;
    }
    #endregion
}