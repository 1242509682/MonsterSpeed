using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

#region 更新弹幕数据
public class UpdateProjData
{
    [JsonProperty("新弹幕ID(-1恢复,0不更新,>0替换)", Order = -2)]
    public int NewType = 0;
    [JsonProperty("更新间隔/毫秒", Order = -2)]
    public double UpdateTime = 500f;
    [JsonProperty("延长持续时间", Order = -1)]
    public int ExtraTime = 0;
    [JsonProperty("更新次数", Order = 0)]
    public int Count = 1;
    [JsonProperty("更新条件", Order = 1)]
    public string Condition { get; set; } = "默认配置";
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

    [JsonProperty("追踪模式", Order = 20)]
    public HomingData HomingMode { get; set; } = new HomingData();

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

#region 用于存储更新弹幕的信息
public class UpdateProjState
{
    public string Notes { get; set; } // 标志
    public int Index { get; set; }  // 弹幕索引
    public int whoAmI { get; set; } // 怪物索引
    public int Type { get; set; }  // 弹幕ID
    public int NewType { get; set; } // 新弹幕ID（用于类型转换）
    public int UpdateIndex { get; set; } = 0; // 当前更新阶段索引
    public UpdateProjState(int index, int useIndex, int type)
    {
        Index = index;
        whoAmI = useIndex;
        Type = type;
        NewType = type; // 初始时新类型与原类型相同
    }
}
#endregion

internal class UpdateProjectile
{
    #region 检查所有更新弹幕
    public static Dictionary<int, DateTime> UpdateTimer = new(); //用于追踪更新弹幕的时间
    public static UpdateProjState[] UpdateState { get; set; } = new UpdateProjState[Main.maxProjectiles];
    public static void CheckAllUpdate(NpcData Setting, NPC npc, List<SpawnProjData> datas)
    {
        // 增强参数检查
        if (npc == null || !npc.active || datas == null || UpdateState == null)
            return;

        var state = StateUtil.GetState(npc);
        if (state == null || state.SendProjIndex < 0 || state.SendProjIndex >= datas.Count)
            return;

        var data = datas[state.SendProjIndex];
        if (data?.UpdateProj == null || data.UpdateProj.Count == 0)
            return;

        for (var i = UpdateState.Length - 1; i > 0; i--)
        {
            var upState = UpdateState[i];
            if (upState == null)
                continue;

            // 增强索引检查
            if (upState.whoAmI != npc.whoAmI ||
                upState.Index < 0 ||
                upState.Index >= Main.maxProjectiles)
            {
                // 结束更新
                Remove(upState.Index);
                continue;
            }

            Projectile NewProj = Main.projectile[upState.Index];
            if (NewProj == null || !NewProj.active ||
                NewProj.owner != Main.myPlayer ||
                NewProj.type != upState.NewType)
            {
                // 结束更新
                Remove(upState.Index);
                continue;
            }

            // 从文件加载更新弹幕配置
            var Update = new List<UpdateProjData>();

            if (data.UpdateProj != null && data.UpdateProj.Count > 0)
            {
                foreach (var File in data.UpdateProj)
                {
                    var file = UpdateProjectileFile.GetData(File);
                    if (file != null && file.Count > 0)
                    {
                        Update.AddRange(file);
                    }
                }
            }

            if (Update.Count <= 0) continue;

            if (upState.UpdateIndex < 0 || upState.UpdateIndex >= Update.Count)
            {
                Remove(upState.Index);
                continue;
            }

            UpdateProjData up = Update[upState.UpdateIndex];

            // 条件检查
            if (!string.IsNullOrEmpty(up.Condition))
            {
                bool allow = true;
                var cond = CondFileManager.GetCondData(up.Condition);
                Conditions.Condition(npc, new StringBuilder(), Setting, cond, ref allow);
                if (!allow) continue;
            }

            // 检查更新间隔
            if (UpdateTimer.ContainsKey(upState.Index) &&
                (DateTime.UtcNow - UpdateTimer[upState.Index]).TotalMilliseconds >= up.UpdateTime)
            {
                UpdateSingleProj(npc, state, upState, up, NewProj, data.Stack);
                UpdateTimer[upState.Index] = DateTime.UtcNow;
            }
        }
    }
    #endregion

    #region 更新单个弹幕
    private static void UpdateSingleProj(NPC npc, NpcState state, UpdateProjState upState, UpdateProjData up, Projectile NewProj, int Stack)
    {
        if (npc == null || !npc.active || state == null)
            return;

        var UpList = new List<int>();

        if (NewProj.friendly)
        {
            NewProj.friendly = false;
            Add(UpList, upState.Index);
        }

        // 新增：应用指示物注入AI
        if (up.MarkerToAI != null && up.MarkerToAI.Count > 0)
        {
            MarkerUtil.InjectToAI(state, up.MarkerToAI, NewProj);
            Add(UpList, upState.Index);
        }

        // 弹幕类型变更
        if (up.NewType != 0)
        {
            int newType = up.NewType == -1 ? upState.Type : up.NewType;
            if (newType > 0 && newType != upState.NewType)
            {
                NewProj.type = newType;
                upState.NewType = newType;
                Add(UpList, upState.Index);
            }
        }

        // 延长持续时间
        if (up.ExtraTime != 0)
        {
            NewProj.timeLeft += up.ExtraTime;
            Add(UpList, upState.Index);
        }

        // 速度处理
        Vector2 newVel = up.GetFinalVelocity(NewProj);

        // 应用追踪模式（使用新的追踪系统）
        if (up.HomingMode != null && up.HomingMode.Homing)
        {
            newVel = AutoHoming.ApplyHomingMode(newVel, NewProj, up.HomingMode, npc, UpList);
        }

        // 应用角度和旋转
        if (up.Angle != 0f || up.Rotate != 0f)
        {
            double totalAngle = up.Angle * Math.PI / 180;
            if (up.Rotate != 0f)
            {
                totalAngle += up.Rotate * Math.PI / 180;
            }
            newVel = newVel.RotatedBy(totalAngle);
            Add(UpList, upState.Index);
        }

        // 应用位置偏移（使用字符串格式）
        if (!string.IsNullOrWhiteSpace(up.PosOffset_XY) && up.PosOffset_XY != "0,0")
        {
            Vector2 offset = up.GetPosOffsetVector();
            Vector2 newPos = NewProj.position + offset;
            if (NewProj.position != newPos)
            {
                NewProj.position = newPos;
                Add(UpList, upState.Index);
            }
        }

        // 应用半径偏移
        if (up.Radius != 0f)
        {
            Vector2 newPos = NewProj.position;
            float radiusPx = PxUtil.ToPx(up.Radius);
            double angle = state.SPCount / (float)(Stack - 1) * MathHelper.TwoPi;
            Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radiusPx;
            newPos += offset;

            if (NewProj.position != newPos)
            {
                NewProj.position = newPos;
                Add(UpList, upState.Index);
            }
        }

        // 更新速度
        if (NewProj.velocity != newVel)
        {
            NewProj.velocity = newVel;
            Add(UpList, upState.Index);
        }

        // 更新AI参数
        if (up.AI.Count > 0)
        {
            for (int j = 0; j < NewProj.ai.Length; j++)
            {
                if (up.AI.ContainsKey(j))
                {
                    NewProj.ai[j] = up.AI[j];
                    Add(UpList, upState.Index);
                }
            }
        }

        // 速度注入AI（自定义注入）
        if (up.SpeedToAI != null && up.SpeedToAI.Count > 0)
        {
            // 应用速度注入到指定的AI索引
            up.ApplySpeedToAI(NewProj, newVel, UpList, upState.Index);

            // 设置新的速度向量
            Vector2 speedVec = up.GetSpeedToAIVector();
            if (speedVec != Vector2.Zero)
            {
                NewProj.velocity = speedVec;
                Add(UpList, upState.Index);
            }
        }

        Next(upState); // 移动下个更新阶段

        // 发送更新
        if (UpList.Count > 0)
        {
            foreach (int all in UpList)
            {
                // 确保弹幕索引有效且弹幕仍然活跃
                if (all >= 0 && all < Main.maxProjectiles &&
                    Main.projectile[all] != null && Main.projectile[all].active)
                {
                    TSPlayer.All.SendData(PacketTypes.ProjectileNew, null, all, 0f, 0f, 0f, 0);
                }
            }
        }
    }
    #endregion

    #region 添加到更新列表
    private static void Add(List<int> list, int projId)
    {
        // 添加边界检查
        if (projId >= 0 && projId < Main.maxProjectiles && !list.Contains(projId))
        {
            list.Add(projId);
        }
    }
    #endregion

    #region 获取弹幕状态
    public static UpdateProjState GetState(int projIndex)
    {
        if (projIndex >= 0 && projIndex < Main.maxProjectiles)
        {
            return UpdateState[projIndex];
        }
        return null;
    }
    #endregion

    #region 清理怪物相关的弹幕状态
    public static void ClearStates(int npcWhoAmI)
    {
        for (int i = 0; i < UpdateState.Length; i++)
        {
            if (UpdateState[i] != null && UpdateState[i].whoAmI == npcWhoAmI)
            {
                UpdateState[i] = null;
                UpdateTimer.Remove(i);
            }
        }
    }
    #endregion

    #region 移除单个弹幕状态
    public static void Remove(int Index)
    {
        if (Index >= 0 && Index < Main.maxProjectiles)
        {
            UpdateState[Index] = null;
            if (UpdateTimer.ContainsKey(Index))
                UpdateTimer.Remove(Index);
        }
    }
    #endregion

    #region 移动下个更新阶段
    private static void Next(UpdateProjState upInfo)
    {
        upInfo.UpdateIndex++; // 移动到下一个阶段
    }
    #endregion

    #region 添加弹幕更新状态（带边界检查）
    public static bool AddState(int projIndex, int npcWhoAmI, int projType)
    {
        // 严格的边界检查
        if (projIndex < 0 || projIndex >= Main.maxProjectiles)
        {
            TShock.Log.ConsoleError($"[怪物加速] 弹幕索引越界: {projIndex}");
            return false;
        }

        try
        {
            var state = new UpdateProjState(projIndex, npcWhoAmI, projType);
            UpdateState[projIndex] = state;
            UpdateTimer[projIndex] = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 添加弹幕状态失败: {ex.Message}");
            return false;
        }
    }
    #endregion
}
