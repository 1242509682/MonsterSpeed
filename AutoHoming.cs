using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;

namespace MonsterSpeed;

#region 发射弹幕后:更新弹幕的追踪参数配置类
public class HomingData
{
    [JsonProperty("追踪目标说明", Order = 0)]
    public string Mess = "0仇恨对象,1附近怪物,2附近弹幕,3附近玩家";
    [JsonProperty("启用追踪", Order = 1)]
    public bool Homing = false;
    [JsonProperty("追踪目标", Order = 2)]
    public int TarType = 0;
    [JsonProperty("预测时间", Order = 3)]
    public float PredTime = 0f;
    [JsonProperty("追踪强度", Order = 4)]
    public float HomeStr = 1.0f;
    [JsonProperty("最大角度", Order = 5)]
    public float MaxAngle = 45f;
    [JsonProperty("最大范围", Order = 6)]
    public float MaxRange { get; set; } = 62f;
    [JsonProperty("视线检查", Order = 7)]
    public bool CheckLoS { get; set; } = false;
    [JsonProperty("躲避弹幕", Order = 8)]
    public bool Avoid { get; set; } = false;
    [JsonProperty("躲避范围", Order = 9)]
    public float AvoidRange { get; set; } = 5f;
}
#endregion

public static class AutoHoming
{
    #region 处理追踪与躲避模式
    public static Vector2 ApplyAll(Vector2 v, Projectile p, HomingData d, NPC n, List<int> lst)
    {
        Entity tar = null;

        // 获取目标
        tar = GetTar(d.TarType, n, p, d.MaxRange);

        if (tar is null || !tar.active) return v;

        // 执行顺序
        if (d.Avoid && d.Homing)
        {
            // 先追踪
            v = Track(v, p, d, n, lst, tar);
            // 再躲避（知道目标）
            v = Evade(v, p, d, lst, tar);
        }
        else if (d.Homing)
        {
            v = Track(v, p, d, n, lst, tar);
        }
        else if (d.Avoid)
        {
            v = Evade(v, p, d, lst);
        }

        return v;
    }
    #endregion

    #region 追踪模式核心
    public static Vector2 Track(Vector2 v, Entity p, HomingData d, NPC n, List<int> lst, Entity tar = null)
    {
        if (!d.Homing || tar == null) return v;

        // 计算附近同类弹幕
        int cnt = 0;
        float rngSq = d.MaxRange * d.MaxRange;
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile o = Main.projectile[i];
            if (!o.active || o.whoAmI == p.whoAmI) continue;
            if (o.type == (p as Projectile)?.type && p.DistanceSQ(o.Center) < rngSq)
                cnt++;
        }

        // 目标点（考虑分散）
        Vector2 tarPos = GetTarPt(p, tar, d, cnt);

        // 视线检查
        if (d.CheckLoS &&
            !Collision.CanHitLine(p.Center, 0, 0, tarPos, 0, 0))
            return v;

        // 预测时间
        if (d.PredTime > 0f)
            tarPos += tar.velocity * (d.PredTime / 60f);

        return CalcVel(v, p, tarPos, d, lst);
    }

    private static Vector2 GetTarPt(Entity p, Entity tar, HomingData d, int nearCnt)
    {
        Vector2 pos = tar.Center;

        // 如果有多个同类弹幕，分散目标点
        if (nearCnt > 0)
        {
            // 根据弹幕索引计算角度
            float ang = p.whoAmI % Math.Max(nearCnt, 1) * (MathHelper.TwoPi / Math.Max(nearCnt, 1));
            float offset = Math.Min(nearCnt * 10f, d.MaxRange * 0.3f);

            pos.X += (float)Math.Cos(ang) * offset;
            pos.Y += (float)Math.Sin(ang) * offset;
        }

        return pos;
    }

    private static Vector2 CalcVel(Vector2 v, Entity p, Vector2 tarPos, HomingData d, List<int> lst)
    {
        Vector2 dir = tarPos - p.Center;
        if (dir == Vector2.Zero) return v;

        dir = dir.SafeNormalize(Vector2.Zero);

        // 计算角度差
        float curAng = (float)Math.Atan2(v.Y, v.X);
        float tarAng = (float)Math.Atan2(dir.Y, dir.X);
        float angDiff = tarAng - curAng;

        // 规范化角度差
        while (angDiff > Math.PI) angDiff -= (float)Math.PI * 2;
        while (angDiff < -Math.PI) angDiff += (float)Math.PI * 2;

        // 最大角度限制
        float maxAng = MathHelper.ToRadians(d.MaxAngle);
        angDiff = MathHelper.Clamp(angDiff, -maxAng, maxAng);

        // 应用追踪强度
        float newAng = curAng + angDiff * d.HomeStr;
        Vector2 newV = new Vector2((float)Math.Cos(newAng), (float)Math.Sin(newAng)) * v.Length();

        UpProj.Add(lst, p.whoAmI);
        return newV;
    }
    #endregion

    #region 目标获取
    private static Entity GetTar(int type, NPC n, Entity p, float rng)
    {
        return type switch
        {
            0 => Main.player[n.target], // 当前目标玩家
            1 => FindMst(n, rng), // 最近怪物
            2 => FindProj(p, rng), // 最近弹幕
            3 => FindPlr(n, rng), // 最近玩家
            _ => Main.player[n.target]
        };
    }

    private static Player FindPlr(NPC n, float rng)
    {
        Player pl = null;
        float min = float.MaxValue;
        float rngSq = rng * rng;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var p = Main.player[i];
            if (!PxUtil.IsValidPlr(p)) continue;

            float dSq = n.DistanceSQ(p.Center);
            if (dSq < min && dSq <= rngSq)
            {
                min = dSq;
                pl = p;
            }
        }

        return pl;
    }

    private static Projectile FindProj(Entity src, float rng)
    {
        Projectile proj = null;
        float min = float.MaxValue;
        float rngSq = rng * rng;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile p = Main.projectile[i];
            if (!p.active || p.whoAmI == src.whoAmI)
                continue;

            float dSq = src.DistanceSQ(p.Center);
            if (dSq < min && dSq <= rngSq)
            {
                min = dSq;
                proj = p;
            }
        }

        return proj;
    }

    public static NPC FindMst(NPC n, float rng)
    {
        NPC target = null;
        float min = float.MaxValue;
        float rngSq = rng * rng;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var t = Main.npc[i];

            if (!PxUtil.IsValidMst(t, n))
                continue;

            float dSq = n.DistanceSQ(t.Center);
            if (dSq < min && dSq <= rngSq)
            {
                min = dSq;
                target = t;
            }
        }

        return target;
    }
    #endregion

    #region 躲避系统
    private static Vector2 Evade(Vector2 v, Projectile p, HomingData d, List<int> lst, Entity tar = null)
    {
        Vector2 f = Vector2.Zero;
        int cnt = 0;
        float rng = PxUtil.ToPx(d.AvoidRange);
        float rngSq = rng * rng;

        // 统计附近弹幕密度
        List<Vector2> nearPos = new List<Vector2>();

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile o = Main.projectile[i];
            if (!o.active || o.whoAmI == p.whoAmI) continue;

            float dSq = p.DistanceSQ(o.Center);
            if (dSq < rngSq && dSq > 0)
            {
                Vector2 dir = (p.Center - o.Center).SafeNormalize(Vector2.Zero);
                float dist = (float)Math.Sqrt(dSq);
                float str = 1f - (dist / rng);

                // 存储位置
                nearPos.Add(o.Center);

                // 如果有目标，检查是否朝向同一目标
                if (tar != null)
                {
                    Vector2 toTar = (tar.Center - p.Center).SafeNormalize(Vector2.Zero);
                    if (toTar != Vector2.Zero && dir != Vector2.Zero)
                    {
                        float dot = Vector2.Dot(toTar, dir);
                        if (dot > 0.7f) str *= 2f; // 同方向加倍排斥
                        else if (dot < -0.7f) str *= 0.5f; // 反方向减半排斥
                    }
                }

                f += dir * str;
                cnt++;
            }
        }

        if (cnt > 0)
        {
            f /= cnt;

            // 计算密度
            float density = 0f;
            foreach (Vector2 pos in nearPos)
            {
                float dist = Vector2.Distance(p.Center, pos);
                density += 1f - (dist / rng);
            }
            density /= nearPos.Count;

            // 动态权重：密度越高，躲避影响越大
            float weight = (tar != null) ? 0.2f : 0.4f;
            weight *= (1f + density * 0.5f);
            weight = MathHelper.Clamp(weight, 0.1f, 0.8f);

            // 混合方向
            Vector2 curDir = v.SafeNormalize(Vector2.Zero);
            if (curDir == Vector2.Zero) curDir = f.SafeNormalize(Vector2.Zero);

            Vector2 newDir = (curDir * (1f - weight) + f * weight).SafeNormalize(Vector2.Zero);

            if (newDir != Vector2.Zero)
            {
                v = newDir * v.Length();
                UpProj.Add(lst, p.whoAmI);
            }
        }

        return v;
    }
    #endregion
}