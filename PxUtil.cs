using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

public static class PxUtil
{
    /// <summary>将 "x,y" 格数字符串转为像素 Vector2</summary>
    public static Vector2 GetVector2(this string str, Vector2 def = default)
    {
        if (string.IsNullOrWhiteSpace(str) || str == "0,0") return def;
        try
        {
            var parts = str.Split(',');
            if (parts.Length != 2) return def;
            float x = float.Parse(parts[0].Trim());
            float y = float.Parse(parts[1].Trim());
            return new Vector2(x * 16f, y * 16f);
        }
        catch { return def; }
    }

    /// <summary>解析浮点范围 "min,max"</summary>
    public static (bool ok, float min, float max) ParseRng(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (false, 0, 0);
        var parts = s.Split(',');
        if (parts.Length != 2) return (false, 0, 0);
        if (float.TryParse(parts[0].Trim(), out float a) && float.TryParse(parts[1].Trim(), out float b))
            return (true, Math.Min(a, b), Math.Max(a, b));
        return (false, 0, 0);
    }

    /// <summary>解析整数范围 "min,max"</summary>
    public static (bool ok, int min, int max) ParseRngInt(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (false, 0, 0);
        var parts = s.Split(',');
        if (parts.Length != 2) return (false, 0, 0);
        if (int.TryParse(parts[0].Trim(), out int a) && int.TryParse(parts[1].Trim(), out int b))
            return (true, Math.Min(a, b), Math.Max(a, b));
        return (false, 0, 0);
    }

    // 获取有效的仇恨目标玩家（活跃且未死亡）
    public static Player? GetTarg(NPC n)
    {
        int tid = n.target;
        if (tid >= 0 && tid < Main.maxPlayers)
        {
            var plr = Main.player[tid];
            if (plr != null && plr.active && !plr.dead)
                return plr;
        }
        return null;
    }

    // 查找安全传送位置（修正随机数生成）
    public static Vector2 FindPos(Vector2 cen, float dic)
    {
        // 最多尝试10次寻找可用位置
        for (int i = 0; i < 10; i++)
        {
            float minOff = dic * 0.8f;      // 最小偏移量（像素）
            float maxOff = dic * 1.2f;      // 最大偏移量（像素）

            // 随机生成X偏移量，方向随机（正或负）
            float offX = (minOff + Main.rand.NextFloat() * (maxOff - minOff)) * (Main.rand.Next(2) == 0 ? -1 : 1);
            // 随机生成Y偏移量，方向随机（正或负）
            float offY = (minOff + Main.rand.NextFloat() * (maxOff - minOff)) * (Main.rand.Next(2) == 0 ? -1 : 1);

            Vector2 testPos = cen + new Vector2(offX, offY); // 计算测试位置
            if (InWorld(testPos) && IsSafe(testPos))        // 检查是否在世界内且安全
                return testPos;                             // 成功则返回该位置
        }

        // 保底位置：中心偏移(dic, dic)
        Vector2 fallPos = cen + new Vector2(dic, dic);
        return InWorld(fallPos) ? fallPos : cen;            // 若保底位置在世界内则返回，否则返回原中心
    }

    // 检查传送位置安全性
    public static bool IsSafe(Vector2 pos)
    {
        Point tilePos = pos.ToTileCoordinates();            // 将像素坐标转为瓦片坐标
                                                            // 检查瓦片坐标是否超出世界范围（不超出0～maxTiles边界）
        if (tilePos.X < 0 || tilePos.X >= Main.maxTilesX || tilePos.Y < 0 || tilePos.Y >= Main.maxTilesY)
            return false;                                   // 超出则不安全

        ITile tile = Main.tile[tilePos.X, tilePos.Y];      // 获取该位置的瓦片实例
                                                           // 瓦片存在且为非固体活动瓦片时认为不安全（玩家不能站在固体块里）
        return tile != null && !(tile.active() && WorldGen.SolidTile(tile));
    }

    // 使用 WorldGen.InWorld 替换的边界检查
    public static bool InWorld(Vector2 pos, float mar = 16f) 
    {
        // 将像素边距转换为瓦片格数（向上取整，确保至少1格）
        int fluff = (int)Math.Ceiling(mar / 16f);

        int tileX = (int)(pos.X / 16);                      // 像素X转瓦片X坐标
        int tileY = (int)(pos.Y / 16);                      // 像素Y转瓦片Y坐标

        // 调用原版WorldGen.InWorld，检查(瓦片X,瓦片Y)是否在有效区域内
        return WorldGen.InWorld(tileX, tileY, fluff);
    }

    /// <summary>查找下一个有效的弹幕组</summary>
    public static int FindNxt(List<SpawnProjData> projs, int start)
    {
        if (projs == null || projs.Count == 0) return -1;
        for (int i = 0; i < projs.Count; i++)
        {
            int idx = (start + i) % projs.Count;
            if (projs[idx].Type > 0) return idx;
        }
        return -1;
    }

    /// <summary>获取基础速度向量</summary>
    public static Vector2 GetVel(SpawnProjData d, NPC npc, NPCAimedTarget tar)
    {
        Vector2 vel = d.VelXY.GetVector2();
        if (vel != Vector2.Zero) return vel;
        if (d.Velocity > 0)
        {
            var dir = tar.Center - npc.Center;
            if (dir != Vector2.Zero)
                vel = dir.SafeNormalize(Vector2.Zero) * d.Velocity;
        }
        if (!string.IsNullOrWhiteSpace(d.SpdFix) && d.SpdFix != "0,0")
            vel += d.SpdFix.GetVector2();
        return vel;
    }

    /// <summary>应用角度配置（支持单值或范围）</summary>
    public static Vector2 ApplyAng(Vector2 v, string cfg, int idx, int total)
    {
        if (string.IsNullOrWhiteSpace(cfg) || cfg == "0" || v == Vector2.Zero) return v;
        try
        {
            string[] parts = cfg.Split(',');
            if (parts.Length == 1 && float.TryParse(parts[0], out float a))
                return v.RotatedBy(MathHelper.ToRadians(a));
            if (parts.Length == 2 && float.TryParse(parts[0], out float min) && float.TryParse(parts[1], out float max))
            {
                if (total > 0 && idx >= 0 && idx < total)
                {
                    float range = max - min;
                    float step = range / Math.Max(1, total - 1);
                    float ang = min + step * idx;
                    return v.RotatedBy(MathHelper.ToRadians(ang));
                }
                else
                {
                    float ang = (min + max) / 2f;
                    return v.RotatedBy(MathHelper.ToRadians(ang));
                }
            }
        }
        catch { }
        return v;
    }

    /// <summary>速度修正+锁定目标重定向</summary>
    public static Vector2 FixVel(SpawnProjData d, Vector2 pos, Vector2 vel, Vector2 tgt, int idx)
    {
        if (!d.UseProjPos || tgt == Vector2.Zero) return vel;
        Vector2 toTgt = tgt - pos;
        if (toTgt == Vector2.Zero) return vel;
        float spd = d.LockSpd > 0 ? d.LockSpd : vel.Length();
        Vector2 nv = toTgt.SafeNormalize(Vector2.Zero) * spd;
        if (!string.IsNullOrWhiteSpace(d.SpdFix) && d.SpdFix != "0,0")
            nv += d.SpdFix.GetVector2();
        return ApplyAng(nv, d.AngleCfg, idx, d.Stack);
    }
}