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

    [JsonProperty("生成点切换间隔", Order = 15)]
    public float SpawnPointInterval = 500f;
    [JsonProperty("自定义生成点", Order = 16)]
    public List<SpawnPointData> SpawnPoint { get; set; } = new List<SpawnPointData>();

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

    [JsonProperty("生成点切换间隔", Order = 8)]
    public float SpawnPointInterval = 0f;
    [JsonProperty("自定义生成点", Order = 9)]
    public List<SpawnPointData> SpawnPoint { get; set; } = new List<SpawnPointData>();

    [JsonProperty("追踪说明", Order = 24)]
    public string Text2 = "0追踪怪物, 1追踪玩家, 2追踪弹幕位置";
    [JsonProperty("追踪模式", Order = 25)]
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

public class SpawnPointData
{
    [JsonProperty("类型说明", Order = -1)]
    public string Text = "自定义出生点留空【无法发射弹幕】 0怪物中心, 1玩家中心, 2绝对坐标, 3相对NPC偏移, 4相对玩家偏移, 5上次更新位置";
    [JsonProperty("出生点类型", Order = 0)]
    public int Type = 0;
    [JsonProperty("坐标", Order = 1)]
    public Vector2 Position = Vector2.Zero;
    [JsonProperty("偏移", Order = 2)]
    public Vector2 Offset = Vector2.Zero;
    [JsonProperty("使用NPC朝向", Order = 3)]
    public bool UseNpcDir = false;
    [JsonProperty("重置偏移", Order = 4)]
    public bool ResetOffset = false;
    [JsonProperty("射向说明", Order = 5)]
    public string Text2 = "0射向玩家, 1射向怪物, 2使用固定方向";
    [JsonProperty("射向模式", Order = 6)]
    public int AimMode = 0; // 0: 射向玩家, 1: 射向NPC, 2: 固定方向
    [JsonProperty("固定方向", Order = 7)]
    public Vector2 FixedDir = new Vector2(1, 0); // 固定方向向量
}

public class SpawnProjInfo
{
    public int Index = 0; //存储弹幕组索引
    public int SPCount = 0; //用于追踪所有弹幕生成次数
    public Dictionary<int, int> SendStack = new Dictionary<int, int>(); //追踪每发弹幕的数量
    public Dictionary<int, float> SpawnCooldowns = new Dictionary<int, float>(); // 追踪每组弹幕的冷却时间
    public Dictionary<int, Vector2> LastGroupPos = new Dictionary<int, Vector2>(); // 追踪每组弹幕的最后位置
    public Dictionary<int, float> SendCooldowns = new Dictionary<int, float>(); //追踪每发弹幕之间的发射间隔
    public Dictionary<int, int> SpawnIndex = new Dictionary<int, int>(); // 追踪当前使用的生成点索引

    public void ClearOld(int Index)
    {
        var oldKeys = LastGroupPos.Keys
            .Where(k => Math.Abs(k - Index) > 5)
            .ToList();

        foreach (var key in oldKeys)
        {
            LastGroupPos.Remove(key);
            SendCooldowns.Remove(key);
            SpawnIndex.Remove(key);
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
    public Vector2 InitOffset { get; set; } // 初始偏移量
    public int SpawnIndex { get; set; } = 0; // 当前生成点索引
    public float UpdateSpawnTimer { get; set; } = 0f; // 生成点切换计时器

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
            var npc = GetNPC(npcId);
            if (npc != null)
                InitOffset = LastPos - npc.Center;
        }
    }

    private NPC? GetNPC(int whoAmI)
    {
        return whoAmI >= 0 && whoAmI < Main.maxNPCs ? Main.npc[whoAmI] : null;
    }
}

internal class MyProjectile
{
    #region 核心字段
    public static Dictionary<int, DateTime> UpdateTimes = new Dictionary<int, DateTime>();  //用于追踪更新弹幕的时间
    public static UpdateProjInfo[] UpdateState { get; set; } = new UpdateProjInfo[Main.maxProjectiles]; // 用于存储每个弹幕的更新信息
    #endregion

    #region 生成弹幕(核心方法)
    public static void SpawnProjectile(List<SpawnProjData> projList, NPC npc)
    {
        if (projList == null || projList.Count == 0 || npc == null || !npc.active)
            return;

        var state = GetState(npc);
        if (state == null) return;

        if (state.Index >= projList.Count) return;

        // 确保所有必要的字典键都存在
        if (!state.SendStack.ContainsKey(state.Index))
            state.SendStack[state.Index] = 0;
        if (!state.SpawnCooldowns.ContainsKey(state.Index))
            state.SpawnCooldowns[state.Index] = 0f;
        if (!state.SpawnIndex.ContainsKey(state.Index))
            state.SpawnIndex[state.Index] = 0;
        if (!state.SendCooldowns.ContainsKey(state.Index))
            state.SendCooldowns[state.Index] = 0f;
        if (!state.SendStack.ContainsKey(state.Index))
            state.SendStack[state.Index] = 0;
        if (!state.SpawnCooldowns.ContainsKey(state.Index))
            state.SpawnCooldowns[state.Index] = 0f;
        if (!state.SpawnIndex.ContainsKey(state.Index))
            state.SpawnIndex[state.Index] = 0;
        if (!state.SendCooldowns.ContainsKey(state.Index))
            state.SendCooldowns[state.Index] = 0f;

        SpawnProjData data = projList[state.Index];
        Player plr = Main.player[npc.target];

        // 检查完成条件
        if (IsGroupComplete(state, data, plr))
        {
            RecordEndPos(state, data, npc, plr);
            Next(projList, state);
            return;
        }

        // 处理冷却
        if (!IsReady(state)) return;

        int fireIndex = state.SendStack[state.Index];

        // 选择生成点配置
        SpawnPointData Settiing = SelectSpawnSetting(data, state, npc, plr);

        HandleSpawnProjectile(data, npc, plr, state, fireIndex, Settiing);

        state.SendStack[state.Index]++;
        state.SpawnCooldowns[state.Index] = data.interval;

        UpdateGroupProj(npc, plr, projList, state.Index);
    }
    #endregion

    #region 更新弹幕(核心方法)
    private static void UpdateProj(SpawnProjData proj, List<UpdateProjData> updates, NPC npc, Player plr, UpdateProjInfo info)
    {
        if (updates == null || updates.Count == 0) return;

        if (info.UpdateIndex >= updates.Count) return;

        UpdateProjData up = updates[info.UpdateIndex];
        if (up == null) return;

        if (up.StageInterval > 0 &&
            (DateTime.UtcNow - info.UpdateCooldowns).TotalMilliseconds < up.StageInterval)
            return;

        var obj = Main.projectile[info.Index];
        if (obj == null || !obj.active) return;

        List<int> upList = new List<int>();

        // 处理类型变更
        if (up.NewType != 0)
        {
            int newType = up.NewType == -1 ? info.Type : up.NewType;
            if (newType > 0 && newType != info.NewType)
            {
                obj.type = newType;
                info.NewType = newType;
                Add(upList, info.Index);
            }
        }

        // 延长持续时间
        if (up.ExtraTime != 0)
        {
            obj.timeLeft += up.ExtraTime;
            Add(upList, info.Index);
        }

        // 处理生成点
        if (up.SpawnPoint != null && up.SpawnPoint.Count > 0)
        {
            // 更新生成点计时器
            info.UpdateSpawnTimer += (float)proj.UpdateTime;

            // 检查是否需要切换生成点
            if (up.SpawnPointInterval > 0 && info.UpdateSpawnTimer >= up.SpawnPointInterval)
            {
                info.SpawnIndex = (info.SpawnIndex + 1) % up.SpawnPoint.Count;
                info.UpdateSpawnTimer = 0f;
            }

            // 使用当前生成点
            int spawnIndex = info.SpawnIndex % up.SpawnPoint.Count;
            SpawnPointData spawnConfig = up.SpawnPoint[spawnIndex];

            Vector2 newPos = GetSpawnPos(spawnConfig, npc, plr, info, null, 0, info.FireIndex, up.Radius, proj.stack, true);

            if (spawnConfig.ResetOffset)
                info.InitOffset = newPos - npc.Center;

            obj.position = newPos - new Vector2(obj.width, obj.height) / 2f;
            Add(upList, info.Index);

            info.LastPos = obj.Center;
        }
        else
        {
            info.LastPos = obj.Center;
        }

        // 更新速度
        Vector2 newVel = GetUpdateVel(up, obj, proj, info, npc, plr, ref upList);
        if (obj.velocity != newVel)
        {
            obj.velocity = newVel;
            Add(upList, info.Index);
        }

        // 更新AI
        if (up.ai.Count > 0)
        {
            for (int j = 0; j < obj.ai.Length; j++)
            {
                if (up.ai.ContainsKey(j))
                {
                    obj.ai[j] = up.ai[j];
                    Add(upList, info.Index);
                }
            }
        }

        // 进入下一阶段或循环到第一阶段
        info.UpdateIndex++;

        // 如果已经到达最后一个阶段，循环回到第一个阶段
        if (info.UpdateIndex >= updates.Count)
        {
            info.UpdateIndex = 0;

            // 重置生成点状态，让每个循环重新开始
            info.SpawnIndex = 0;
            info.UpdateSpawnTimer = 0f;
        }

        info.UpdateCooldowns = DateTime.UtcNow;

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

    #region 完成检查方法
    private static bool IsGroupComplete(SpawnProjInfo state, SpawnProjData proj, Player plr)
    {
        return (state.SendStack.ContainsKey(state.Index) && state.SendStack[state.Index] >= proj.stack) ||
               proj.Type <= 0 || plr == null;
    } 
    #endregion

    #region 冷却检查方法
    private static bool IsReady(SpawnProjInfo state)
    {
        if (!state.SpawnCooldowns.ContainsKey(state.Index) || state.SpawnCooldowns[state.Index] <= 0f)
            return true;

        state.SpawnCooldowns[state.Index] -= 1f;
        return false;
    } 
    #endregion

    #region 选择生成点配置方法
    private static SpawnPointData SelectSpawnSetting(SpawnProjData proj, SpawnProjInfo state, NPC npc, Player plr)
    {
        // 如果没有多重生成点，创建一个默认的生成点
        if (proj.SpawnPoint == null || proj.SpawnPoint.Count == 0)
        {
            return null;
        }

        // 初始化状态
        if (!state.SpawnIndex.ContainsKey(state.Index))
            state.SpawnIndex[state.Index] = 0;
        if (!state.SendCooldowns.ContainsKey(state.Index))
            state.SendCooldowns[state.Index] = 0f;

        // 更新计时器
        state.SendCooldowns[state.Index] += 1f;

        // 检查是否需要切换生成点
        if (proj.SpawnPointInterval > 0 && state.SendCooldowns[state.Index] >= proj.SpawnPointInterval)
        {
            state.SpawnIndex[state.Index] = (state.SpawnIndex[state.Index] + 1) % proj.SpawnPoint.Count;
            state.SendCooldowns[state.Index] = 0f;
        }

        // 返回当前生成点
        return proj.SpawnPoint[state.SpawnIndex[state.Index]];
    } 
    #endregion

    #region 处理弹幕生成方法
    private static void HandleSpawnProjectile(SpawnProjData proj, NPC npc, Player plr, SpawnProjInfo state, int fireIndex, SpawnPointData spawnConfig)
    {
        Vector2 spawnPos;
        Vector2 dir;
        Vector2 vel;

        float ai0 = proj.ai?.ContainsKey(0) == true ? proj.ai[0] : 0f;
        float ai1 = proj.ai?.ContainsKey(1) == true ? proj.ai[1] : 0f;
        float ai2 = proj.ai?.ContainsKey(2) == true ? proj.ai[2] : 0f;

        if (spawnConfig is null)
        {
            var tar = npc.GetTargetData(true); // 获取玩家与怪物的距离和相对位置向量
            spawnPos = tar.Center - npc.Center;
            dir = (tar.Center - npc.Center).SafeNormalize(Vector2.Zero);
            vel = GetVel(proj, dir, fireIndex);
        }
        else
        {
            spawnPos = GetSpawnPos(spawnConfig, npc, plr, null, state, state.Index, fireIndex, proj.Radius, proj.stack, false);
            dir = GetDir(spawnConfig, npc, plr, spawnPos, fireIndex, proj.stack, proj.Angle, proj.Rotate);
            vel = GetVel(proj, dir, fireIndex);
        }

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

                int spawnIndex = 0;
                if (state.SpawnIndex.ContainsKey(state.Index))
                {
                    spawnIndex = state.SpawnIndex[state.Index];
                }
                else
                {
                    state.SpawnIndex[state.Index] = 0;
                }

                SetupUpdate(newProj, npc, proj, state.Index, fireIndex, spawnIndex);
            }
        }
    } 
    #endregion

    #region 设置弹幕更新信息
    private static void SetupUpdate(int projId, NPC npc, SpawnProjData proj, int groupIndex, int fireIndex, int spawnIndex)
    {
        if (proj.UpdateProj != null && proj.UpdateProj.Count > 0)
        {
            UpdateState[projId] = new UpdateProjInfo(projId, npc.whoAmI, proj.Type, groupIndex, fireIndex)
            {
                SpawnIndex = spawnIndex
            };
            UpdateTimes[projId] = DateTime.UtcNow;
        }
    } 
    #endregion

    #region 位置计算
    private static Vector2 GetSpawnPos(SpawnPointData spawn, NPC npc, Player plr, UpdateProjInfo info, SpawnProjInfo state, int groupIndex, int fireIndex, float radius, int stack, bool isUpdate)
    {
        if (npc == null) return Vector2.Zero;

        Vector2 basePos = GetBasePos(spawn, npc, plr, info, state, groupIndex, isUpdate);
        basePos = ApplySpawnSetting(spawn, basePos, npc, plr, info);
        return ApplyRadius(basePos, radius, stack, fireIndex);
    }
    #endregion

    #region 获取基础位置
    private static Vector2 GetBasePos(SpawnPointData spawn, NPC npc, Player plr, UpdateProjInfo info, SpawnProjInfo state, int groupIndex, bool isUpdate)
    {
        if (isUpdate && info != null)
            return info.LastPos;

        if (state != null && state.LastGroupPos.ContainsKey(groupIndex - 1) && groupIndex > 0)
            return state.LastGroupPos[groupIndex - 1];

        return npc.Center;
    } 
    #endregion

    #region 应用生成点配置（选择模式）
    private static Vector2 ApplySpawnSetting(SpawnPointData spawn, Vector2 curPos, NPC npc, Player plr, UpdateProjInfo info)
    {
        if (spawn == null) return curPos;

        Vector2 newPos = curPos;
        bool hasPlr = plr != null && plr.active;

        switch (spawn.Type)
        {
            case 0: newPos = npc.Center; break;
            case 1: newPos = hasPlr ? plr.Center : npc.Center; break;
            case 2: newPos = spawn.Position; break;
            case 3: newPos = npc.Center + spawn.Offset; break;
            case 4: newPos = (hasPlr ? plr.Center : npc.Center) + spawn.Offset; break;
            case 5: if (info != null) newPos = info.LastPos; break;
        }

        if (spawn.Offset != Vector2.Zero && spawn.Type != 3 && spawn.Type != 4)
            newPos += spawn.Offset;

        return newPos;
    } 
    #endregion

    #region 应用半径偏移
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
    private static Vector2 GetDir(SpawnPointData spawn, NPC npc, Player plr, Vector2 spawnPos, int fireIndex, int stack, float angle, float rotate)
    {
        Vector2 dir;

        if (spawn.UseNpcDir && npc != null)
        {
            // 使用NPC朝向
            dir = new Vector2(npc.direction, 0);
            if (dir == Vector2.Zero) dir = plr.Center - spawnPos;
        }
        else
        {
            // 根据射向模式计算方向
            dir = GetAimDirection(spawn, npc, plr, spawnPos);
        }

        if (dir == Vector2.Zero)
            dir = plr.Center - spawnPos;
        else
            dir = dir.SafeNormalize(Vector2.Zero);

        return ApplyAngle(dir, angle, rotate, stack, fireIndex);
    }
    #endregion

    #region 计算射向方向
    private static Vector2 GetAimDirection(SpawnPointData spawn, NPC npc, Player plr, Vector2 spawnPos)
    {
        if (spawn == null) return plr.Center - spawnPos;

        switch (spawn.AimMode)
        {
            case 0: // 射向玩家
                if (plr != null && plr.active)
                    return plr.Center - spawnPos;
                break;
            case 1: // 射向NPC
                return npc.Center - spawnPos;
            case 2: // 固定方向
                return spawn.FixedDir;
        }

        // 默认方向：射向玩家
        return plr.Center - spawnPos;
    } 
    #endregion

    #region 应用角度和旋转
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
            if (proj == null || !proj.active)
            {
                continue;
            }

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
        float decay = GetDecay(proj, fireIndex);
        return dir * (proj.Velocity * decay);
    }

    private static Vector2 GetUpdateVel(UpdateProjData update, Projectile proj, SpawnProjData projData, UpdateProjInfo info, NPC npc, Player plr, ref List<int> updateList)
    {
        float decay = GetDecay(update, info.FireIndex, projData.stack);
        float speed = update.Velocity != 0 ? update.Velocity * decay : proj.velocity.Length();

        Vector2 vel = proj.velocity.SafeNormalize(Vector2.Zero) * speed;

        // 如果设置了新的生成点配置，重新计算方向
        if (update.SpawnPoint != null && update.SpawnPoint.Count > 0)
        {
            int spawnIndex = info.SpawnIndex % update.SpawnPoint.Count;
            SpawnPointData spawnConfig = update.SpawnPoint[spawnIndex];

            Vector2 newDir;

            if (spawnConfig.UseNpcDir && npc != null)
            {
                // 使用NPC朝向
                newDir = new Vector2(npc.direction, 0);
                if (newDir == Vector2.Zero) newDir = new Vector2(1, 0);
            }
            else
            {
                // 根据射向模式计算方向
                newDir = GetAimDirection(spawnConfig, npc, plr, proj.Center);
            }

            if (newDir != Vector2.Zero)
            {
                vel = newDir.SafeNormalize(Vector2.Zero) * speed;
                Add(updateList, proj.whoAmI);
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

                Vector2 dir = targetPos - proj.Center;
                dir = dir.SafeNormalize(Vector2.Zero);

                float currAngle = (float)Math.Atan2(vel.Y, vel.X);
                float targetAngle = (float)Math.Atan2(dir.Y, dir.X);
                float angleDiff = targetAngle - currAngle;

                while (angleDiff > Math.PI) angleDiff -= (float)Math.PI * 2;
                while (angleDiff < -Math.PI) angleDiff += (float)Math.PI * 2;

                float maxAngle = MathHelper.ToRadians(update.MaxHomingAngle);
                angleDiff = MathHelper.Clamp(angleDiff, -maxAngle, maxAngle);

                float newAngle = currAngle + angleDiff * update.HomingStrength;
                vel = new Vector2((float)Math.Cos(newAngle), (float)Math.Sin(newAngle)) * vel.Length();
                Add(updateList, proj.whoAmI);
            }
        }

        // 应用角度和旋转
        vel = ApplyAngle(vel, update.Angle, update.Rotate, projData.stack, info.FireIndex);

        return vel;
    }
    #endregion

    #region 计算衰减
    private static float GetDecay(SpawnProjData proj, int fireIndex)
    {
        if (!proj.decayForStack)
            return proj.decay * proj.DecayMultiplier;

        return 1.0f - fireIndex / (float)proj.stack * (proj.decay * proj.DecayMultiplier);
    }

    private static float GetDecay(UpdateProjData update, int fireIndex, int stack)
    {
        if (!update.decayForStack)
            return update.decay * update.DecayMultiplier;

        return 1.0f - fireIndex / (float)stack * (update.decay * update.DecayMultiplier);
    } 
    #endregion

    #region 添加需要更新的弹幕索引到列表
    private static void Add(List<int> UpList, int index)
    {
        if (!UpList.Contains(index))
        {
            UpList.Add(index);
        }
    }
    #endregion

    #region 移动到下一个要发射的弹幕方法
    private static void Next(List<SpawnProjData> data, SpawnProjInfo state)
    {
        // 确保当前索引的键存在
        if (!state.SendStack.ContainsKey(state.Index)) state.SendStack[state.Index] = 0;

        if (state.SendStack[state.Index] >= data[state.Index].stack)
        {
            state.Index++;
            if (state.Index >= data.Count)
            {
                state.Index = 0;
            }
            state.SendStack[state.Index] = 0;
            state.SpawnCooldowns[state.Index] = 0f;
            state.SpawnIndex[state.Index] = 0;
            state.SendCooldowns[state.Index] = 0f;
            state.SPCount++; //增加弹幕生成次数
            state.ClearOld(state.Index);
        }
    }
    #endregion

    #region 记录当前弹幕组的结束位置 
    private static void RecordEndPos(SpawnProjInfo state, SpawnProjData proj, NPC npc, Player plr)
    {
        if (state.SendStack.ContainsKey(state.Index) && state.SendStack[state.Index] > 0)
        {
            int lastIndex = state.SendStack[state.Index] - 1;
            if (proj.SpawnPoint != null && proj.SpawnPoint.Count > 0)
            {
                int spawnIndex = state.SpawnIndex.ContainsKey(state.Index) ? state.SpawnIndex[state.Index] : 0;
                SpawnPointData spawnConfig = proj.SpawnPoint[spawnIndex % proj.SpawnPoint.Count];
                state.LastGroupPos[state.Index] = GetSpawnPos(spawnConfig, npc, plr, null, state, state.Index, lastIndex, proj.Radius, proj.stack, false);
            }
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
    public static Dictionary<int, SpawnProjInfo> States = new Dictionary<int, SpawnProjInfo>(); // 用于存储每个NPC的弹幕生成状态
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
        // 清理无效弹幕
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

        // 清理无效NPC状态
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