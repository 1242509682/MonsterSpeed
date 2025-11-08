using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;

namespace MonsterSpeed;

public class SpawnProjData
{
    [JsonProperty("弹幕ID", Order = 0)]
    public int Type = 115;
    [JsonProperty("伤害", Order = 1)]
    public int Damage = 30;
    [JsonProperty("数量", Order = 2)]
    public int stack = 5;
    [JsonProperty("间隔", Order = 3)]
    public float interval = 30f;
    [JsonProperty("击退", Order = 4)]
    public int KnockBack = 5;
    [JsonProperty("速度", Order = 5)]
    public float Velocity = 10f;
    [JsonProperty("衰减", Order = 6)]
    public float decay = 0.9f;
    [JsonProperty("衰减倍数", Order = 7)]
    public float DecayMultiplier = 1.0f;
    [JsonProperty("按发衰减", Order = 8)]
    public bool decayForStack = true;
    [JsonProperty("半径", Order = 9)]
    public float Radius = 0f;
    [JsonProperty("偏移", Order = 10)]
    public float Angle = 0;
    [JsonProperty("旋转", Order = 11)]
    public float Rotate = 0;
    [JsonProperty("弹幕AI", Order = 12)]
    public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
    [JsonProperty("持续时间", Order = 13)]
    public int Lift = 120;
    [JsonProperty("生成点(0怪物/1玩家/2弹幕)", Order = 14)]
    public int SpawnPointType = 0;
    [JsonProperty("射向模式(0玩家/1怪物/2固定方向)", Order = 15)]
    public int AimMode = 0;
    [JsonProperty("固定方向", Order = 16)]
    public Vector2 FixedDir = new Vector2(1, 0);

    [JsonProperty("更新间隔", Order = 20)]
    public double UpdateTime = 500f;
    [JsonProperty("更新弹幕", Order = 21)]
    public List<UpdateProjData> UpdateProj { get; set; } = new List<UpdateProjData>();
}

public class UpdateProjData
{
    [JsonProperty("新弹幕ID说明", Order = -3)]
    public string Text = "-1恢复生成弹幕ID, 0不更新弹幕ID, >0替换新弹幕ID";
    [JsonProperty("新弹幕ID", Order = -2)]
    public int NewType = 0;
    [JsonProperty("延长更新间隔", Order = -1)]
    public double StageInterval { get; set; } = 0f;
    [JsonProperty("延长持续时间", Order = 0)]
    public int ExtraTime = 0;
    [JsonProperty("速度", Order = 1)]
    public float Velocity = 0f;
    [JsonProperty("衰减", Order = 2)]
    public float decay = 0.9f;
    [JsonProperty("衰减倍数", Order = 3)]
    public float DecayMultiplier = 1.0f;
    [JsonProperty("按发衰减", Order = 4)]
    public bool decayForStack = true;
    [JsonProperty("半径", Order = 5)]
    public float Radius = 0f;
    [JsonProperty("偏移", Order = 6)]
    public float Angle = 0f;
    [JsonProperty("旋转", Order = 7)]
    public float Rotate = 0;

    [JsonProperty("生成点(0怪物/1玩家/2更新位)", Order = 8)]
    public int SpawnPointType = 0;
    [JsonProperty("射向模式(0玩家/1怪物/2固定方向)", Order = 9)]
    public int AimMode = 0;
    [JsonProperty("固定方向", Order = 10)]
    public Vector2 FixedDir = new Vector2(1, 0);

    [JsonProperty("追踪模式(0怪物/1玩家/2弹幕)", Order = 25)]
    public bool Homing = false;
    [JsonProperty("追踪目标", Order = 26)]
    public int Tar = 0;
    [JsonProperty("预测秒数", Order = 27)]
    public float PredictTime = 0f;
    [JsonProperty("追踪强度", Order = 28)]
    public float HomingStrength = 1.0f;
    [JsonProperty("追踪角度", Order = 29)]
    public float MaxHomingAngle = 45f;

    [JsonProperty("弹幕AI", Order = 50)]
    public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
}

public class SpawnProjInfo
{
    public int Index = 0; //存储弹幕组索引
    public int SPCount = 0; //用于追踪所有弹幕生成次数
    public Dictionary<int, int> SendStack = new Dictionary<int, int>(); //追踪每发弹幕的数量
    public Dictionary<int, float> SpawnCooldowns = new Dictionary<int, float>(); // 追踪每组弹幕的冷却时间
    public Dictionary<int, Vector2> LastGroupPos = new Dictionary<int, Vector2>(); // 追踪每组弹幕的最后位置

    public void ClearOld(int Index)
    {
        var oldKeys = LastGroupPos.Keys.Where(k => Math.Abs(k - Index) > 5).ToList();
        foreach (var key in oldKeys)
        {
            LastGroupPos.Remove(key);
        }
    }
}

public class UpdateProjInfo
{
    public int Index { get; set; } // 弹幕索引
    public int whoAmI { get; set; } // 怪物索引
    public int Type { get; set; } // 弹幕ID
    public int NewType { get; set; } // 新弹幕ID（用于更新弹幕时类型转换）
    public int UpdateIndex { get; set; } = 0; // 当前更新阶段索引
    public DateTime UpdateCooldowns { get; set; } = DateTime.UtcNow; // 更新冷却时间
    public int GroupIndex { get; set; } // 弹幕组索引
    public int FireIndex { get; set; } // 发射索引
    public Vector2 LastPos { get; set; } // 上次更新弹幕的位置

    public UpdateProjInfo(int index, int npcId, int type, int groupIndex, int fireIndex)
    {
        Index = index;
        whoAmI = npcId;
        Type = type;
        NewType = type;
        GroupIndex = groupIndex;
        FireIndex = fireIndex;
        UpdateCooldowns = DateTime.UtcNow;

        if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index] != null)
        {
            LastPos = Main.projectile[index].Center;
        }
    }
}

internal class MyProjectile
{
    #region 核心字段
    public static Dictionary<int, DateTime> UpdateTimes = new Dictionary<int, DateTime>();
    public static UpdateProjInfo[] UpdateState { get; set; } = new UpdateProjInfo[Main.maxProjectiles];
    #endregion

    #region 生成弹幕(核心方法)
    public static void SpawnProjectile(List<SpawnProjData> projList, NPC npc)
    {
        if (projList == null || projList.Count == 0 || npc == null || !npc.active)
            return;

        var state = GetState(npc);
        if (state == null || state.Index >= projList.Count) return;

        // 确保所有必要的字典键都存在
        if (!state.SendStack.ContainsKey(state.Index))
            state.SendStack[state.Index] = 0;
        if (!state.SpawnCooldowns.ContainsKey(state.Index))
            state.SpawnCooldowns[state.Index] = 0f;

        SpawnProjData data = projList[state.Index];
        Player plr = Main.player[npc.target];

        // 检查完成条件
        if ((state.SendStack[state.Index] >= data.stack) || data.Type <= 0 || plr == null || !plr.active)
        {
            RecordPos(state, data, npc, plr);
            Next(projList, state);
            return;
        }

        // 处理冷却
        if (state.SpawnCooldowns[state.Index] > 0f)
        {
            state.SpawnCooldowns[state.Index] -= 1f;
            return;
        }

        int fireIndex = state.SendStack[state.Index];

        HandleSpawnProjectile(data, npc, plr, state, fireIndex);

        state.SendStack[state.Index]++;
        state.SpawnCooldowns[state.Index] = data.interval;

        UpdateGroupProj(npc, plr, projList, state.Index);
    }
    #endregion

    #region 更新弹幕(核心方法)
    private static void UpdateProj(SpawnProjData proj, List<UpdateProjData> updates, NPC npc, Player plr, UpdateProjInfo info)
    {
        if (updates == null || updates.Count == 0 || info.UpdateIndex >= updates.Count)
            return;

        UpdateProjData up = updates[info.UpdateIndex];
        if (up == null) return;

        if (up.StageInterval > 0 &&
            (DateTime.UtcNow - info.UpdateCooldowns).TotalMilliseconds < up.StageInterval)
            return;

        var obj = Main.projectile[info.Index];
        if (obj == null || !obj.active) return;

        List<int> upList = new List<int>();

        // 处理友好变更
        if (obj.friendly)
        {
            obj.friendly = false;
            if (!upList.Contains(info.Index))
                upList.Add(info.Index);
        }

        // 处理类型变更
        if (up.NewType != 0)
        {
            int newType = up.NewType == -1 ? info.Type : up.NewType;
            if (newType > 0 && newType != info.NewType)
            {
                obj.type = newType;
                info.NewType = newType;
                if (!upList.Contains(info.Index))
                    upList.Add(info.Index);
            }
        }

        // 延长持续时间
        if (up.ExtraTime != 0)
        {
            obj.timeLeft += up.ExtraTime;
            if (!upList.Contains(info.Index))
                upList.Add(info.Index);
        }

        // 处理生成点
        Vector2 newPos = GetSpawnPos(up.SpawnPointType, npc, plr, info, null, 0, info.FireIndex, up.Radius, proj.stack, true);
        obj.position = newPos - new Vector2(obj.width, obj.height) / 2f;
        if (!upList.Contains(info.Index))
            upList.Add(info.Index);

        info.LastPos = obj.Center;

        // 更新速度
        Vector2 newVel = GetUpdateVel(up, obj, proj, info, npc, plr, ref upList);
        if (obj.velocity != newVel)
        {
            obj.velocity = newVel;
            if (!upList.Contains(info.Index))
                upList.Add(info.Index);
        }

        // 更新AI
        if (up.ai.Count > 0)
        {
            for (int j = 0; j < obj.ai.Length; j++)
            {
                if (up.ai.ContainsKey(j))
                {
                    obj.ai[j] = up.ai[j];
                    if (!upList.Contains(info.Index))
                        upList.Add(info.Index);
                }
            }
        }

        NextUpdate(updates, info);

        // 发送更新
        if (upList.Count > 0)
        {
            foreach (var idx in upList)
            {
                TSPlayer.All.SendData(PacketTypes.ProjectileNew, null, idx);
            }
        }
    }
    #endregion

    #region 处理弹幕生成方法
    private static void HandleSpawnProjectile(SpawnProjData proj, NPC npc, Player plr, SpawnProjInfo state, int fireIndex)
    {
        Vector2 spawnPos = GetSpawnPos(proj.SpawnPointType, npc, plr, null, state, state.Index, fireIndex, proj.Radius, proj.stack, false);
        Vector2 dir = GetDir(proj.AimMode, proj.FixedDir, npc, plr, spawnPos, fireIndex, proj.stack);
        Vector2 vel = GetVel(proj, dir, fireIndex);

        float ai0 = proj.ai?.ContainsKey(0) == true ? proj.ai[0] : 0f;
        float ai1 = proj.ai?.ContainsKey(1) == true ? proj.ai[1] : 0f;
        float ai2 = proj.ai?.ContainsKey(2) == true ? proj.ai[2] : 0f;

        if (proj.Lift > 0)
        {
            int newProj = Projectile.NewProjectile(
                npc.GetSpawnSourceForNPCFromNPCAI(),
                spawnPos.X, spawnPos.Y, vel.X, vel.Y,
                proj.Type, proj.Damage, proj.KnockBack,
                Main.myPlayer, ai0, ai1, ai2
            );

            if (newProj >= 0 && newProj < Main.maxProjectiles)
            {
                Main.projectile[newProj].timeLeft = proj.Lift;
                SetupUpdate(newProj, npc, proj, state.Index, fireIndex);
            }
        }
    }
    #endregion

    #region 设置弹幕更新
    private static void SetupUpdate(int projId, NPC npc, SpawnProjData proj, int groupIndex, int fireIndex)
    {
        if (proj.UpdateProj != null && proj.UpdateProj.Count > 0)
        {
            UpdateState[projId] = new UpdateProjInfo(projId, npc.whoAmI, proj.Type, groupIndex, fireIndex);
            UpdateTimes[projId] = DateTime.UtcNow;
        }
    }
    #endregion

    #region 位置计算
    private static Vector2 GetSpawnPos(int spawnType, NPC npc, Player plr, UpdateProjInfo info, SpawnProjInfo state, int groupIndex, int fireIndex, float radius, int stack, bool isUpdate)
    {
        if (npc == null) return Vector2.Zero;

        Vector2 basePos = Vector2.Zero;

        // 根据生成点类型确定基础位置
        switch (spawnType)
        {
            case 0: // 怪物中心
                basePos = npc.Center;
                break;
            case 1: // 玩家中心
                basePos = (plr != null && plr.active) ? plr.Center : npc.Center;
                break;
            case 2: // 上次更新位置
                basePos = isUpdate && info != null
                    ? info.LastPos
                    : (state != null && state.LastGroupPos.ContainsKey(groupIndex - 1) && groupIndex > 0
                        ? state.LastGroupPos[groupIndex - 1]
                        : npc.Center);
                break;
            default:
                basePos = npc.Center;
                break;
        }

        return ApplyRadius(basePos, radius, stack, fireIndex);
    }

    private static Vector2 ApplyRadius(Vector2 pos, float radius, int stack, int fireIndex)
    {
        if (radius == 0 || stack <= 1) return pos;

        double angle = fireIndex / (float)(stack - 1) * MathHelper.TwoPi;
        Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) *
                        (Math.Abs(radius) * 16) * Math.Sign(radius);

        return pos + offset;
    }
    #endregion

    #region 计算方向
    private static Vector2 GetDir(int aimMode, Vector2 fixedDir, NPC npc, Player plr, Vector2 pos, int fireIndex, int stack)
    {
        Vector2 dir = Vector2.Zero;

        switch (aimMode)
        {
            case 0: // 射向玩家
                if (plr != null && plr.active && plr.Center != pos)
                    dir = Vector2.Normalize(plr.Center - pos);
                break;
            case 1: // 射向怪物
                if (npc.Center != pos)
                    dir = Vector2.Normalize(npc.Center - pos);
                break;
            case 2: // 固定方向
                dir = Vector2.Normalize(fixedDir);
                break;
        }

        if (dir == Vector2.Zero)
            dir = new Vector2(1, 0); // 默认方向

        return dir;
    }

    private static Vector2 ApplyAngle(Vector2 dir, float angle, float rotate, int stack, int fireIndex)
    {
        Vector2 result = dir;

        if (angle != 0 && stack > 1)
        {
            double rad = angle * Math.PI / 180;
            double addRad = rad * 2 / (stack - 1);
            double currAngle = (fireIndex - (stack - 1) / 2.0f) * addRad;
            result = result.RotatedBy(currAngle);
        }

        if (rotate != 0)
            result = result.RotatedBy(rotate * fireIndex);

        return result;
    }
    #endregion

    #region 弹幕更新组处理方法
    private static void UpdateGroupProj(NPC npc, Player plr, List<SpawnProjData> projList, int GroupIndex)
    {
        if (GroupIndex >= projList.Count) return;

        var projData = projList[GroupIndex];
        if (projData?.UpdateProj == null || projData.UpdateProj.Count == 0) return;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            if (i < 0 || i >= Main.maxProjectiles) continue;

            var info = UpdateState[i];
            if (info == null) continue;

            if (info.whoAmI != npc.whoAmI || info.GroupIndex != GroupIndex) continue;

            var proj = Main.projectile[i];
            if (proj == null || !proj.active) continue;

            if (UpdateTimes.ContainsKey(i) &&
                (DateTime.UtcNow - UpdateTimes[i]).TotalMilliseconds >= projData.UpdateTime)
            {
                UpdateProj(projData, projData.UpdateProj, npc, plr, info);
                UpdateTimes[i] = DateTime.UtcNow;
            }
        }
    }
    #endregion

    #region 计算速度
    private static Vector2 GetVel(SpawnProjData proj, Vector2 dir, int fireIndex)
    {
        float decay = GetDecay(proj.decay, proj.DecayMultiplier, proj.decayForStack, fireIndex, proj.stack);
        Vector2 baseVel = dir * (proj.Velocity * decay);

        // 如果是四周汇聚模式，不应用额外的角度和旋转
        if (proj.AimMode == 3)
            return baseVel;

        return ApplyAngle(baseVel, proj.Angle, proj.Rotate, proj.stack, fireIndex);
    }

    private static Vector2 GetUpdateVel(UpdateProjData update, Projectile proj, SpawnProjData projData, UpdateProjInfo info, NPC npc, Player plr, ref List<int> updateList)
    {
        float decay = GetDecay(update.decay, update.DecayMultiplier, update.decayForStack, info.FireIndex, projData.stack);
        float speed = update.Velocity != 0 ? update.Velocity * decay : proj.velocity.Length();

        Vector2 vel = Vector2.Normalize(proj.velocity) * speed;

        // 重新计算方向（如果更新配置中指定了新的方向）
        if (update.AimMode != -1) // -1 表示保持原方向
        {
            Vector2 newDir = GetDir(update.AimMode, update.FixedDir, npc, plr, proj.Center, info.FireIndex, projData.stack);
            if (newDir != Vector2.Zero)
            {
                vel = Vector2.Normalize(newDir) * speed;
                if (!updateList.Contains(info.Index))
                    updateList.Add(info.Index);
            }
        }

        // 应用追踪
        if (update.Homing)
        {
            Entity target = GetTarget(update.Tar, npc, plr, proj);
            if (target != null && target.active)
            {
                Vector2 targetPos = target.Center;
                if (update.PredictTime > 0)
                    targetPos += target.velocity * update.PredictTime * 60f;

                Vector2 desiredDir = Vector2.Normalize(targetPos - proj.Center);
                Vector2 currentDir = Vector2.Normalize(proj.velocity);

                float currAngle = (float)Math.Atan2(currentDir.Y, currentDir.X);
                float targetAngle = (float)Math.Atan2(desiredDir.Y, desiredDir.X);

                float angleDiff = targetAngle - currAngle;
                angleDiff = (float)Math.IEEERemainder(angleDiff, MathHelper.TwoPi);

                float maxAngle = MathHelper.ToRadians(update.MaxHomingAngle);
                angleDiff = MathHelper.Clamp(angleDiff, -maxAngle, maxAngle);

                float newAngle = currAngle + angleDiff * update.HomingStrength;
                vel = new Vector2((float)Math.Cos(newAngle), (float)Math.Sin(newAngle)) * vel.Length();
                if (!updateList.Contains(info.Index))
                    updateList.Add(info.Index);
            }
        }

        // 如果是四周汇聚模式，不应用额外的角度和旋转
        if (update.AimMode == 3)
            return vel;

        // 应用角度和旋转
        vel = ApplyAngle(vel, update.Angle, update.Rotate, projData.stack, info.FireIndex);

        return vel;
    }
    #endregion

    #region 计算衰减
    private static float GetDecay(float decay, float decayMultiplier, bool decayForStack, int fireIndex, int stack)
    {
        if (!decayForStack)
            return decay * decayMultiplier;

        return 1.0f - fireIndex / (float)stack * (decay * decayMultiplier);
    }
    #endregion

    #region 移动到下一个要发射的弹幕方法
    private static void Next(List<SpawnProjData> data, SpawnProjInfo state)
    {
        if (!state.SendStack.ContainsKey(state.Index)) 
            state.SendStack[state.Index] = 0;

        if (state.SendStack[state.Index] >= data[state.Index].stack)
        {
            state.Index++;
            if (state.Index >= data.Count)
            {
                state.Index = 0;
            }
            state.SendStack[state.Index] = 0;
            state.SpawnCooldowns[state.Index] = 0f;
            state.SPCount++;
            state.ClearOld(state.Index);
        }
    }
    #endregion

    #region 移动到下一个更新阶段方法
    private static void NextUpdate(List<UpdateProjData> updates, UpdateProjInfo info)
    {
        // 进入下一阶段或循环到第一阶段
        info.UpdateIndex++;
        if (info.UpdateIndex >= updates.Count)
        {
            info.UpdateIndex = 0;
        }
        info.UpdateCooldowns = DateTime.UtcNow;
    }
    #endregion

    #region 记录当前弹幕组的结束位置 
    private static void RecordPos(SpawnProjInfo state, SpawnProjData proj, NPC npc, Player plr)
    {
        if (state.SendStack.ContainsKey(state.Index) && state.SendStack[state.Index] > 0)
        {
            int lastIndex = state.SendStack[state.Index] - 1;
            state.LastGroupPos[state.Index] = GetSpawnPos(proj.SpawnPointType, npc, plr, null, state, state.Index, lastIndex, proj.Radius, proj.stack, false);
        }
    }
    #endregion

    #region 目标类型选择方法
    private static Entity GetTarget(int tarType, NPC npc, Player plr, Projectile proj)
    {
        switch (tarType)
        {
            case 0: return npc;
            case 1: return plr;
            case 2: return proj;
            default: return npc;
        }
    }
    #endregion

    #region 状态管理
    public static Dictionary<int, SpawnProjInfo> States = new Dictionary<int, SpawnProjInfo>();
    public static SpawnProjInfo GetState(NPC npc)
    {
        if (npc == null || !npc.active)
            return null;

        if (!States.ContainsKey(npc.whoAmI))
            States[npc.whoAmI] = new SpawnProjInfo();

        return States[npc.whoAmI];
    }

    public static void ClearState(NPC npc)
    {
        if (npc != null && States.ContainsKey(npc.whoAmI))
        {
            States.Remove(npc.whoAmI);
        }
    }

    public static void ClearAllStates()
    {
        States.Clear();
        UpdateTimes.Clear();

        for (int i = 0; i < UpdateState.Length; i++)
            UpdateState[i] = null;
    }

    public static void ClearUpdateState(NPC npc)
    {
        if (npc == null) return;

        var toRemove = UpdateTimes.Keys
            .Where(key => key >= 0 && key < Main.maxProjectiles &&
                         UpdateState[key] != null && UpdateState[key].whoAmI == npc.whoAmI)
            .ToList();

        foreach (var key in toRemove)
        {
            UpdateTimes.Remove(key);
            if (key >= 0 && key < Main.maxProjectiles)
                UpdateState[key] = null;
        }
    }

    public static void PeriodicClear()
    {
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            if (UpdateState[i] != null)
            {
                var proj = Main.projectile[i];
                if (proj == null || !proj.active)
                {
                    if (i >= 0 && i < Main.maxProjectiles)
                        UpdateState[i] = null;

                    if (UpdateTimes.ContainsKey(i))
                        UpdateTimes.Remove(i);
                }
            }
        }

        var invalidNpcs = States.Keys
            .Where(id => id < 0 || id >= Main.maxNPCs || Main.npc[id] == null || !Main.npc[id].active)
            .ToList();

        foreach (var id in invalidNpcs)
        {
            States.Remove(id);
        }
    }
    #endregion
}