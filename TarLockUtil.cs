using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;

namespace MonsterSpeed;

#region 目标锁定参数
public class TarLockParams
{
    [JsonProperty("锁定模式(0-2)", Order = 190)]
    public int LockMode { get; set; } = 0;
    [JsonProperty("优先目标(0-3)", Order = 191)]
    public int PrioTar { get; set; } = 0;
    [JsonProperty("锁定范围格数", Order = 200)]
    public int LockRange { get; set; } = 0;
    [JsonProperty("锁定速度", Order = 201)]
    public float LockSpd { get; set; } = 0f;
    [JsonProperty("锁定点偏移XY/格", Order = 202)]
    public string LockOffset_XY { get; set; } = "0,0";
    [JsonProperty("最大锁定数", Order = 203)]
    public int MaxLock { get; set; } = 1;
    [JsonProperty("扇形锁定", Order = 206)]
    public bool SectorLock { get; set; } = false;
    [JsonProperty("扇形半角", Order = 207)]
    public int SectorAng { get; set; } = 60;
    [JsonProperty("仅攻击对象", Order = 208)]
    public bool OnlyAtkTar { get; set; } = false;
}
#endregion

#region 目标锁定工具
public static class TarLockUtil
{
    #region 获取锁定目标
    public static List<int> GetLockTars(NPC npc, TarLockParams param)
    {
        var targets = new List<int>();

        if (param.LockRange <= 0 && param.LockMode != 1)
            return targets;

        switch (param.LockMode)
        {
            case 0: // 玩家锁定
                targets = GetPlayerTargets(npc, param);
                break;
            case 1: // 怪物锁定
                targets.Add(-1); // 特殊标记，表示怪物目标
                break;
            case 2: // 混合锁定
                targets = GetMixedTargets(npc, param);
                break;
        }

        return targets.Take(param.MaxLock).ToList();
    }
    #endregion

    #region 获取玩家目标
    private static List<int> GetPlayerTargets(NPC npc, TarLockParams param)
    {
        var targets = new List<int>();
        float rangePixels = PxUtil.ToPx(param.LockRange);

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (!IsValidPlayerTarget(i, npc, param, rangePixels))
                continue;

            targets.Add(i);
        }

        // 按优先级排序
        return SortTargets(targets, npc, param);
    }
    #endregion

    #region 获取混合目标
    private static List<int> GetMixedTargets(NPC npc, TarLockParams param)
    {
        var targets = GetPlayerTargets(npc, param);

        // 添加怪物目标（如果玩家目标数量不足）
        if (targets.Count < param.MaxLock)
        {
            targets.Add(-1); // 怪物目标标记
        }

        return targets;
    }
    #endregion

    #region 验证玩家目标
    private static bool IsValidPlayerTarget(int playerIndex, NPC npc, TarLockParams param, float rangePixels)
    {
        var player = Main.player[playerIndex];
        
        // 使用统一的玩家验证
        if (!PxUtil.IsValidPlr(player))
            return false;

        // 检查是否仅攻击当前目标
        if (param.OnlyAtkTar && playerIndex != npc.target)
            return false;

        // 使用统一的范围检查
        if (param.LockRange > 0 && !PxUtil.InRange(npc.Center, player.Center, rangePixels))
            return false;

        // 扇形区域检查
        if (param.SectorLock && !IsInSector(npc, player.Center, param.SectorAng))
            return false;

        return true;
    }
    #endregion

    #region 扇形检查
    private static bool IsInSector(NPC npc, Vector2 targetPos, int sectorAngle)
    {
        var directionToTarget = targetPos - npc.Center;
        
        // 如果NPC没有移动方向，默认接受所有方向
        if (npc.velocity == Vector2.Zero)
            return true;

        // 使用NPC的移动方向作为参考方向
        var npcDirection = Vector2.Normalize(npc.velocity);
        var targetDirection = Vector2.Normalize(directionToTarget);

        var angle = AngleBetween(npcDirection, targetDirection);
        return Math.Abs(angle) <= sectorAngle;
    }
    #endregion

    #region 计算角度差（优化版）
    private static double AngleBetween(Vector2 v1, Vector2 v2)
    {
        // 归一化向量
        v1 = Vector2.Normalize(v1);
        v2 = Vector2.Normalize(v2);
        
        var dot = Vector2.Dot(v1, v2);
        var det = v1.X * v2.Y - v1.Y * v2.X;
        return Math.Atan2(det, dot) * (180.0 / Math.PI);
    }
    #endregion

    #region 目标排序
    private static List<int> SortTargets(List<int> targets, NPC npc, TarLockParams param)
    {
        if (targets.Count <= 1)
            return targets;

        return param.PrioTar switch
        {
            0 => targets.OrderBy(i => Vector2.DistanceSquared(npc.Center, Main.player[i].Center)).ToList(), // 最近
            1 => targets.OrderBy(i => Main.player[i].statLife).ToList(), // 血量最少
            2 => targets.OrderBy(i => Main.player[i].statDefense).ToList(), // 防御最低
            3 => targets.OrderByDescending(i => Main.player[i].aggro).ToList(), // 仇恨最高
            _ => targets
        };
    }
    #endregion

    #region 获取目标位置
    public static Vector2 GetTargetPosition(int targetIndex, NPC npc, TarLockParams param)
    {
        if (targetIndex == -1) // 怪物目标
        {
            return GetNpcTargetPosition(npc, param);
        }
        else // 玩家目标
        {
            return GetPlayerTargetPosition(targetIndex, param);
        }
    }
    #endregion

    #region 获取怪物目标位置
    private static Vector2 GetNpcTargetPosition(NPC npc, TarLockParams param)
    {
        Vector2 npcPosition = npc.Center;
        Vector2 pixelOffset = GetLockOffsetVector(param);

        // 寻找最近的怪物作为目标
        NPC closestMonster = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var targetNpc = Main.npc[i];
            
            // 使用统一的怪物验证
            if (!PxUtil.IsValidMst(targetNpc, npc))
                continue;

            // 使用统一的范围检查
            if (param.LockRange > 0 && !PxUtil.InRangeTiles(npcPosition, targetNpc.Center, param.LockRange))
                continue;

            float distance = Vector2.DistanceSquared(npcPosition, targetNpc.Center);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestMonster = targetNpc;
            }
        }

        // 返回目标位置（如果有找到怪物）或NPC位置+偏移
        return closestMonster?.Center + pixelOffset ?? npcPosition + pixelOffset;
    }
    #endregion

    #region 获取玩家目标位置
    private static Vector2 GetPlayerTargetPosition(int playerIndex, TarLockParams param)
    {
        var player = Main.player[playerIndex];
        Vector2 pixelOffset = GetLockOffsetVector(param);
        return player.Center + pixelOffset;
    }
    #endregion

    #region 获取锁定偏移向量（使用字符串格式）
    private static Vector2 GetLockOffsetVector(TarLockParams param)
    {
        if (string.IsNullOrWhiteSpace(param.LockOffset_XY) || param.LockOffset_XY == "0,0")
            return Vector2.Zero;

        // 解析偏移字符串
        var Result = PxUtil.ParseFloatRange(param.LockOffset_XY);
        if (!Result.success)
            return Vector2.Zero;

        float offsetX = Result.min;
        float offsetY = Result.max;

        return PxUtil.ToPx(new Vector2(offsetX, offsetY));
    }
    #endregion

    #region 批量获取目标位置
    public static List<Vector2> GetTargetPositions(List<int> targetIndices, NPC npc, TarLockParams param)
    {
        var positions = new List<Vector2>();
        
        foreach (int targetIndex in targetIndices)
        {
            positions.Add(GetTargetPosition(targetIndex, npc, param));
        }
        
        return positions;
    }
    #endregion

    #region 验证锁定参数
    public static bool ValidateLockParams(TarLockParams param)
    {
        if (param == null)
            return false;

        if (param.LockMode < 0 || param.LockMode > 2)
            return false;

        if (param.PrioTar < 0 || param.PrioTar > 3)
            return false;

        if (param.LockRange < 0)
            return false;

        if (param.MaxLock < 1)
            return false;

        if (param.SectorAng < 0 || param.SectorAng > 180)
            return false;

        return true;
    }
    #endregion
}
#endregion