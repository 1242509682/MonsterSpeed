using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Terraria;

namespace MonsterSpeed
{
    #region 目标锁定参数
    public class TarLockData
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
        [JsonProperty("视线检查", Order = 209)]
        public bool RequireLineOfSight { get; set; } = false;

        // 验证锁定参数
        public bool Validate()
        {
            if (LockMode < 0 || LockMode > 2)
                return false;

            if (PrioTar < 0 || PrioTar > 3)
                return false;

            if (LockRange < 0)
                return false;

            if (MaxLock < 1)
                return false;

            if (SectorAng < 0 || SectorAng > 180)
                return false;

            return true;
        }
    }
    #endregion

    public static class TarLockMode
    {
        #region 获取锁定目标
        public static List<int> GetLockTars(NPC npc, TarLockData data)
        {
            var targets = new List<int>();

            if (data.LockRange <= 0 && data.LockMode != 1)
                return targets;

            if (!data.Validate())
                return targets;

            switch (data.LockMode)
            {
                case 0: // 玩家锁定
                    targets = GetPlayerTargets(npc, data);
                    break;
                case 1: // 怪物锁定
                    targets.Add(-1); // 特殊标记，表示怪物目标
                    break;
                case 2: // 混合锁定
                    targets = GetMixedTargets(npc, data);
                    break;
            }

            return targets.Take(data.MaxLock).ToList();
        }
        #endregion

        #region 获取玩家目标
        private static List<int> GetPlayerTargets(NPC npc, TarLockData data)
        {
            var targets = new List<int>();
            float rangePixels = PxUtil.ToPx(data.LockRange);

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (!IsValidPlayerTarget(i, npc, data, rangePixels))
                    continue;

                targets.Add(i);
            }

            // 按优先级排序
            return SortTargets(targets, npc, data);
        }
        #endregion

        #region 获取混合目标
        private static List<int> GetMixedTargets(NPC npc, TarLockData data)
        {
            var targets = GetPlayerTargets(npc, data);

            // 添加怪物目标（如果玩家目标数量不足）
            if (targets.Count < data.MaxLock)
            {
                targets.Add(-1); // 怪物目标标记
            }

            return targets;
        }
        #endregion

        #region 验证玩家目标 - 使用自动追踪的方法
        private static bool IsValidPlayerTarget(int playerIndex, NPC npc, TarLockData data, float rangePixels)
        {
            var plr = Main.player[playerIndex];
            
            // 使用自动追踪的目标验证方法
            if (!IsValidTarget(plr, npc, data))
                return false;

            // 使用自动追踪的范围检查方法
            if (data.LockRange > 0 && !IsTargetInRange(npc, plr, data.LockRange))
                return false;

            // 扇形区域检查
            if (data.SectorLock && !IsInSector(npc, plr.Center, data.SectorAng))
                return false;

            // 视线检查 - 使用自动追踪的视线检查
            if (data.RequireLineOfSight && !HasLineOfSight(npc, plr))
                return false;

            return true;
        }

        // 使用自动追踪的目标验证逻辑
        private static bool IsValidTarget(Player plr, NPC npc, TarLockData data)
        {
            // 使用PxUtil的统一玩家验证
            if (!PxUtil.IsValidPlr(plr))
                return false;

            // 检查是否仅攻击当前目标
            if (data.OnlyAtkTar && plr.whoAmI != npc.target)
                return false;

            return true;
        }

        // 使用自动追踪的范围检查逻辑
        private static bool IsTargetInRange(Entity npc, Entity tar, float rangeTiles)
        {
            if (npc == null || tar == null) return false;
            
            float rangePixels = PxUtil.ToPx(rangeTiles);
            return PxUtil.InRange(npc.Center, tar.Center, rangePixels);
        }

        // 使用自动追踪的视线检查逻辑
        private static bool HasLineOfSight(Entity source, Entity target)
        {
            if (source == null || target == null || !source.active || !target.active)
                return false;
            
            return Collision.CanHit(source.Center, 0, 0, target.Center, 0, 0);
        }
        #endregion

        #region 扇形检查 - 使用PxUtil的方法
        private static bool IsInSector(Entity npc, Vector2 tarPos, int sectorAngle)
        {
            var directionToTarget = tarPos - npc.Center;
            
            // 如果NPC没有移动方向，默认接受所有方向
            if (npc.velocity == Vector2.Zero)
                return true;

            // 使用NPC的移动方向作为参考方向
            var npcDirection = Vector2.Normalize(npc.velocity);
            var targetDirection = Vector2.Normalize(directionToTarget);

            var angle = PxUtil.AngleTo(Vector2.Zero, npcDirection, targetDirection);
            return Math.Abs(angle) <= sectorAngle;
        }
        #endregion

        #region 目标排序 - 保留原有的评分机制
        private static List<int> SortTargets(List<int> tars, Entity npc, TarLockData data)
        {
            if (tars.Count <= 1)
                return tars;

            return data.PrioTar switch
            {
                0 => tars.OrderBy(i => PxUtil.DistanceSquared(npc.Center, Main.player[i].Center)).ToList(), // 最近
                1 => tars.OrderBy(i => Main.player[i].statLife).ToList(), // 血量最少
                2 => tars.OrderBy(i => Main.player[i].statDefense).ToList(), // 防御最低
                3 => tars.OrderByDescending(i => Main.player[i].aggro).ToList(), // 仇恨最高
                _ => tars
            };
        }
        #endregion

        #region 获取目标位置
        public static Vector2 GetTargetPosition(int tarIndex, NPC npc, TarLockData data)
        {
            if (tarIndex == -1) // 怪物目标
            {
                return GetNpcTargetPosition(npc, data);
            }
            else // 玩家目标
            {
                return GetPlayerTargetPosition(tarIndex, data);
            }
        }
        #endregion

        #region 获取怪物目标位置 - 使用自动追踪的方法
        private static Vector2 GetNpcTargetPosition(NPC npc, TarLockData data)
        {
            Vector2 npcPos = npc.Center;
            Vector2 pixelOffset = GetLockOffsetVector(data);

            // 使用自动追踪的目标查找逻辑
            NPC Monster = FindMonster(npc, data);
            
            // 返回目标位置（如果有找到怪物）或NPC位置+偏移
            return Monster?.Center + pixelOffset ?? npcPos + pixelOffset;
        }

        private static NPC FindMonster(NPC npc, TarLockData data)
        {
            NPC npcs = new NPC();
            float closestDistance = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                var tar = Main.npc[i];
                
                // 使用PxUtil的统一怪物验证
                if (!PxUtil.IsValidMst(tar, npc))
                    continue;

                // 使用自动追踪的范围检查
                if (data.LockRange > 0 && !IsTargetInRange(npc, tar, data.LockRange))
                    continue;

                float distance = PxUtil.DistanceSquared(npc.Center, tar.Center);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    npcs = tar;
                }
            }

            return npcs;
        }
        #endregion

        #region 获取玩家目标位置
        private static Vector2 GetPlayerTargetPosition(int plrIndex, TarLockData data)
        {
            var plr = Main.player[plrIndex];
            Vector2 pixelOffset = GetLockOffsetVector(data);
            return plr.Center + pixelOffset;
        }
        #endregion

        #region 获取锁定偏移向量（使用字符串格式）- 使用PxUtil的方法
        private static Vector2 GetLockOffsetVector(TarLockData data)
        {
            if (string.IsNullOrWhiteSpace(data.LockOffset_XY) || data.LockOffset_XY == "0,0")
                return Vector2.Zero;

            // 使用PxUtil的字符串解析
            var result = PxUtil.ParseFloatRange(data.LockOffset_XY);
            if (!result.success)
                return Vector2.Zero;

            float offsetX = result.min;
            float offsetY = result.max;

            return PxUtil.ToPx(new Vector2(offsetX, offsetY));
        }
        #endregion

        #region 批量获取目标位置
        public static List<Vector2> GetTargetPositions(List<int> tarsIndex, NPC npc, TarLockData data)
        {
            var pos = new List<Vector2>();
            
            foreach (int Index in tarsIndex)
            {
                pos.Add(GetTargetPosition(Index, npc, data));
            }
            
            return pos;
        }
        #endregion
    }
}