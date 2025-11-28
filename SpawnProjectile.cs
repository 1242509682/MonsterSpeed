using System;
using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.DataStructures;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.UpdateProjectile;

namespace MonsterSpeed;

#region 弹幕数据
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
    [JsonProperty("射弹条件", Order = 4)]
    public string Condition { get; set; } = "默认配置";
    [JsonProperty("伤害", Order = 5)]
    public int Damage = 30;
    [JsonProperty("击退", Order = 6)]
    public int KnockBack = 5;
    [JsonProperty("速度", Order = 7)]
    public float Velocity = 10f;
    [JsonProperty("速度向量XY/格", Order = 8)]
    public string Velocity_XY = "0,0";
    [JsonProperty("半径格数", Order = 9)]
    public float Radius = 0f;
    [JsonProperty("发射位置偏移XY/格", Order = 10)]
    public string SpawnOffset_XY { get; set; } = "0,0";
    [JsonProperty("偏移角度", Order = 11)]
    public float Angle = 0;
    [JsonProperty("旋转角度", Order = 12)]
    public float Rotate = 0;
    [JsonProperty("以玩家为中心", Order = 13)]
    public bool TarCenter = false;
    [JsonProperty("复杂模式", Order = 50)]
    public CplxProjParams CplxParams { get; set; } = new();
    [JsonProperty("目标锁定", Order = 60)]
    public TarLockData LockParams { get; set; }
    [JsonProperty("指示物注入AI", Order = 73)]
    public Dictionary<int, string> MarkerToAI { get; set; } = new();
    [JsonProperty("更新弹幕", Order = 81)]
    public List<string> UpdateProj { get; set; } = new List<string>();
    [JsonProperty("弹幕AI", Order = 82)]
    public Dictionary<int, float> AI { get; set; } = new();

    #region 计算最终速度（使用XY分离格式）
    public Vector2 GetFinalVelocity(NPC npc, NPCAimedTarget tar, Player plr, NpcState state)
    {
        Vector2 baseVel;

        // 优先使用速度向量字符串
        if (!string.IsNullOrWhiteSpace(Velocity_XY) && Velocity_XY != "0,0")
        {
            var Result = PxUtil.ParseFloatRange(Velocity_XY);
            if (Result.success)
            {
                baseVel = PxUtil.ToPx(new Vector2(Result.min, Result.max));
            }
            else
            {
                baseVel = Vector2.Zero;
            }
        }
        // 使用标量速度+方向
        else if (Velocity > 0)
        {
            var dict = tar.Center - npc.Center;
            baseVel = dict.SafeNormalize(Vector2.Zero) * Velocity;
        }
        else
        {
            baseVel = Vector2.Zero;
        }

        return baseVel;
    }
    #endregion

    #region 计算最终位置（使用XY分离格式）
    public Vector2 GetFinalPosition(NPC npc, Player plr, NpcState state)
    {
        Vector2 basePos = TarCenter ? plr.Center : npc.Center;

        // 应用位置偏移字符串
        if (!string.IsNullOrWhiteSpace(SpawnOffset_XY) && SpawnOffset_XY != "0,0")
        {
            var Result = PxUtil.ParseFloatRange(SpawnOffset_XY);
            if (Result.success)
            {
                Vector2 offset = PxUtil.ToPx(new Vector2(Result.min, Result.max));
                basePos += offset;
            }
        }

        // 应用半径偏移
        if (Radius != 0f)
        {
            basePos = SpawnProjectile.ApplyRadiusOffset(basePos, this, state);
        }

        return basePos;
    }
    #endregion
}
#endregion

internal class SpawnProjectile
{
    #region 生成弹幕（主方法）
    public static void SpawnProj(NpcData Setting, List<SpawnProjData> SpawnProj, NPC npc)
    {
        if (SpawnProj == null || SpawnProj.Count == 0 || npc == null) return;

        var tar = npc.GetTargetData(true);
        var state = StateUtil.GetState(npc);

        if (state == null || state.SendProjIndex >= SpawnProj.Count) return;

        var data = SpawnProj[state.SendProjIndex];

        // 初始化发射数量
        if (!state.SendStack.TryGetValue(state.SendProjIndex, out var _)) state.SendStack[state.SendProjIndex] = 0;
        // 初始化发射间隔
        if (!state.SendCD.TryGetValue(state.SendProjIndex, out var _)) state.SendCD[state.SendProjIndex] = 0f;

        // 发射数量超过设定，切换下组弹幕
        if (state.SendStack.ContainsKey(state.SendProjIndex) && 
            state.SendStack[state.SendProjIndex] >= data.Stack)
        {
            NextProj(SpawnProj, state);
        }

        // 条件检查
        if (!string.IsNullOrEmpty(data.Condition))
        {
            bool allow = true;
            var cond = CondFileManager.GetCondData(data.Condition);
            Conditions.Condition(npc, new StringBuilder(), Setting, cond, ref allow);

            if (!allow)
            {
                NextProj(SpawnProj, state);
            }
        }

        // 冷却检查
        if (state.SendCD[state.SendProjIndex] > 0f)
        {
            state.SendCD[state.SendProjIndex] -= 1f;
        }
        else
        {
            GenerateProj(data, npc, tar, state); // 生成弹幕
        }

        // 检查所有更新弹幕
        CheckAllUpdate(Setting, npc, SpawnProj);
    }
    #endregion

    #region 生成单个弹幕
    private static void GenerateProj(SpawnProjData data, NPC npc, NPCAimedTarget tar, NpcState state)
    {
        if (data == null || npc == null || state == null) return;

        var plr = Main.player[npc.target];
        if (plr == null) return;

        // 计算基础位置和速度
        var pos = data.TarCenter ? plr.Center : npc.Center;
        var vel = BaseVelocity(data, npc, tar, plr, state);

        // 应用半径偏移
        var RadiusPos = ApplyRadiusOffset(pos, data, state);

        // 应用角度偏移
        var AngleVel = ApplyAngleOffset(vel, data, state);

        // 生成基础弹幕
        if (data.Life > 0)
        {
            CreateBaseProj(data, npc, RadiusPos, AngleVel, state);
        }

        // 生成复杂弹幕
        if (data.CplxParams != null)
        {
           CplxProjGen.SpawnCplxProj(data, npc, RadiusPos, AngleVel, state);
        }
    }
    #endregion

    #region 创建基础弹幕
    private static void CreateBaseProj(SpawnProjData data, NPC npc, Vector2 pos, Vector2 vel, NpcState state)
    {
        if (data == null || npc == null) return;

        var ai0 = data.AI.ContainsKey(0) ? data.AI[0] : 0f;
        var ai1 = data.AI.ContainsKey(1) ? data.AI[1] : 0f;
        var ai2 = data.AI.ContainsKey(2) ? data.AI[2] : 0f;

        var ProjIndex = Projectile.NewProjectile(
            npc.GetSpawnSourceForNPCFromNPCAI(),
            pos.X, pos.Y, vel.X, vel.Y,
            data.Type, data.Damage, data.KnockBack,
            Main.myPlayer, ai0, ai1, ai2
        );

        var proj = Main.projectile[ProjIndex];

        proj.timeLeft = data.Life;

        // 应用指示物注入AI
        if (data.MarkerToAI != null && data.MarkerToAI.Count > 0)
        {
            MarkerUtil.InjectToAI(state, data.MarkerToAI, proj);
        }

        // 注册更新弹幕
        if (data.UpdateProj != null && data.UpdateProj.Count > 0)
        {
            // 确保索引在有效范围内
            if (ProjIndex >= 0 && ProjIndex < UpdateState.Length)
            {
                // 使用提供的安全方法而不是直接访问数组
                if (!AddState(ProjIndex, npc.whoAmI, proj.type))
                {
                    TShock.Log.ConsoleWarn($"[怪物加速] 注册弹幕更新状态失败: {ProjIndex}");
                }
            }
        }

        state.SendStack[state.SendProjIndex]++;  //更新发射数计数器
        state.SendCD[state.SendProjIndex] = data.Interval;  //设置间隔
        state.SPCount++; // 更新发射组次数
    }
    #endregion

    #region 切换到下一个弹幕
    private static void NextProj(List<SpawnProjData> data, NpcState state)
    {
        // 移动到下个弹幕（自动循环）
        state.SendProjIndex = (state.SendProjIndex + 1) % data.Count;

        // 重置发射计数器和发射间隔
        state.SendStack[state.SendProjIndex] = 0;
        state.SendCD[state.SendProjIndex] = 0f;
    }
    #endregion

    #region 计算基础速度
    private static Vector2 BaseVelocity(SpawnProjData data, NPC npc, NPCAimedTarget tar, Player plr, NpcState state)
    {
        // 基础模式
        var dict = tar.Center - npc.Center;
        if (data.LockParams is null)
            return dict.SafeNormalize(Vector2.Zero) * data.Velocity;

        // 目标锁定模式
        if (data.LockParams.LockRange > 0)
        {
            var tars = TarLockMode.GetLockTars(npc, data.LockParams);
            if (tars.Count > 0)
            {
                var tarPos = TarLockMode.GetTargetPosition(tars[0], npc, data.LockParams);
                var center = data.TarCenter ? plr.Center : npc.Center;
                var dir = tarPos - center;

                // 检查方向向量是否为零
                if (dir == Vector2.Zero)
                    return Vector2.Zero;

                return dir.SafeNormalize(Vector2.Zero) * data.Velocity;
            }
            return Vector2.Zero;
        }

        // 检查基础方向向量是否为零
        if (dict == Vector2.Zero)
            return Vector2.Zero;

        return dict.SafeNormalize(Vector2.Zero) * data.Velocity;
    }
    #endregion

    #region 应用半径偏移（统一方法）
    public static Vector2 ApplyRadiusOffset(Vector2 basePos, SpawnProjData data, NpcState state)
    {
        if (data.Radius == 0) return basePos;

        // 防止除零错误
        if (data.Stack <= 1) return basePos;

        var radiusPx = PxUtil.ToPx(data.Radius);
        var angle = state.SendStack[state.SendProjIndex] / (float)(data.Stack - 1) * MathHelper.TwoPi;
        var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radiusPx;

        return basePos + offset;
    }
    #endregion

    #region 应用角度偏移
    private static Vector2 ApplyAngleOffset(Vector2 baseVel, SpawnProjData data, NpcState state)
    {
        if (data.Angle == 0 && data.Rotate == 0) return baseVel;

        var totalAngle = 0f;

        // 基础角度偏移
        if (data.Angle != 0)
        {
            // 防止除零错误
            if (data.Stack > 1)
            {
                var angleRange = data.Angle * Math.PI / 180;
                var angleStep = angleRange * 2 / (data.Stack - 1);
                totalAngle += (float)((state.SendStack[state.SendProjIndex] - (data.Stack - 1) / 2.0f) * angleStep);
            }
        }

        // 旋转偏移
        if (data.Rotate != 0)
        {
            totalAngle += data.Rotate * state.SendStack[state.SendProjIndex] * MathHelper.Pi / 180f;
        }

        // 检查基础速度是否为零
        if (baseVel == Vector2.Zero)
            return Vector2.Zero;

        return baseVel.RotatedBy(totalAngle);
    }
    #endregion
}