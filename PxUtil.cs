using Microsoft.Xna.Framework;
using System;
using System.Text;
using TShockAPI;
using Terraria;

namespace MonsterSpeed;

/// <summary>
/// 像素转换和游戏实体工具类
/// 提供像素与格数的转换、实体验证、距离计算等常用功能
/// </summary>
public static class PxUtil
{
    #region 基础转换方法
    // 格数转像素
    public static float ToPx(float tiles) => tiles * 16f;
    // 格数转像素
    public static Vector2 ToPx(Vector2 tiles) => tiles * 16f;
    // 格数转像素
    public static int ToPx(int tiles) => tiles * 16;
    // 像素转格数
    public static float ToTiles(float pixels) => pixels / 16f;
    // 像素转格数
    public static Vector2 ToTiles(Vector2 pixels) => pixels / 16f;
    // 像素转格数
    public static int ToTiles(int pixels) => pixels / 16;
    #endregion

    #region 扩展方法
    // 扩展方法：格数转像素
    public static Vector2 ToPx2(this Vector2 tiles) => PxUtil.ToPx(tiles);
    // 扩展方法：格数转像素
    public static float ToPx2(this float tiles) => PxUtil.ToPx(tiles);
    // 扩展方法：格数转像素
    public static int ToPx2(this int tiles) => PxUtil.ToPx(tiles);
    // 扩展方法：像素转格数
    public static Vector2 ToTiles2(this Vector2 pixels) => PxUtil.ToTiles(pixels);
    // 扩展方法：像素转格数
    public static float ToTiles2(this float pixels) => PxUtil.ToTiles(pixels);
    // 扩展方法：像素转格数
    public static int ToTiles2(this int pixels) => PxUtil.ToTiles(pixels);
    // 扩展方法：检查是否在像素范围内
    public static bool IsInRangeOf(this Vector2 from, Vector2 to, float rangePx) => 
        InRange(from, to, rangePx);
    // 扩展方法：检查是否在格数范围内
    public static bool IsInRangeOfTiles(this Vector2 from, Vector2 to, float rangeTiles) => 
        InRangeTiles(from, to, rangeTiles);
    #endregion

    #region 数学工具方法
    // 线性插值
    public static float Lerp(float from, float to, float amount) => MathHelper.Lerp(from, to, amount);
    // 限制浮点数范围
    public static float Clamp(float value, float min, float max) => MathHelper.Clamp(value, min, max);
    // 限制整数范围
    public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
    // 平滑插值
    public static float SmoothStep(float min, float max, float x) => Terraria.Utils.SmoothStep(min, max, x);
    // 获取插值比例
    public static float GetLerpValue(float from, float to, float t, bool clamped = false) => 
        Terraria.Utils.GetLerpValue(from, to, t, clamped);
    #endregion

    #region 距离计算
    // 计算两点间距离平方（性能更好）
    public static float DistanceSquared(Vector2 from, Vector2 to) => Vector2.DistanceSquared(from, to);
    // 计算两点间实际距离
    public static float Distance(Vector2 from, Vector2 to) => Vector2.Distance(from, to);
    // 计算从起点到目标点的角度
    public static float AngleTo(Vector2 from, Vector2 to) => (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
    #endregion

    #region 范围检查
    // 检查两点是否在指定像素范围内
    public static bool InRange(Vector2 from, Vector2 to, float rangePx) => 
        Vector2.DistanceSquared(from, to) <= rangePx * rangePx;
    
    // 检查两点是否在指定格数范围内
    public static bool InRangeTiles(Vector2 from, Vector2 to, float rangeTiles) => 
        InRange(from, to, ToPx(rangeTiles));
    
    // 使用平方距离检查范围（避免开方运算）
    public static bool InRangeSquared(Vector2 from, Vector2 to, float rangePx) => 
        Vector2.DistanceSquared(from, to) <= rangePx * rangePx;
    #endregion

    #region 方向计算
    
    // 获取从起点指向目标点的方向向量
    public static Vector2 DirectionTo(Vector2 from, Vector2 to)
    {
        if (from == to) return Vector2.UnitX;
        return Vector2.Normalize(to - from);
    }
    
    // 安全获取方向向量（避免除零错误）
    public static Vector2 SafeDirectionTo(Vector2 from, Vector2 to, Vector2 defaultValue = default) => 
        (to - from).SafeNormalize(defaultValue);
    
    #endregion

    #region 实体验证
    // 验证NPC是否有效
    public static bool IsValidMst(NPC mst, NPC excludeNpc = null!) => 
        mst?.active == true && (excludeNpc == null || mst.whoAmI != excludeNpc.whoAmI);
    
    // 验证玩家是否有效（Player对象）
    public static bool IsValidPlr(Player plr) => 
        plr?.active == true && !plr.dead && plr.statLife > 0;
    
    // 验证玩家是否有效（TSPlayer对象）
    public static bool IsValidPlr(TSPlayer plr) => 
        plr?.Active == true && !plr.Dead && plr.TPlayer != null && plr.TPlayer.statLife > 0;
    
    // 验证抛射物是否有效
    public static bool IsValidProj(Projectile proj) => 
        proj?.active == true && proj.owner == Main.myPlayer;
    
    // 验证实体是否有效
    public static bool IsValidEntity(Entity entity) => entity?.active == true;
    #endregion

    #region 生命值计算
    // 获取NPC生命值百分比
    public static float GetLifeRatio(NPC npc)
    {
        if (npc?.active != true || npc.lifeMax <= 0) return 0f;
        return MathHelper.Clamp(npc.life * 100f / npc.lifeMax, 0f, 100f);
    }
    
    // 获取玩家生命值百分比
    public static float GetLifeRatio(Player plr)
    {
        if (plr?.active != true || plr.statLifeMax2 <= 0) return 0f;
        return MathHelper.Clamp(plr.statLife * 100f / plr.statLifeMax2, 0f, 100f);
    }
    #endregion

    #region 向量和位置工具
    
    // 生成随机偏移向量（对称范围）
    public static Vector2 RandomOffset(float maxOffset)
    {
        if (maxOffset <= 0) return Vector2.Zero;
        return new Vector2(
            Main.rand.Next((int)-maxOffset, (int)maxOffset),
            Main.rand.Next((int)-maxOffset, (int)maxOffset)
        );
    }
    
    // 生成随机偏移向量（指定范围）
    public static Vector2 RandomOffset(float minOffset, float maxOffset)
    {
        if (minOffset > maxOffset) 
        {
            float temp = minOffset;
            minOffset = maxOffset;
            maxOffset = temp;
        }
        return new Vector2(
            Main.rand.Next((int)minOffset, (int)maxOffset),
            Main.rand.Next((int)minOffset, (int)maxOffset)
        );
    }
    
    // 预测实体未来位置
    public static Vector2 PredictPosition(Vector2 currentPos, Vector2 velocity, float time) => 
        currentPos + velocity * Math.Max(0, time);
    
    // 旋转向量
    public static Vector2 RotateVector(Vector2 vector, float radians) => vector.RotatedBy(radians);
    
    // 获取实体中心位置
    public static Vector2 GetEntityCenter(Entity entity) => entity?.Center ?? Vector2.Zero;
    
    // 获取实体碰撞箱
    public static Rectangle GetEntityHitbox(Entity entity) => entity?.Hitbox ?? new Rectangle(0, 0, 0, 0);
    
    #endregion

    #region 世界边界检查
    
    // 检查位置是否在世界边界内
    public static bool InWorldBounds(Vector2 position, float margin = 0f) => 
        position.X >= margin && position.X <= Main.maxTilesX * 16f - margin &&
        position.Y >= margin && position.Y <= Main.maxTilesY * 16f - margin;
    
    // 检查格数位置是否在世界边界内
    public static bool InWorldBoundsTiles(Vector2 tilePosition, float margin = 0f) => 
        tilePosition.X >= margin && tilePosition.X <= Main.maxTilesX - margin &&
        tilePosition.Y >= margin && tilePosition.Y <= Main.maxTilesY - margin;
    
    // 将位置限制在世界边界内
    public static Vector2 ClampToWorld(Vector2 position, float margin = 16f)
    {
        float maxX = Main.maxTilesX * 16f;
        float maxY = Main.maxTilesY * 16f;
        return new Vector2(
            MathHelper.Clamp(position.X, margin, maxX - margin),
            MathHelper.Clamp(position.Y, margin, maxY - margin)
        );
    }
    
    #endregion

    #region 游戏逻辑工具
    
    // 获取NPC的有效目标玩家
    public static Player GetValidTarget(NPC npc)
    {
        if (npc?.active != true || npc.target < 0 || npc.target >= Main.maxPlayers) return null;
        var plr = Main.player[npc.target];
        return IsValidPlr(plr) ? plr : null;
    }
    
    // 检查玩家生命值条件
    public static bool CheckHPCondition(Player plr, int hpValue, int hpRatio) => 
        plr != null && CheckHPCondition(hpValue, hpRatio, plr.statLife, plr.statLifeMax2);
    
    // 检查TSPlayer生命值条件
    public static bool CheckHPCondition(TSPlayer plr, int hpValue, int hpRatio) => 
        plr?.TPlayer != null && CheckHPCondition(hpValue, hpRatio, plr.TPlayer.statLife, plr.TPlayer.statLifeMax2);
    
    // 检查数量条件并记录消息
    public static bool CheckCountCondition(int required, int actual, string typeName, StringBuilder message = null)
    {
        if (required == 0) return true;
        
        if (required > 0 && actual < required)
        {
            message?.AppendLine($" {typeName}数量不足: 需要{required}个, 当前{actual}个");
            return false;
        }
        
        if (required < 0 && actual >= Math.Abs(required))
        {
            message?.AppendLine($" {typeName}数量过多: 需要少于{Math.Abs(required)}个, 当前{actual}个");
            return false;
        }
        
        return true;
    }
    
    #endregion

    #region 复杂工具方法
    // 解析范围字符串（如 "1,10"）
    public static (bool success, T min, T max) ParseRange<T>(string rangeStr, Func<string, T> parser) where T : IComparable<T>
    {
        if (string.IsNullOrWhiteSpace(rangeStr)) return (false, default, default)!;
            
        var parts = rangeStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return (false, default, default)!;
        
        try
        {
            T val1 = parser(parts[0].Trim());
            T val2 = parser(parts[1].Trim());
            return (true, 
                val1.CompareTo(val2) <= 0 ? val1 : val2, 
                val1.CompareTo(val2) >= 0 ? val1 : val2);
        }
        catch
        {
            return (false, default, default)!;
        }
    }
    
    // 解析浮点数范围
    public static (bool success, float min, float max) ParseFloatRange(string rangeStr) => 
        ParseRange(rangeStr, float.Parse);
    
    // 解析整数范围
    public static (bool success, int min, int max) ParseIntRange(string rangeStr) => 
        ParseRange(rangeStr, int.Parse);
    
    // 内部方法：检查生命值条件
    private static bool CheckHPCondition(int hpValue, int hpRatio, int currentHP, int maxHP)
    {
        if (maxHP <= 0) return false;

        bool hpOk = hpValue == -1 || 
                   (hpValue > 0 ? currentHP >= hpValue : currentHP < Math.Abs(hpValue));

        bool ratioOk = true;
        if (hpRatio != -1)
        {
            float currentRatio = currentHP * 100f / maxHP;
            ratioOk = hpRatio > 0 ? currentRatio >= hpRatio : currentRatio < Math.Abs(hpRatio);
        }

        return hpOk && ratioOk;
    }
    
    #endregion
}