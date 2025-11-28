using System.Data;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;

namespace MonsterSpeed;

// 复杂弹幕参数 - 修改为独立的布尔模式
public class CplxProjParams
{
    [JsonProperty("启用圆形分布", Order = 99)]
    public bool RadialEnabled { get; set; } = false;
    [JsonProperty("启用角度分散", Order = 100)]
    public bool AngularEnabled { get; set; } = false;
    [JsonProperty("启用位置偏移", Order = 101)]
    public bool OffsetEnabled { get; set; } = false;

    [JsonProperty("圆形角度", Order = 103)]
    public int RadialAng { get; set; } = 0;
    [JsonProperty("圆形半径格数", Order = 104)]
    public int RadialR { get; set; } = 0;
    [JsonProperty("圆形起始角", Order = 105)]
    public int StartAng { get; set; } = 0;
    [JsonProperty("角度分散", Order = 107)]
    public float AngDisp { get; set; } = 0f;
    [JsonProperty("偏移量XY/格", Order = 109)]
    public string Offset { get; set; } = "0,0";
}

#region 复杂弹幕生成器
public static class CplxProjGen
{
    #region 生成复杂弹幕
    public static void SpawnCplxProj(SpawnProjData data, NPC npc, Vector2 basePos, Vector2 baseVel, NpcState state)
    {
        if (data.CplxParams == null ||
            (!data.CplxParams.RadialEnabled && !data.CplxParams.AngularEnabled && !data.CplxParams.OffsetEnabled))
            return;

        // 使用现有的发射计数器
        int currStack = state.SendStack[state.SendProjIndex];

        // 检查是否已经发射完所有弹幕
        if (currStack >= data.Stack)
            return;


        // 应用圆形分布模式
        if (data.CplxParams.RadialEnabled)
        {
            SpawnRadial(data, npc, basePos, baseVel, state, currStack);
        }

        // 应用角度分散模式
        if (data.CplxParams.AngularEnabled)
        {
            SpawnAngular(data, npc, basePos, baseVel, state, currStack);
        }

        // 应用位置偏移模式
        if (data.CplxParams.OffsetEnabled)
        {
            SpawnOffset(data, npc, basePos, baseVel, state, currStack);
        }
    }
    #endregion

    #region 圆形分布弹幕（共用计数器）
    private static void SpawnRadial(SpawnProjData data, NPC npc, Vector2 basePos, Vector2 baseVel, NpcState state, int currentIndex)
    {
        var param = data.CplxParams;
        if (data.Stack <= 0 || param.RadialR <= 0) return;

        float radiusPixels = PxUtil.ToPx(param.RadialR);
        double angleStep = 360.0 / data.Stack;

        // 使用当前索引计算角度
        double currentAngle = param.StartAng + (currentIndex * angleStep);

        // 计算圆形分布坐标
        Vector2 offset = CalculateCircularOffset(currentAngle, radiusPixels);
        Vector2 position = basePos + offset;

        // 生成弹幕
        CreateProjectile(data, npc, position, baseVel, state);
    }
    #endregion

    #region 角度分散弹幕（共用计数器）
    private static void SpawnAngular(SpawnProjData data, NPC npc, Vector2 basePos, Vector2 baseVel, NpcState state, int currentIndex)
    {
        var param = data.CplxParams;
        if (data.Stack <= 0 || param.AngDisp == 0f) return;

        // 计算基础角度
        double baseAngle = Math.Atan2(baseVel.Y, baseVel.X) * (180.0 / Math.PI);

        // 使用当前索引计算角度
        double currentAngle = baseAngle - ((data.Stack - 1) * param.AngDisp / 2) + (currentIndex * param.AngDisp);

        // 计算角度分散速度
        Vector2 velocity = CalculateVelocityFromAngle(currentAngle, baseVel.Length());

        // 生成弹幕
        CreateProjectile(data, npc, basePos, velocity, state);
    }
    #endregion

    #region 位置偏移弹幕（共用计数器）
    private static void SpawnOffset(SpawnProjData data, NPC npc, Vector2 basePos, Vector2 baseVel, NpcState state, int currentIndex)
    {
        var param = data.CplxParams;
        if (data.Stack <= 0 || string.IsNullOrWhiteSpace(param.Offset)) return;

        Vector2 pixelOff = GetOffsetVector(param);

        // 使用当前索引计算位置
        Vector2 currPos = basePos + (pixelOff * currentIndex);

        // 生成弹幕
        CreateProjectile(data, npc, currPos, baseVel, state);
    }
    #endregion

    #region 计算圆形偏移
    private static Vector2 CalculateCircularOffset(double angle, float radius)
    {
        double radians = angle * (Math.PI / 180.0);
        return new Vector2(
            (float)(radius * Math.Cos(radians)),
            (float)(radius * Math.Sin(radians))
        );
    }
    #endregion

    #region 从角度计算速度
    private static Vector2 CalculateVelocityFromAngle(double angle, float speed)
    {
        double radians = angle * (Math.PI / 180.0);
        return new Vector2(
            (float)(speed * Math.Cos(radians)),
            (float)(speed * Math.Sin(radians))
        );
    }
    #endregion

    #region 获取偏移向量
    private static Vector2 GetOffsetVector(CplxProjParams param)
    {
        if (string.IsNullOrWhiteSpace(param.Offset) || param.Offset == "0,0")
            return Vector2.Zero;

        // 解析偏移字符串
        var Result = PxUtil.ParseFloatRange(param.Offset);
        if (!Result.success)
            return Vector2.Zero;

        // Result.min 对应X偏移，Result.max 对应Y偏移
        return PxUtil.ToPx(new Vector2(Result.min, Result.max));
    }
    #endregion

    #region 创建复杂弹幕（支持锁定）
    private static bool CreateProjectile(SpawnProjData data, NPC npc, Vector2 pos, Vector2 baseVel, NpcState state)
    {
        Vector2 finalVel = baseVel;

        // 应用目标锁定
        if (data.LockParams != null && data.LockParams.LockRange > 0)
        {
            var tars = TarLockMode.GetLockTars(npc, data.LockParams);
            if (tars.Count > 0)
            {
                var tarPos = TarLockMode.GetTargetPosition(tars[0], npc, data.LockParams);
                var dir = tarPos - pos;

                if (dir != Vector2.Zero)
                {
                    finalVel = Vector2.Normalize(dir) * baseVel.Length();
                }
            }
        }

        // 获取AI参数
        float ai0 = data.AI.ContainsKey(0) ? data.AI[0] : 0f;
        float ai1 = data.AI.ContainsKey(1) ? data.AI[1] : 0f;
        float ai2 = data.AI.ContainsKey(2) ? data.AI[2] : 0f;

        // 创建弹幕
        int projId = Projectile.NewProjectile(
            npc.GetSpawnSourceForNPCFromNPCAI(),
            pos.X, pos.Y, finalVel.X, finalVel.Y,
            data.Type, data.Damage, data.KnockBack,
            Main.myPlayer, ai0, ai1, ai2
        );

        // 设置弹幕生命时间
        if (data.Life > 0 && projId < Main.maxProjectiles)
        {
            Main.projectile[projId].timeLeft = data.Life;
        }

        // 注册更新弹幕
        if (data.UpdateProj != null && data.UpdateProj.Count > 0)
        {
            // 确保索引在有效范围内
            if (projId >= 0 && projId < UpdateProjectile.UpdateState.Length)
            {
                // 使用提供的安全方法而不是直接访问数组
                if (!UpdateProjectile.AddState(projId, npc.whoAmI, Main.projectile[projId].type))
                {
                    TShock.Log.ConsoleWarn($"[怪物加速] 注册弹幕更新状态失败: {projId}");
                }
            }
        }

        state.SendStack[state.SendProjIndex]++;  //更新发射数计数器
        state.SendCD[state.SendProjIndex] = data.Interval;  //设置间隔
        state.SPCount++; // 更新总发射次数

        return projId < Main.maxProjectiles;
    }
    #endregion

    #region 验证复杂弹幕参数
    public static bool ValidateCplxParams(CplxProjParams param)
    {
        if (param == null)
            return false;

        // 模式特定验证
        if (param.RadialEnabled)
        {
            if (param.RadialR < 0)
                return false;
        }

        if (param.OffsetEnabled)
        {
            if (string.IsNullOrWhiteSpace(param.Offset))
                return false;

            // 验证偏移字符串格式
            var Result = PxUtil.ParseFloatRange(param.Offset);
            if (!Result.success)
                return false;
        }

        return true;
    }
    #endregion
}
#endregion