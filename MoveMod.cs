using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;

namespace MonsterSpeed;

// 移动模式参数统一封装类
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

    // 环绕模式参数
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

    // 徘徊模式参数
    [JsonProperty("徘徊半径格数", Order = 20)]
    public float WanderRadius { get; set; } = 30f;
    [JsonProperty("徘徊速度", Order = 21)]
    public float WanderSpeed { get; set; } = 10f;
    [JsonProperty("徘徊间隔", Order = 22)]
    public int WanderChangeInterval { get; set; } = 120;
    [JsonProperty("接近距离格数", Order = 23)]
    public float WanderCloseDistance { get; set; } = 1f;

    // 突进模式参数
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

    // 对视模式参数
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

// 突进状态枚举
public enum DashState
{
    Windup,         // 预备阶段
    Dashing,        // 突进阶段
    Cooldown        // 冷却阶段
}

internal class MoveMod
{
    #region 处理行动模式管理
    public static void MoveModes(NPC npc, Configuration.NpcData NpcData, StringBuilder mess, string FileName, ref bool handled)
    {
        if (string.IsNullOrEmpty(FileName)) return;

        var data = MoveFile.GetData(FileName);
        if (data == null || data.Mode == MoveMode.None) return;

        var state = StateApi.GetState(npc);
        if (state == null) return;

        // 条件检查
        if (!CheckCond(npc, NpcData, data)) return;

        // 执行对应的行动模式
        switch (data.Mode)
        {
            case MoveMode.Stay:
                StayMode(npc, data, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:停留\n")}");
                handled = true;
                break;
            case MoveMode.Orbit:
                OrbitMode(npc, data, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:环绕\n")}");
                handled = true;
                break;
            case MoveMode.Wander:
                WanderMode(npc, data, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:徘徊\n")}");
                handled = true;
                break;
            case MoveMode.Dash:
                DashMode(npc, data, state, ref handled);
                mess.Append($"{Tool.TextGradient(" 行动模式:突进\n")}");
                break;
            case MoveMode.FaceTarget:
                FaceTargetMode(npc, data, state);
                mess.Append($"{Tool.TextGradient(" 行动模式:对视\n")}");
                handled = true;
                break;
        }
    }

    // 条件检查封装
    private static bool CheckCond(NPC npc, Configuration.NpcData data, MoveModeData Event)
    {
        if (string.IsNullOrEmpty(Event.Condition))
            return true;

        bool all = true;
        var cond = ConditionFile.GetCondData(Event.Condition);
        Conditions.Condition(npc, new StringBuilder(), data, cond, ref all);
        return all;
    }
    #endregion

    #region 停留原地模式
    private static void StayMode(NPC npc, MoveModeData data, NpcState state)
    {
        // 使用PxUtil的平滑插值
        npc.velocity = Vector2.Lerp(npc.velocity, Vector2.Zero, data.SmoothFactor);
        
        // 使用PxUtil的长度检查
        if (npc.velocity.Length() < 0.1f)
        {
            npc.velocity = Vector2.Zero;
        }
    }
    #endregion

    #region 环绕模式
    private static void OrbitMode(NPC npc, MoveModeData data, NpcState TState)
    {
        var state = TState.MoveState;

        // 使用锁定目标
        Vector2 pos = Main.player[npc.target].position;

        // 转换为像素距离 - 使用PxUtil
        float radiusPx = PxUtil.ToPx(data.OrbitRadius);
        float speed = data.OrbitSpeed;
        float moveSpeed = data.OrbitMoveSpeed;

        // 处理环绕方向
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
        Vector2 orbitPos = pos + state.OrbitAngle.ToRotationVector2() * radiusPx;

        // 计算移动方向 - 使用PxUtil的安全方向计算
        Vector2 dir = PxUtil.SafeDirectionTo(npc.Center, orbitPos, Vector2.UnitX);
        Vector2 vel = dir * moveSpeed;

        // 平滑速度过渡
        state.SmoothVelocity = Vector2.Lerp(state.SmoothVelocity, vel, data.SmoothFactor);
        npc.velocity = state.SmoothVelocity;

        // 根据方向更新角度
        float direction = state.OrbitDir == OrbitDirection.Clockwise ? 1f : -1f;
        state.OrbitAngle += direction * speed * 0.01f;

        // 角度归一化
        state.OrbitAngle = NormalizeAngle(state.OrbitAngle);
    }

    // 角度归一化
    private static float NormalizeAngle(float angle)
    {
        while (angle > MathHelper.TwoPi) angle -= MathHelper.TwoPi;
        while (angle < 0) angle += MathHelper.TwoPi;
        return angle;
    }
    #endregion

    #region 随机徘徊模式
    private static void WanderMode(NPC npc, MoveModeData data, NpcState TState)
    {
        var state = TState.MoveState;

        // 使用锁定目标或默认目标
        Vector2 pos = Main.player[npc.target].position;

        state.WanderTimer++;

        // 定期更换目标点
        if (state.WanderTimer >= data.WanderChangeInterval ||
               PxUtil.Distance(npc.Center, state.WanderTarget) <
               PxUtil.ToPx(data.WanderCloseDistance))
        {
            // 更新徘徊目标
            float minDis = PxUtil.ToPx(data.WanderRadius * 0.5f);
            float maxDis = PxUtil.ToPx(data.WanderRadius);
            float dis = minDis + Main.rand.NextFloat() * (maxDis - minDis);
            state.WanderTarget = pos + Main.rand.NextVector2Unit() * dis;
            state.WanderTimer = 0;
        }

        // 向目标点移动，带平滑
        Vector2 dir = PxUtil.SafeDirectionTo(npc.Center, state.WanderTarget, Vector2.UnitX);
        Vector2 tar = dir * data.WanderSpeed;
        state.WanderVelocity = Vector2.Lerp(state.WanderVelocity, tar, data.SmoothFactor);
        npc.velocity = state.WanderVelocity;
    }
    #endregion

    #region 突进模式
    private static void DashMode(NPC npc, MoveModeData data, NpcState TState, ref bool handled)
    {
        var state = TState.MoveState;

        state.DashTimer++;

        switch (state.DashState)
        {
            // 处理突进预备阶段
            case DashState.Windup:
                // 在Windup阶段开始时记录起始位置
                if (state.DashTimer == 1)
                {
                    state.DashStartPosition = npc.Center;
                }

                // 使用锁定目标或默认目标
                Vector2 pos = Main.player[npc.target].position;

                // 计算后退方向（与目标方向相反）
                Vector2 Dir = PxUtil.SafeDirectionTo(pos, npc.Center, Vector2.UnitX);
                float dis = PxUtil.ToPx(data.DashRetreatDistance);
                Vector2 tar = state.DashStartPosition + Dir * dis;

                // 平滑后退移动，使用配置的后退速度系数
                Vector2 vel = PxUtil.SafeDirectionTo(npc.Center, tar, Vector2.UnitX) * data.DashSpeed * data.DashRetreatSpeedFactor;
                state.DashVelocity = Vector2.Lerp(state.DashVelocity, vel, data.SmoothFactor);

                // 应用后退速度
                npc.velocity = state.DashVelocity;
                handled = true;

                if (state.DashTimer >= data.DashWindup)
                {
                    state.DashState = DashState.Dashing;
                    state.DashTimer = 0;
                    // 计算突进方向（朝向目标）
                    state.DashDirection = PxUtil.SafeDirectionTo(npc.Center, pos, Vector2.UnitX);
                }
                break;

            // 处理突进阶段
            case DashState.Dashing:
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

            // 处理突进冷却阶段
            case DashState.Cooldown:
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

    #region 保持对视模式
    private static void FaceTargetMode(NPC npc, MoveModeData data, NpcState TState)
    {
        var state = TState.MoveState;

        // 使用锁定目标或默认目标
        Vector2 pos = Main.player[npc.target].position;

        // 计算目标位置（保持在对视距离的八个方向之一）
        Vector2 facePos = CalcFacePos(pos, state.FaceDirection, data.FaceDistance);

        // 平滑移动到目标位置
        Vector2 moveDir = PxUtil.SafeDirectionTo(npc.Center, facePos, Vector2.UnitX);
        Vector2 tarVel = moveDir * data.FaceSpeed;
        state.FaceVelocity = Vector2.Lerp(state.FaceVelocity, tarVel, data.FaceSmooth);
        npc.velocity = state.FaceVelocity;

        // 检查是否需要切换方向
        float toTarget = PxUtil.Distance(npc.Center, facePos);

        // 如果接近目标位置，考虑切换方向
        if (toTarget <= PxUtil.ToPx(data.SwitchDistance))
        {
            // 使用配置的换位概率
            if (Main.rand.Next(data.FaceSwitchChance) == 0)
            {
                // 切换对视方向
                int oldDir = state.FaceDirection;
                int newDir;

                // 确保新方向不是原来的方向
                do
                {
                    newDir = Main.rand.Next(0, 8);
                } while (newDir == oldDir);

                state.FaceDirection = newDir;
            }
        }
    }

    // 计算对视位置
    private static Vector2 CalcFacePos(Vector2 center, int direction, float distance)
    {
        float angle = direction * MathHelper.PiOver4; // 45度间隔
        return center + angle.ToRotationVector2() * PxUtil.ToPx(distance);
    }
    #endregion
}