using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using static MonsterSpeed.PxUtil;

namespace MonsterSpeed;

#region 发射弹幕后:更新弹幕的追踪参数配置类
public class HomingData
{
    [JsonProperty("启用追踪", Order = 1)]
    public bool Homing = false;
    [JsonProperty("最近玩家", Order = 2)]
    public bool TrackNear = false;  // true:追踪最近玩家, false:追踪仇恨目标
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
}
#endregion

public static class AutoHoming
{
    #region 处理追踪与躲避模式
    public static Vector2 ApplyAll(Vector2 v, Projectile p, HomingData d, NpcState st, NPC n, List<int> lst)
    {
        if (!d.Homing) return v;

        // 获取目标实体
        Entity? tar = GetTar(n, d, st);
        if (tar == null) return v;

        // 追踪
        return Track(v, p, d, n, lst, tar);
    }

    private static Entity? GetTar(NPC n, HomingData d, NpcState st)
    {
        if (d.TrackNear)
        {
            // 追踪最近攻击者模式
            if (st != null && st.Attack.Count > 0)
            {
                Player? near = null;
                float minD2 = float.MaxValue;
                float maxR = d.MaxRange * 16f;
                float maxD2 = maxR * maxR;

                for (int i = st.Attack.Count - 1; i >= 0; i--)
                {
                    int pid = st.Attack[i];
                    var plr = Main.player[pid];
                    if (plr == null || !plr.active || plr.dead)
                    {
                        st.Attack.RemoveAt(i);
                        continue;
                    }

                    float dsq = n.DistanceSQ(plr.Center);
                    if (dsq < minD2 && dsq <= maxD2)
                    {
                        minD2 = dsq;
                        near = plr;
                    }
                }
                if (near != null) 
                    return near;
            }

            // 保底：追踪仇恨目标
            return GetTarg(n);
        }
        else
        {
            // 保底：追踪仇恨目标
            return GetTarg(n);
        }
    }
    #endregion

    #region 追踪模式核心
    private static Vector2 Track(Vector2 v, Entity p, HomingData d, NPC n, List<int> lst, Entity tar)
    {
        Vector2 tarPos = tar.Center;

        // 预测
        if (d.PredTime > 0f)
            tarPos += tar.velocity * (d.PredTime / 60f);

        // 视线检查
        if (d.CheckLoS && !Collision.CanHitLine(tarPos, 1, 1, n.Center, 1, 1))
            return v;

        Vector2 dir = tarPos - p.Center;
        if (dir == Vector2.Zero) return v;

        dir = dir.SafeNormalize(Vector2.Zero);
        float curAng = (float)Math.Atan2(v.Y, v.X);
        float tarAng = (float)Math.Atan2(dir.Y, dir.X);
        float angDiff = tarAng - curAng;

        while (angDiff > Math.PI) angDiff -= (float)Math.PI * 2;
        while (angDiff < -Math.PI) angDiff += (float)Math.PI * 2;

        float maxAng = MathHelper.ToRadians(d.MaxAngle);
        angDiff = MathHelper.Clamp(angDiff, -maxAng, maxAng);

        float newAng = curAng + angDiff * d.HomeStr;
        Vector2 newV = new Vector2((float)Math.Cos(newAng), (float)Math.Sin(newAng)) * v.Length();

        UpProj.Add(lst, p.whoAmI);
        return newV;
    }
    #endregion
}