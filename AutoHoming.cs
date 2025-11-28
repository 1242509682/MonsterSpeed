using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Terraria;

namespace MonsterSpeed;

#region 追踪参数配置类
public class HomingData
{
    [JsonProperty("启用追踪", Order = 19)]
    public bool Homing = false;
    [JsonProperty("追踪模式(0-2)", Order = 20)]
    public int ModeType = 0;
    [JsonProperty("追踪目标类型(0-4)", Order = 21)]
    public int TarType = 1;
    [JsonProperty("预测时间", Order = 22)]
    public float PredTime = 0f;
    [JsonProperty("追踪强度", Order = 23)]
    public float HomeStr = 1.0f;
    [JsonProperty("最大追踪角度", Order = 24)]
    public float MaxAngle = 45f;

    // 新增智能追踪参数
    [JsonProperty("智能预测", Order = 25)]
    public bool SmartPred { get; set; } = false;
    [JsonProperty("预测缩放", Order = 26)]
    public float PredScale { get; set; } = 1.0f;
    [JsonProperty("视线检查", Order = 27)]
    public bool CheckLoS { get; set; } = false;
    [JsonProperty("最小范围", Order = 28)]
    public float MinRange { get; set; } = 0f;
    [JsonProperty("最大范围", Order = 29)]
    public float MaxRange { get; set; } = 62f;
    [JsonProperty("优先Boss", Order = 30)]
    public bool FavBoss { get; set; } = false;
    [JsonProperty("动态强度", Order = 31)]
    public bool DynStr { get; set; } = false;
    [JsonProperty("视线模式(0-2)", Order = 32)]
    public int LoSMode { get; set; } = 0;
    [JsonProperty("检查频率", Order = 33)]
    public int LoSFreq { get; set; } = 1;
    [JsonProperty("视线容差", Order = 34)]
    public float LoSTol { get; set; } = 0f;
}
#endregion

public static class AutoHoming
{
    #region 应用追踪模式（优化版）
    public static Vector2 ApplyHomingMode(Vector2 vel, Entity proj, HomingData data, NPC npc, List<int> upList)
    {
        if (!data.Homing) return vel;

        // 根据配置选择追踪模式
        return data.ModeType switch
        {
            0 => ApplyHoming(vel, proj, data, npc, upList),           // 基础追踪
            1 => ApplyEdgeHoming(vel, proj, data, npc, upList),       // 边缘追踪
            2 => ApplyMultiPointHoming(vel, proj, data, npc, upList), // 多点追踪
            _ => ApplyHoming(vel, proj, data, npc, upList)            // 默认基础追踪
        };
    }
    #endregion

    #region 追踪模式核心
    private static Vector2 ApplyHoming(Vector2 vel, Entity proj, HomingData data, NPC npc, List<int> upList)
    {
        if (!data.Homing) return vel;

        // 获取目标
        Entity tar = GetTar(data.TarType, npc, proj);
        if (tar == null || !tar.active) return vel;

        // 使用碰撞箱感知的目标点
        Vector2 tarPos = GetTarPoint(proj, tar, data);

        // 范围检查
        if (!InRange(proj, tar, data))
            return vel;

        // 视线检查
        if (data.CheckLoS && !CheckLoS(proj, tar, data, StateUtil.GetState(npc)))
            return vel;

        // 智能预测
        if (data.SmartPred)
        {
            // 对碰撞箱感知的目标点进行预测
            tarPos = PredPos(proj, tar, data, tarPos);

            // 检查预测位置视线
            if (data.CheckLoS && !CheckLoS(proj, tarPos))
                tarPos = tar.Center;
        }
        else if (data.PredTime > 0f)
        {
            // 基础预测
            tarPos += tar.velocity * data.PredTime;
        }

        return CalcNewVel(vel, proj, tarPos, data, upList);
    }

    private static Vector2 CalcNewVel(Vector2 vel, Entity proj, Vector2 tarPos, HomingData up, List<int> upList)
    {
        Vector2 dir = tarPos - proj.Center;
        if (dir == Vector2.Zero) return vel;

        dir = dir.SafeNormalize(Vector2.Zero);

        // 计算角度差
        float currAng = (float)Math.Atan2(vel.Y, vel.X);
        float tarAng = (float)Math.Atan2(dir.Y, dir.X);
        float angDiff = tarAng - currAng;

        // 规范化角度差
        while (angDiff > Math.PI) angDiff -= (float)Math.PI * 2;
        while (angDiff < -Math.PI) angDiff += (float)Math.PI * 2;

        // 动态追踪强度
        float homeStr = up.HomeStr;
        if (up.DynStr)
        {
            float dist = Vector2.Distance(proj.Center, tarPos);
            homeStr = CalcDynStr(dist, up);
        }

        // 应用最大追踪角度限制
        float maxAng = MathHelper.ToRadians(up.MaxAngle);
        angDiff = MathHelper.Clamp(angDiff, -maxAng, maxAng);

        // 应用追踪强度
        float newAng = currAng + angDiff * homeStr;
        Vector2 newVel = new Vector2((float)Math.Cos(newAng), (float)Math.Sin(newAng)) * vel.Length();

        Add(upList, proj.whoAmI);
        return newVel;
    }
    #endregion

    #region 目标获取
    private static Entity GetTar(int tarType, NPC npc, Entity proj)
    {
        return tarType switch
        {
            0 => npc, // NPC自己
            1 => Main.player[npc.target], // 当前目标玩家
            2 => proj, // 弹幕自己
            3 => FindClosePlr(npc), // 最近玩家
            4 => FindBestTar(npc), // 最优目标
            _ => Main.player[npc.target]
        };
    }

    private static Player FindClosePlr(NPC npc)
    {
        Player closest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var plr = Main.player[i];
            if (!PxUtil.IsValidPlr(plr)) continue;

            float dist = Vector2.DistanceSquared(npc.Center, plr.Center);
            if (dist < minDist)
            {
                minDist = dist;
                closest = plr;
            }
        }

        return closest;
    }

    private static Player FindBestTar(Entity npc)
    {
        Player best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var plr = Main.player[i];
            if (!PxUtil.IsValidPlr(plr)) continue;

            float score = CalcTarScore(npc, plr);
            if (score > bestScore)
            {
                bestScore = score;
                best = plr;
            }
        }

        return best;
    }

    private static float CalcTarScore(Entity npc, Player plr)
    {
        float score = 0f;

        // 距离分数（越近分数越高）
        float dist = Vector2.Distance(npc.Center, plr.Center);
        score += 1000f / (dist + 1f);

        // Boss优先级
        if (IsFightBoss(plr))
            score += 500f;

        // 低生命值优先级
        float hpRatio = (float)plr.statLife / plr.statLifeMax2;
        score += (1f - hpRatio) * 300f;

        return score;
    }

    private static bool IsFightBoss(Entity plr)
    {
        // 检查玩家是否在与Boss战斗
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var npc = Main.npc[i];
            if (npc.active && npc.boss && PxUtil.InRange(plr.Center, npc.Center, 1000f))
            {
                return true;
            }
        }
        return false;
    }
    #endregion

    #region 范围检查
    private static bool InRange(Entity proj, Entity tar, HomingData data)
    {
        if (proj == null || tar == null) return false;

        float dist = Vector2.Distance(proj.Center, tar.Center);

        if (data.MinRange > 0)
        {
            float minPx = PxUtil.ToPx(data.MinRange);
            if (dist < minPx) return false;
        }

        if (data.MaxRange > 0)
        {
            float maxPx = PxUtil.ToPx(data.MaxRange);
            if (dist > maxPx) return false;
        }

        return true;
    }
    #endregion

    #region 视线检查
    private static bool CheckLoS(Entity proj, Entity tar, HomingData data, NpcState state)
    {
        if (data.LoSMode == 2) return true; // 无视线检查

        // 检查频率控制
        if (data.LoSFreq > 1 && state.ActiveTime % data.LoSFreq != 0)
            return true;

        bool hasSight = data.LoSMode switch
        {
            0 => Collision.CanHit(proj.Center, 0, 0, tar.Center, 0, 0),
            1 => Collision.CanHit(proj.position, proj.width, proj.height,
                                tar.position, tar.width, tar.height),
            _ => true
        };

        // 容差检查
        if (!hasSight && data.LoSTol > 0f)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = PxUtil.RandomOffset(data.LoSTol);
                Vector2 checkPos = tar.Center + offset;
                if (Collision.CanHitLine(proj.Center, 0, 0, checkPos, 0, 0))
                    return true;
            }
        }

        return hasSight;
    }

    private static bool CheckLoS(Entity proj, Vector2 tarPos)
    {
        return Collision.CanHitLine(proj.Center, 0, 0, tarPos, 0, 0);
    }
    #endregion

    #region 位置预测
    private static Vector2 PredPos(Entity proj, Entity tar, HomingData data, Vector2 startPos)
    {
        Vector2 tarPos = startPos;

        if (tar.velocity == Vector2.Zero)
            return tarPos;

        // 计算到达时间
        float dist = Vector2.Distance(proj.Center, tarPos);
        float projSpd = proj.velocity.Length();

        if (projSpd > 0)
        {
            float time = dist / projSpd;
            time *= data.PredScale;
            tarPos += tar.velocity * time;
        }

        return tarPos;
    }
    #endregion

    #region 动态强度计算
    private static float CalcDynStr(float dist, HomingData data)
    {
        float maxR = data.MaxRange > 0 ? data.MaxRange : 1000f;
        float normDist = MathHelper.Clamp(dist / maxR, 0f, 1f);

        // 距离越近强度越大
        float str = data.HomeStr * (1f - normDist * 0.7f);

        return MathHelper.Clamp(str, data.HomeStr * 0.3f, data.HomeStr);
    }
    #endregion

    #region 碰撞箱感知追踪系统
    private static Vector2 GetTarPoint(Entity proj, Entity tar, HomingData data)
    {
        if (tar == null) return Vector2.Zero;
        
        // 基础目标点是实体中心
        Vector2 pos = tar.Center;
        
        // 如果目标是玩家，考虑选择更容易命中的部位
        if (tar is Player plr)
        {
            // 根据弹幕速度方向选择目标点
            if (proj.velocity != Vector2.Zero)
            {
                Vector2 projDir = Vector2.Normalize(proj.velocity);
                Rectangle hitbox = plr.Hitbox;
                
                // 根据弹幕方向选择碰撞箱上的最近点
                if (Math.Abs(projDir.X) > Math.Abs(projDir.Y))
                {
                    // 水平方向为主
                    pos.X = projDir.X > 0 ? hitbox.Right : hitbox.Left;
                    pos.Y = hitbox.Center.Y;
                }
                else
                {
                    // 垂直方向为主
                    pos.X = hitbox.Center.X;
                    pos.Y = projDir.Y > 0 ? hitbox.Bottom : hitbox.Top;
                }
            }
        }
        else if (tar is NPC npc)
        {
            // 对NPC使用类似的逻辑
            Rectangle hitbox = npc.Hitbox;
            if (proj.velocity != Vector2.Zero)
            {
                Vector2 projDir = Vector2.Normalize(proj.velocity);
                
                if (Math.Abs(projDir.X) > Math.Abs(projDir.Y))
                {
                    pos.X = projDir.X > 0 ? hitbox.Right : hitbox.Left;
                    pos.Y = hitbox.Center.Y;
                }
                else
                {
                    pos.X = hitbox.Center.X;
                    pos.Y = projDir.Y > 0 ? hitbox.Bottom : hitbox.Top;
                }
            }
        }
        
        return pos;
    }
    #endregion

    #region 高级追踪模式
    // 碰撞箱边缘追踪模式
    public static Vector2 ApplyEdgeHoming(Vector2 vel, Entity proj, HomingData data, NPC npc, List<int> upList)
    {
        if (!data.Homing) return vel;

        Entity tar = GetTar(data.TarType, npc, proj);
        if (tar == null || !tar.active) return vel;

        // 获取碰撞箱边缘目标点
        Vector2 edgePos = GetEdgePoint(proj, tar, data);

        // 范围检查
        if (!InRange(proj, tar, data)) return vel;

        // 视线检查
        if (data.CheckLoS && !CheckLoS(proj, tar, data, StateUtil.GetState(npc)))
            return vel;

        // 应用追踪
        return CalcNewVel(vel, proj, edgePos, data, upList);
    }

    // 多目标点追踪模式
    public static Vector2 ApplyMultiPointHoming(Vector2 vel, Entity proj, HomingData data, NPC npc, List<int> upList)
    {
        if (!data.Homing) return vel;

        Entity tar = GetTar(data.TarType, npc, proj);
        if (tar == null || !tar.active) return vel;

        // 获取多个潜在目标点
        List<Vector2> points = GetMultiPoints(proj, tar, data);

        // 选择最优目标点（考虑视线和距离）
        Vector2 bestPoint = SelectBestPoint(proj, points, data);

        if (bestPoint == Vector2.Zero) return vel;

        // 应用追踪
        return CalcNewVel(vel, proj, bestPoint, data, upList);
    }

    // 获取碰撞箱边缘点
    private static Vector2 GetEdgePoint(Entity proj, Entity tar, HomingData data)
    {
        if (tar == null) return tar?.Center ?? Vector2.Zero;

        Rectangle hitbox = tar.Hitbox;
        Vector2 projDir = proj.velocity != Vector2.Zero ?
            Vector2.Normalize(proj.velocity) : Vector2.UnitX;

        // 根据弹幕方向选择碰撞箱边缘
        return projDir.X > 0 ?
            new Vector2(hitbox.Right, hitbox.Center.Y) :
            new Vector2(hitbox.Left, hitbox.Center.Y);
    }

    // 获取多个潜在目标点
    private static List<Vector2> GetMultiPoints(Entity proj, Entity tar, HomingData data)
    {
        var points = new List<Vector2>();
        if (tar == null) return points;

        Rectangle hitbox = tar.Hitbox;

        // 添加碰撞箱的四个边中点
        points.Add(new Vector2(hitbox.Center.X, hitbox.Top));    // 上
        points.Add(new Vector2(hitbox.Right, hitbox.Center.Y));  // 右
        points.Add(new Vector2(hitbox.Center.X, hitbox.Bottom)); // 下
        points.Add(new Vector2(hitbox.Left, hitbox.Center.Y));   // 左

        // 添加中心点
        points.Add(tar.Center);

        return points;
    }

    // 选择最优目标点
    private static Vector2 SelectBestPoint(Entity proj, List<Vector2> points, HomingData data)
    {
        Vector2 best = Vector2.Zero;
        float bestScore = float.MinValue;

        foreach (var point in points)
        {
            float score = CalcPointScore(proj, point, data);
            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    // 计算目标点得分
    private static float CalcPointScore(Entity proj, Vector2 point, HomingData data)
    {
        float score = 0f;

        // 距离得分（越近越好）
        float dist = Vector2.Distance(proj.Center, point);
        score += 100f / (dist + 1f);

        // 视线得分
        if (Collision.CanHitLine(proj.Center, 0, 0, point, 0, 0))
            score += 50f;

        // 方向对齐得分（与弹幕当前方向一致）
        if (proj.velocity != Vector2.Zero)
        {
            Vector2 toPoint = point - proj.Center;
            Vector2 projDir = Vector2.Normalize(proj.velocity);
            Vector2 pointDir = Vector2.Normalize(toPoint);

            float dot = Vector2.Dot(projDir, pointDir);
            score += dot * 30f;
        }

        return score;
    } 
    #endregion

    #region 工具方法
    private static void Add(List<int> list, int projId)
    {
        if (!list.Contains(projId))
        {
            list.Add(projId);
        }
    }
    #endregion
}