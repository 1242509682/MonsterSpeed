using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;

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

    [JsonProperty("圆形数量", Order = 102)]
    public int RadialCnt { get; set; } = 0;
    [JsonProperty("圆形角度", Order = 103)]
    public int RadialAng { get; set; } = 0;
    [JsonProperty("圆形半径格数", Order = 104)]
    public int RadialR { get; set; } = 0;
    [JsonProperty("圆形起始角", Order = 105)]
    public int StartAng { get; set; } = 0;
    [JsonProperty("角度数量", Order = 106)]
    public int AngCnt { get; set; } = 0;
    [JsonProperty("角度分散", Order = 107)]
    public float AngDisp { get; set; } = 0f;
    [JsonProperty("偏移数量", Order = 108)]
    public int OffsetCnt { get; set; } = 0;
    [JsonProperty("偏移量XY/格", Order = 109)]
    public string Offset { get; set; } = "0,0";
}

#region 复杂弹幕生成器
public static class CplxProjGen
{
    #region 生成复杂弹幕
    public static int SpawnCplxProj(SpawnProjData proj, NPC npc, Vector2 basePos, Vector2 baseVel)
    {
        if (proj.CplxParams == null ||
            (!proj.CplxParams.RadialEnabled && !proj.CplxParams.AngularEnabled && !proj.CplxParams.OffsetEnabled))
            return 0;

        int Count = 0;

        // 应用圆形分布模式
        if (proj.CplxParams.RadialEnabled)
        {
            Count += SpawnRadial(proj, npc, basePos, baseVel);
        }

        // 应用角度分散模式
        if (proj.CplxParams.AngularEnabled)
        {
            Count += SpawnAngular(proj, npc, basePos, baseVel);
        }

        // 应用位置偏移模式
        if (proj.CplxParams.OffsetEnabled)
        {
            Count += SpawnOffset(proj, npc, basePos, baseVel);
        }

        return Count;
    }
    #endregion

    #region 圆形分布弹幕
    private static int SpawnRadial(SpawnProjData proj, NPC npc, Vector2 basePos, Vector2 baseVel)
    {
        var param = proj.CplxParams;
        // 修改：使用SpawnProjData的Stack作为数量
        int radialCount = proj.Stack > 0 ? proj.Stack : param.RadialCnt;
        if (radialCount <= 0 || param.RadialR <= 0)
            return 0;

        int Count = 0;
        float radiusPixels = PxUtil.ToPx(param.RadialR);
        double angleStep = 360.0 / radialCount; // 自动计算角度步长
        double currentAngle = param.StartAng;

        for (int i = 0; i < radialCount; i++)
        {
            // 计算圆形分布坐标
            Vector2 offset = CalculateCircularOffset(currentAngle, radiusPixels);
            Vector2 position = basePos + offset;

            // 生成弹幕
            if (CreateComplexProjectile(proj, npc, position, baseVel))
                Count++;

            currentAngle += angleStep;
        }

        return Count;
    }
    #endregion

    #region 角度分散弹幕
    private static int SpawnAngular(SpawnProjData proj, NPC npc, Vector2 basePos, Vector2 baseVel)
    {
        var param = proj.CplxParams;
        // 修改：使用SpawnProjData的Stack作为数量
        int angularCount = proj.Stack > 0 ? proj.Stack : param.AngCnt;
        if (angularCount <= 0 || param.AngDisp == 0f)
            return 0;

        int Count = 0;

        // 计算基础角度（从速度向量获取）
        double baseAngle = Math.Atan2(baseVel.Y, baseVel.X) * (180.0 / Math.PI);
        double currentAngle = baseAngle - ((angularCount - 1) * param.AngDisp / 2);

        for (int i = 0; i < angularCount; i++)
        {
            // 计算角度分散速度
            Vector2 velocity = CalculateVelocityFromAngle(currentAngle, baseVel.Length());

            // 生成弹幕
            if (CreateComplexProjectile(proj, npc, basePos, velocity))
                Count++;

            currentAngle += param.AngDisp;
        }

        return Count;
    }
    #endregion

    #region 位置偏移弹幕
    private static int SpawnOffset(SpawnProjData proj, NPC npc, Vector2 basePos, Vector2 baseVel)
    {
        var param = proj.CplxParams;
        // 修改：使用SpawnProjData的Stack作为数量
        int offsetCount = proj.Stack > 0 ? proj.Stack : param.OffsetCnt;
        if (offsetCount <= 0 || string.IsNullOrWhiteSpace(param.Offset))
            return 0;

        int SpCount = 0;
        Vector2 currPos = basePos;
        Vector2 pixelOff = GetOffsetVector(param);

        for (int i = 0; i < offsetCount; i++)
        {
            // 生成弹幕
            if (CreateComplexProjectile(proj, npc, currPos, baseVel))
                SpCount++;

            currPos += pixelOff;
        }

        return SpCount;
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
    private static bool CreateComplexProjectile(SpawnProjData proj, NPC npc, Vector2 pos, Vector2 baseVel)
    {
        Vector2 finalVel = baseVel;

        // 应用目标锁定
        if (proj.LockParams != null && proj.LockParams.LockRange > 0)
        {
            var tars = TarLockUtil.GetLockTars(npc, proj.LockParams);
            if (tars.Count > 0)
            {
                var tarPos = TarLockUtil.GetTargetPosition(tars[0], npc, proj.LockParams);
                var dir = tarPos - pos;

                if (dir != Vector2.Zero)
                {
                    finalVel = Vector2.Normalize(dir) * baseVel.Length();
                }
            }
        }

        // 获取AI参数
        float ai0 = proj.AI.ContainsKey(0) ? proj.AI[0] : 0f;
        float ai1 = proj.AI.ContainsKey(1) ? proj.AI[1] : 0f;
        float ai2 = proj.AI.ContainsKey(2) ? proj.AI[2] : 0f;

        // 创建弹幕
        int projId = Projectile.NewProjectile(
            npc.GetSpawnSourceForNPCFromNPCAI(),
            pos.X, pos.Y, finalVel.X, finalVel.Y,
            proj.Type, proj.Damage, proj.KnockBack,
            Main.myPlayer, ai0, ai1, ai2
        );

        // 设置弹幕生命时间
        if (proj.Life > 0 && projId < Main.maxProjectiles)
        {
            Main.projectile[projId].timeLeft = proj.Life;
        }

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