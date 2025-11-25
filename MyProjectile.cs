using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.DataStructures;
using TShockAPI;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

#region 弹幕数据
public class SpawnProjData
{
    [JsonProperty("弹幕ID", Order = 0)]
    public int Type = 115;
    [JsonProperty("数量", Order = 1)]
    public int Stack = 5;
    [JsonProperty("间隔/帧", Order = 2)]
    public float Interval = 30f;
    [JsonProperty("持续时间/帧", Order = 3)]
    public int Life = 120;
    [JsonProperty("射弹条件", Order = 4)]
    public Conditions EnhConds { get; set; } = new();
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
    public TarLockParams LockParams { get; set; } = new();
    [JsonProperty("指示物修改", Order = 72)]
    public Dictionary<string, string[]> MarkerMods { get; set; } = new();
    [JsonProperty("指示物注入AI", Order = 73)]
    public Dictionary<int, string> MarkerToAI { get; set; } = new();
    [JsonProperty("更新间隔/毫秒", Order = 80)]
    public double UpdateTime = 500f;
    [JsonProperty("更新弹幕", Order = 81)]
    public List<UpdateProjData> UpdateProj { get; set; } = new();
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
            basePos = ApplyRadiusOffset(basePos, state);
        }

        return basePos;
    }
    #endregion

    #region 应用半径偏移
    private Vector2 ApplyRadiusOffset(Vector2 basePos, NpcState state)
    {
        var radiusPx = PxUtil.ToPx(Radius);
        var angle = state.SendStack[state.Index] / (float)(Stack - 1) * MathHelper.TwoPi;
        var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radiusPx;

        return basePos + offset;
    }
    #endregion
}
#endregion

#region 更新弹幕数据
public class UpdateProjData
{
    [JsonProperty("新弹幕ID(-1恢复,0不更新,>0替换)", Order = -2)]
    public int NewType = 0;
    [JsonProperty("延长更新间隔", Order = -1)]
    public double StageInterval { get; set; } = 0f;
    [JsonProperty("延长持续时间", Order = 0)]
    public int ExtraTime = 0;
    [JsonProperty("更新条件", Order = 1)]
    public Conditions Condition { get; set; } = new Conditions() { NpcLift = "0,100" };
    [JsonProperty("速度", Order = 2)]
    public float Velocity = 0f;
    [JsonProperty("速度向量XY/格", Order = 3)]
    public string Velocity_XY { get; set; } = "0,0";
    [JsonProperty("半径格数", Order = 4)]
    public float Radius = 0f;
    [JsonProperty("位置偏移XY/格", Order = 5)]
    public string PosOffset_XY { get; set; } = "0,0";
    [JsonProperty("偏移角度", Order = 6)]
    public float Angle = 0f;
    [JsonProperty("旋转角度", Order = 7)]
    public float Rotate = 0f;
    // 简化的追踪模式
    [JsonProperty("启用追踪", Order = 20)]
    public bool Homing = false;
    [JsonProperty("追踪目标类型(0-3)", Order = 21)]
    public int TarType = 1; // 0:NPC 1:玩家 2:弹幕 3:最近玩家
    [JsonProperty("预测时间", Order = 22)]
    public float PredictTime = 0f;
    [JsonProperty("追踪强度", Order = 23)]
    public float HomingStrength = 1.0f;
    [JsonProperty("最大追踪角度", Order = 24)]
    public float MaxHomingAngle = 45f;
    [JsonProperty("弹幕AI", Order = 50)]
    public Dictionary<int, float> AI { get; set; } = new Dictionary<int, float>();
    [JsonProperty("速度注入AI", Order = 51)]
    public Dictionary<int, int> SpeedToAI { get; set; } = new Dictionary<int, int>();
    [JsonProperty("注入后速度向量XY/格", Order = 52)]
    public string SpeedToAIVel_XY { get; set; } = "0,0";
    [JsonProperty("指示物修改", Order = 54)]
    public Dictionary<string, string[]> MarkerMods { get; set; } = new();
    [JsonProperty("指示物注入AI", Order = 55)]
    public Dictionary<int, string> MarkerToAI { get; set; } = new Dictionary<int, string>();

    #region 计算最终速度（使用XY分离格式）
    public Vector2 GetFinalVelocity(Projectile proj)
    {
        Vector2 newVel = proj.velocity;

        // 优先使用速度向量字符串
        if (!string.IsNullOrWhiteSpace(Velocity_XY) && Velocity_XY != "0,0")
        {
            var Result = PxUtil.ParseFloatRange(Velocity_XY);
            if (Result.success)
            {
                newVel = PxUtil.ToPx(new Vector2(Result.min, Result.max));
            }
        }
        // 使用标量速度
        else if (Velocity != 0f)
        {
            newVel = newVel.SafeNormalize(Vector2.Zero) * Velocity;
        }

        return newVel;
    }
    #endregion

    #region 获取位置偏移向量
    public Vector2 GetPosOffsetVector()
    {
        if (string.IsNullOrWhiteSpace(PosOffset_XY) || PosOffset_XY == "0,0")
            return Vector2.Zero;

        var Result = PxUtil.ParseFloatRange(PosOffset_XY);
        if (!Result.success)
            return Vector2.Zero;

        return PxUtil.ToPx(new Vector2(Result.min, Result.max));
    }
    #endregion

    #region 获取速度注入向量
    public Vector2 GetSpeedToAIVector()
    {
        if (string.IsNullOrWhiteSpace(SpeedToAIVel_XY) || SpeedToAIVel_XY == "0,0")
            return Vector2.Zero;

        var Result = PxUtil.ParseFloatRange(SpeedToAIVel_XY);
        if (!Result.success)
            return Vector2.Zero;

        return PxUtil.ToPx(new Vector2(Result.min, Result.max));
    }
    #endregion

    #region 应用速度注入到AI
    public void ApplySpeedToAI(Projectile proj, Vector2 velocity, List<int> updateList, int projId)
    {
        if (SpeedToAI == null || SpeedToAI.Count == 0) return;

        foreach (var inje in SpeedToAI)
        {
            int aiIndex = inje.Key;
            int injeType = inje.Value;

            if (aiIndex < 0 || aiIndex >= proj.ai.Length) continue;

            float value = injeType switch
            {
                0 => (float)Math.Atan2(velocity.Y, velocity.X), // 角度
                1 => velocity.Length(),                         // 速度大小
                2 => velocity.X,                                // X分量
                3 => velocity.Y,                                // Y分量
                _ => 0f
            };

            proj.ai[aiIndex] = value;
            if (!updateList.Contains(projId))
                updateList.Add(projId);
        }
    }
    #endregion
}
#endregion

// 用于存储更新弹幕的信息
public class UpdateProjInfo
{
    // 指示物
    public string Notes { get; set; }
    // 弹幕索引
    public int Index { get; set; }
    // 怪物索引
    public int whoAmI { get; set; }
    // 弹幕ID
    public int Type { get; set; }
    // 新弹幕ID（用于类型转换）
    public int NewType { get; set; }
    // 当前更新阶段索引
    public int UpdateIndex { get; set; } = 0;
    // 阶段开始时间
    public DateTime StageStartTime { get; set; } = DateTime.UtcNow;
    public UpdateProjInfo(int index, int useIndex, int type)
    {
        Index = index;
        whoAmI = useIndex;
        Type = type;
        NewType = type; // 初始时新类型与原类型相同
        StageStartTime = DateTime.UtcNow;
    }
}

internal class MyProjectile
{
    #region 生成弹幕（主方法）
    public static void SpawnProjectile(NpcData data, List<SpawnProjData> SpawnProj, NPC npc)
    {
        if (SpawnProj == null || SpawnProj.Count == 0 || npc == null) return;

        var tar = npc.GetTargetData(true);
        var state = StateUtil.GetState(npc);

        if (state == null || state.Index >= SpawnProj.Count) return;

        var proj = SpawnProj[state.Index];

        // 条件检查
        if (!CheckConditions(npc, proj, state))
        {
            NextProj(SpawnProj, state);
            return;
        }

        // 初始化状态
        InitProjState(state, state.Index);

        // 冷却检查
        if (state.EachCDs[state.Index] > 0f)
        {
            state.EachCDs[state.Index] -= 1f;
            UpdateAllProjs(data, npc, SpawnProj);
            return;
        }

        // 生成弹幕
        GenerateProj(proj, npc, tar, state);

        // 更新状态
        state.EachCDs[state.Index] = proj.Interval;
        state.SendStack[state.Index]++;

        // 检查是否切换到下一个弹幕组
        if (state.SendStack[state.Index] >= proj.Stack)
        {
            NextProj(SpawnProj, state);
        }

        UpdateAllProjs(data, npc, SpawnProj);
    }
    #endregion

    #region 更新所有弹幕
    private static void UpdateAllProjs(NpcData data, NPC npc, List<SpawnProjData> SpawnProj)
    {
        // 添加参数检查
        if (npc == null || SpawnProj == null || UpdateState == null) return;

        var state = StateUtil.GetState(npc);
        if (state == null || state.Index >= SpawnProj.Count) return;

        var proj = SpawnProj[state.Index];
        if (proj?.UpdateProj == null || proj.UpdateProj.Count == 0) return;

        for (var i = 0; i < UpdateState.Length; i++)
        {
            if (UpdateState[i] == null) continue;

            UpdateProjInfo upState = UpdateState[i];
            if (upState.whoAmI != npc.whoAmI) continue; // 检查这个弹幕是否属于当前NPC

            // 检查索引范围
            if (upState.Index < 0 || upState.Index >= Main.maxProjectiles)
            {
                UpdateState[i] = null;
                continue;
            }

            Projectile NewProj = Main.projectile[upState.Index];
            if (NewProj == null || !NewProj.active)
            {
                UpdateState[i] = null!;
                // 同时清理计时器
                if (UpdateTimer.ContainsKey(upState.Index))
                    UpdateTimer.Remove(upState.Index);
                continue;
            }

            // 检查更新间隔
            if (UpdateTimer.ContainsKey(upState.Index) &&
                (DateTime.UtcNow - UpdateTimer[upState.Index]).TotalMilliseconds >= proj.UpdateTime)
            {
                UpdateSingleProj(data, proj, proj.UpdateProj, npc, NewProj.type, state);
                UpdateTimer[upState.Index] = DateTime.UtcNow;
            }
        }
    }
    #endregion

    #region 条件检查（统一使用 Conditions）
    private static bool CheckConditions(NPC npc, SpawnProjData proj, NpcState state)
    {
        if (npc == null || proj == null || state == null) return false;

        if (proj.EnhConds != null)
        {
            bool all = true;
            bool loop = false;
            Conditions.Condition(npc, new StringBuilder(), null, proj.EnhConds, ref all, ref loop);

            if (!all) return false;
        }

        return true;
    }
    #endregion

    #region 初始化弹幕状态
    private static void InitProjState(NpcState state, int index)
    {
        if (!state.SendStack.TryGetValue(index,out var _))
            state.SendStack[index] = 0;

        if (!state.EachCDs.TryGetValue(index, out var _))
            state.EachCDs[index] = 0f;
    }
    #endregion

    #region 生成单个弹幕
    private static void GenerateProj(SpawnProjData proj, NPC npc, NPCAimedTarget tar, NpcState state)
    {
        if (proj == null || npc == null || state == null) return;

        var plr = Main.player[npc.target];
        if (plr == null) return;

        // 计算基础位置和速度
        var basePos = proj.TarCenter ? plr.Center : npc.Center;
        var baseVel = CalcBaseVelocity(proj, npc, tar, plr, state);

        // 应用半径偏移
        var finalPos = ApplyRadiusOffset(basePos, proj, state);

        // 应用角度偏移
        var finalVel = ApplyAngleOffset(baseVel, proj, state);

        // 生成基础弹幕
        if (proj.Life > 0)
        {
            CreateBaseProj(proj, npc, finalPos, finalVel, state);
        }

        // 生成复杂弹幕
        if (proj.CplxParams != null && proj.CplxParams.Mode > 0)
        {
            CplxProjGen.SpawnCplxProj(proj, npc, finalPos, finalVel);
        }

        // 应用指示物修改
        if (proj.MarkerMods != null && proj.MarkerMods.Count > 0)
        {
            MarkerUtil.SetMarkers(state, proj.MarkerMods, ref Main.rand, npc);
        }

        state.SPCount++;
    }
    #endregion

    #region 计算基础速度
    private static Vector2 CalcBaseVelocity(SpawnProjData proj, NPC npc, NPCAimedTarget tar, Player plr, NpcState state)
    {
        // 目标锁定模式
        if (proj.LockParams.LockRange > 0)
        {
            var tars = TarLockUtil.GetLockTars(npc, proj.LockParams);
            if (tars.Count > 0)
            {
                var tarPos = TarLockUtil.GetTargetPosition(tars[0], npc, proj.LockParams);
                var center = proj.TarCenter ? plr.Center : npc.Center;
                var dir = tarPos - center;

                return dir.SafeNormalize(Vector2.Zero) * proj.Velocity;
            }
            return Vector2.Zero;
        }

        // 基础模式
        var dict = tar.Center - npc.Center;
        return dict.SafeNormalize(Vector2.Zero) * proj.Velocity;
    }
    #endregion

    #region 应用半径偏移
    private static Vector2 ApplyRadiusOffset(Vector2 basePos, SpawnProjData proj, NpcState state)
    {
        if (proj.Radius == 0) return basePos;

        var radiusPx = PxUtil.ToPx(proj.Radius);
        var angle = state.SendStack[state.Index] / (float)(proj.Stack - 1) * MathHelper.TwoPi;
        var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radiusPx;

        return basePos + offset;
    }
    #endregion

    #region 应用角度偏移
    private static Vector2 ApplyAngleOffset(Vector2 baseVel, SpawnProjData proj, NpcState state)
    {
        if (proj.Angle == 0 && proj.Rotate == 0) return baseVel;

        var totalAngle = 0f;

        // 基础角度偏移
        if (proj.Angle != 0)
        {
            var angleRange = proj.Angle * Math.PI / 180;
            var angleStep = angleRange * 2 / (proj.Stack - 1);
            totalAngle += (float)((state.SendStack[state.Index] - (proj.Stack - 1) / 2.0f) * angleStep);
        }

        // 旋转偏移
        if (proj.Rotate != 0)
        {
            totalAngle += proj.Rotate * state.SendStack[state.Index] * MathHelper.Pi / 180f;
        }

        return baseVel.RotatedBy(totalAngle);
    }
    #endregion

    #region 创建基础弹幕
    private static void CreateBaseProj(SpawnProjData proj, NPC npc, Vector2 pos, Vector2 vel, NpcState state)
    {
        if (proj == null || npc == null) return;

        var ai0 = proj.AI.ContainsKey(0) ? proj.AI[0] : 0f;
        var ai1 = proj.AI.ContainsKey(1) ? proj.AI[1] : 0f;
        var ai2 = proj.AI.ContainsKey(2) ? proj.AI[2] : 0f;

        var newProj = Projectile.NewProjectile(
            npc.GetSpawnSourceForNPCFromNPCAI(),
            pos.X, pos.Y, vel.X, vel.Y,
            proj.Type, proj.Damage, proj.KnockBack,
            Main.myPlayer, ai0, ai1, ai2
        );

        // 检查弹幕是否成功创建
        if (newProj < 0 || newProj >= Main.maxProjectiles) return;

        var projectile = Main.projectile[newProj];
        if (projectile == null) return;

        projectile.timeLeft = proj.Life;

        // 应用指示物注入AI
        if (proj.MarkerToAI != null && proj.MarkerToAI.Count > 0)
        {
            MarkerUtil.InjectToAI(state, proj.MarkerToAI, projectile);
        }

        // 注册更新弹幕
        if (proj.UpdateProj != null && proj.UpdateProj.Count > 0)
        {
            // 确保索引在有效范围内
            if (newProj >= 0 && newProj < UpdateState.Length)
            {
                UpdateState[newProj] = new UpdateProjInfo(newProj, npc.whoAmI, proj.Type)
                {
                    UpdateIndex = 0
                };

                // 初始化更新计时器
                UpdateTimer[newProj] = DateTime.UtcNow;
            }
        }
    }
    #endregion

    #region 更新单个弹幕
    public static Dictionary<int, DateTime> UpdateTimer = new(); //用于追踪更新弹幕的时间
    public static UpdateProjInfo[] UpdateState { get; set; } = new UpdateProjInfo[Main.maxProjectiles];
    private static void UpdateSingleProj(NpcData data, SpawnProjData proj, List<UpdateProjData> Update, NPC npc, int type, NpcState state)
    {
        if (Update == null || Update.Count <= 0) return;

        var UpList = new List<int>();

        // 检查更新阶段
        for (var i = 0; i < UpdateState.Length; i++)
        {

            if (UpdateState[i] == null) continue;

            if (type <= 0 || UpdateState[i].Index < 0 || UpdateState[i].Type != type || UpdateState[i].whoAmI != npc.whoAmI) continue;

            int index = UpdateState[i].Index;
            UpdateProjInfo upInfo = UpdateState[i];

            Projectile NewProj = Main.projectile[index];
            if (NewProj == null || !NewProj.active || NewProj.type != upInfo.NewType || NewProj.owner != Main.myPlayer)
            {
                UpdateState[i] = null!;
                continue;
            }

            // 检查是否还有更多更新阶段
            if (upInfo.UpdateIndex >= Update.Count)
            {
                continue;
            }

            // 获取当前阶段的更新数据
            UpdateProjData up = Update[upInfo.UpdateIndex];
            if (up == null)
            {
                upInfo.UpdateIndex++;
                return;
            }

            // 阶段时间间隔检查
            if (up.StageInterval > 0 &&
                (DateTime.UtcNow - upInfo.StageStartTime).TotalMilliseconds < up.StageInterval)
            {
                return;
            }


            // 条件检查
            var allow = true;
            Conditions.Condition(data, npc, up.Condition, ref allow);

            if (!allow) return;

            // 新增：应用指示物修改
            if (up.MarkerMods != null && up.MarkerMods.Count > 0)
            {
                MarkerUtil.SetMarkers(state, up.MarkerMods, ref Main.rand, npc);
            }

            // 新增：应用指示物注入AI
            if (up.MarkerToAI != null && up.MarkerToAI.Count > 0)
            {
                MarkerUtil.InjectToAI(state, up.MarkerToAI, NewProj);
            }

            // 弹幕类型变更
            if (up.NewType != 0)
            {
                int newType = up.NewType == -1 ? upInfo.Type : up.NewType;
                if (newType > 0 && newType != upInfo.NewType)
                {
                    NewProj.type = newType;
                    upInfo.NewType = newType;
                    Add(UpList, upInfo.Index);
                }
            }

            // 延长持续时间
            if (up.ExtraTime != 0)
            {
                NewProj.timeLeft += up.ExtraTime;
                Add(UpList, upInfo.Index);
            }

            // 速度处理
            Vector2 newVel = up.GetFinalVelocity(NewProj);

            // 应用追踪模式（修复版）
            newVel = ApplyHomingMode(newVel, NewProj, up, npc, UpList);

            // 应用角度和旋转
            if (up.Angle != 0f || up.Rotate != 0f)
            {
                double totalAngle = up.Angle * Math.PI / 180;
                if (up.Rotate != 0f)
                {
                    totalAngle += up.Rotate * Math.PI / 180;
                }
                newVel = newVel.RotatedBy(totalAngle);
                Add(UpList, upInfo.Index);
            }

            // 应用位置偏移（使用字符串格式）
            if (!string.IsNullOrWhiteSpace(up.PosOffset_XY) && up.PosOffset_XY != "0,0")
            {
                Vector2 offset = up.GetPosOffsetVector();
                Vector2 newPos = NewProj.position + offset;
                if (NewProj.position != newPos)
                {
                    NewProj.position = newPos;
                    Add(UpList, upInfo.Index);
                }
            }

            // 应用半径偏移
            if (up.Radius != 0f)
            {
                Vector2 newPos = NewProj.position;
                float radiusPx = PxUtil.ToPx(up.Radius);
                double angle = state.SPCount / (float)(proj.Stack - 1) * MathHelper.TwoPi;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radiusPx;
                newPos += offset;

                if (NewProj.position != newPos)
                {
                    NewProj.position = newPos;
                    Add(UpList, upInfo.Index);
                }
            }

            // 更新速度
            if (NewProj.velocity != newVel)
            {
                NewProj.velocity = newVel;
                Add(UpList, upInfo.Index);
            }

            // 更新AI参数
            if (up.AI.Count > 0)
            {
                for (int j = 0; j < NewProj.ai.Length; j++)
                {
                    if (up.AI.ContainsKey(j))
                    {
                        NewProj.ai[j] = up.AI[j];
                        Add(UpList, upInfo.Index);
                    }
                }
            }

            // 速度注入AI（自定义注入）
            if (up.SpeedToAI != null && up.SpeedToAI.Count > 0)
            {
                // 应用速度注入到指定的AI索引
                up.ApplySpeedToAI(NewProj, newVel, UpList, upInfo.Index);
                
                // 设置新的速度向量
                Vector2 speedVec = up.GetSpeedToAIVector();
                if (speedVec != Vector2.Zero)
                {
                    NewProj.velocity = speedVec;
                    Add(UpList, upInfo.Index);
                }
            }

            // 移动到下一个阶段
            upInfo.UpdateIndex++;
            // 重置阶段开始时间
            upInfo.StageStartTime = DateTime.UtcNow;
        }

        // 发送更新
        if (UpList.Count > 0)
        {
            foreach (int all in UpList)
            {
                TSPlayer.All.SendData(PacketTypes.ProjectileNew, null, all, 0f, 0f, 0f, 0);
            }
        }
    }
    #endregion

    #region 应用追踪模式（修复版）
    private static Vector2 ApplyHomingMode(Vector2 currVel, Projectile proj, UpdateProjData up,
                                         NPC npc, List<int> upList)
    {
        if (!up.Homing) return currVel;

        // 获取目标
        Entity tar = GetHomingTarget(up.TarType, npc, proj);
        if (tar == null || !tar.active) return currVel;

        Vector2 pos = tar.Center;

        // 预测走位
        if (up.PredictTime > 0f)
        {
            pos += tar.velocity * up.PredictTime * 60f;
        }

        Vector2 direction = pos - proj.Center;
        if (direction == Vector2.Zero) return currVel;

        direction = direction.SafeNormalize(Vector2.Zero);

        // 计算角度差
        float currAngle = (float)Math.Atan2(currVel.Y, currVel.X);
        float tarAngle = (float)Math.Atan2(direction.Y, direction.X);
        float angleDiff = tarAngle - currAngle;

        // 规范化角度差
        while (angleDiff > Math.PI) angleDiff -= (float)Math.PI * 2;
        while (angleDiff < -Math.PI) angleDiff += (float)Math.PI * 2;

        // 应用最大追踪角度限制
        float maxAngle = MathHelper.ToRadians(up.MaxHomingAngle);
        angleDiff = MathHelper.Clamp(angleDiff, -maxAngle, maxAngle);

        // 应用追踪强度
        float newAngle = currAngle + angleDiff * up.HomingStrength;
        Vector2 newVel = new Vector2((float)Math.Cos(newAngle), (float)Math.Sin(newAngle)) * currVel.Length();

        Add(upList, proj.whoAmI);
        return newVel;
    }
    #endregion

    #region 获取追踪目标
    private static Entity GetHomingTarget(int tarType, NPC npc, Projectile proj)
    {
        return tarType switch
        {
            0 => npc, // NPC自己
            1 => Main.player[npc.target], // 当前目标玩家
            2 => proj, // 弹幕自己（用于特殊效果）
            3 => FindClosestPlayer(npc), // 最近玩家
            _ => Main.player[npc.target] // 默认当前目标
        };
    }
    #endregion

    #region 寻找最近玩家
    private static Player FindClosestPlayer(NPC npc)
    {
        Player closest = null;
        float minDice = float.MaxValue;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var plr = Main.player[i];
            if (plr == null || !plr.active || plr.dead) continue;

            float dice = Vector2.DistanceSquared(npc.Center, plr.Center);
            if (dice < minDice)
            {
                minDice = dice;
                closest = plr;
            }
        }

        return closest;
    }
    #endregion

    #region 添加到更新列表
    private static void Add(List<int> list, int projId)
    {
        if (!list.Contains(projId))
        {
            list.Add(projId);
        }
    }
    #endregion

    #region 切换到下一个弹幕
    private static void NextProj(List<SpawnProjData> data, NpcState state)
    {
        state.Index = (state.Index + 1) % data.Count;
        state.SendStack[state.Index] = 0;
        state.EachCDs[state.Index] = 0f;
    }
    #endregion
}