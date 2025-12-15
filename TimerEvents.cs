using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

// 时间事件数据结构
public class TimerData
{
    [JsonProperty("冷却时间", Order = -1)]  // 改为独立冷却时间
    public double CoolTime { get; set; } = 5.0;
    [JsonProperty("触发条件", Order = 0)]
    public string Condition { get; set; } = "默认配置";
    [JsonProperty("修改防御", Order = 1)]
    public int Defense { get; set; } = 0;
    [JsonProperty("C#脚本", Order = 2)]  // 新增字段
    public string CsScript { get; set; } = "";
    [JsonProperty("脚本循环频率", Order = 2)]
    public int ScriptTime { get; set; } = 60;
    [JsonProperty("脚本只跑一次", Order = 2)]
    public bool ScriptOnce { get; set; } = false;
    [JsonProperty("指示物修改", Order = 3)]
    public List<MstMarkerMod> MarkerList { get; set; } = new List<MstMarkerMod>();
    [JsonProperty("发射物品", Order = 4)]
    public HashSet<int> ShootItemList { get; set; } = new HashSet<int>();
    [JsonProperty("行动模式", Order = 5)]
    public string MoveMode { get; set; } = "";
    [JsonProperty("生成怪物", Order = 6)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 7)]
    public List<string> SendProj { get; set; } = new List<string>();
    [JsonProperty("AI赋值", Order = 8)]
    public AIModes AIMode { get; set; }
}

public class TimerEvents
{
    #region 时间事件主入口
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData data, ref bool handled)
    {
        if (data?.TimerEvent == null || data.TimerEvent.Count <= 0) return;

        var state = StateApi.GetState(npc);
        if (state is null) return;

        var Event = data.TimerEvent[state.EventIndex];

        // 检查事件索引有效性
        if (state.EventIndex < 0 || state.EventIndex >= data.TimerEvent.Count)
        {
            state.EventIndex = 0;
            return;
        }

        // 初始化冷却时间
        if (!state.CooldownTime.ContainsKey(state.EventIndex))
            state.CooldownTime[state.EventIndex] = DateTime.UtcNow;

        // 使用文件中定义的冷却时间
        var time = state.CooldownTime[state.EventIndex];
        TimeSpan elapsed = DateTime.UtcNow - time;
        double remaining = Math.Max(0, Event.CoolTime - elapsed.TotalSeconds);

        // 显示冷却文本
        ShowCoolText(npc, data, state,remaining);

        // 检查主事件冷却 - 使用事件的独立冷却时间
        if (elapsed >= TimeSpan.FromSeconds(Event.CoolTime))
        {
            state.LastTextTime = DateTime.UtcNow; // 重置悬浮文本计时
            NextEvent(data, npc, state); // 切换到下个事件
        }
        else
        {
            // 条件检查
            bool allow = true;

            if (!string.IsNullOrEmpty(Event.Condition))
            {
                var cond = ConditionFile.GetCondData(Event.Condition);
                Conditions.Condition(npc, mess, data, cond, ref allow);
            }

            if (allow) // 满足条件则执行事件
            {
                StartEvent(data, npc, Event, mess, state, ref handled);
            }
            else
            {
                state.LastTextTime = DateTime.UtcNow; // 重置悬浮文本计时
                NextEvent(data, npc, state); // 切换到下个事件（默认循环）
            }
        }

        // 状态信息显示
        AppendStatusInfo(npc, mess, data, state, Event);
    }
    #endregion

    #region 下个事件
    public static void NextEvent(NpcData data, NPC npc, NpcState state)
    {
        // 更新当前事件的执行次数
        if (state.EventCounts.ContainsKey(state.EventIndex))
        {
            state.EventCounts[state.EventIndex]++;
        }
        else
        {
            state.EventCounts[state.EventIndex] = 1;
        }

        state.ScriptLoop.Remove(state.EventIndex); // 重置当前事件的脚本执行标记
        state.ScriptOnec.Remove(state.EventIndex);
        state.MoveState = new MoveModeState(); // 重置移动状态
        state.AIState = new AIState(); // 重置AI赋值
        state.EventIndex = (state.EventIndex + 1) % data.TimerEvent.Count; // 移动到下个事件（自动循环）
        state.CooldownTime[state.EventIndex] = DateTime.UtcNow;
    }
    #endregion

    #region 执行事件逻辑
    public static void StartEvent(NpcData data, NPC npc, TimerData Event, StringBuilder mess, NpcState state, ref bool handled)
    {
        // 执行C#脚本 - 每个事件周期只执行一次
        if (!string.IsNullOrEmpty(Event.CsScript))
        {
            // 检查当前事件的脚本是否已执行过
            var Once = state.ScriptOnec.TryGetValue(state.EventIndex, out bool ok) && ok;
            bool has = state.ScriptLoop.ContainsKey(state.EventIndex);
            if (!has) state.ScriptLoop[state.EventIndex] = 0; // 标记为已执行
            if (!Event.ScriptOnce)
            {
                state.ScriptLoop[state.EventIndex]++;
                if (state.ScriptLoop[state.EventIndex] % Event.ScriptTime == 0)
                {
                    mess?.AppendLine($"循环执行脚本:{Event.CsScript}");
                    CSExecutor.SelExec(Event.CsScript, npc, data, state, mess);
                }
            }
            else if(!Once)
            {
                mess?.AppendLine($"仅执行1次脚本:{Event.CsScript}");
                CSExecutor.SelExec(Event.CsScript, npc, data, state, mess);
                state.ScriptOnec[state.EventIndex] = true;
            }
        }

        // 统一处理指示物修改（包括自身和其他NPC）
        if (Event.MarkerList != null && Event.MarkerList.Count > 0)
        {
            int Count = MarkerUtil.SetMstMarkers(Event.MarkerList, npc, ref Main.rand);
            if (Count > 0)
            {
                mess.Append($" 成功修改 {Count} 个指示物\n");
            }
        }

        // 行动模式处理
        if (!string.IsNullOrEmpty(Event.MoveMode))
        {
            MoveMod.MoveModes(npc, data, mess, Event.MoveMode, ref handled);
        }

        // 发射物品
        if (Event.ShootItemList != null)
        {
            foreach (var item in Event.ShootItemList)
            {
                npc.AI_87_BigMimic_ShootItem(item);
            }
        }

        // AI赋值
        if (Event.AIMode != null)
            AISystem.AIPairs(npc, Event.AIMode, npc.FullName, ref handled);

        // 生成怪物
        if (Event.SpawnNPC != null && Event.SpawnNPC.Count > 0)
            SpawnMonster.SpawnMonsters(data, Event.SpawnNPC, npc);

        // 生成弹幕
        if (Event.SendProj != null && Event.SendProj.Count > 0)
        {
            foreach (var projName in Event.SendProj)
            {
                if (string.IsNullOrEmpty(projName)) continue;

                var projfile = SpawnProjFile.GetData(projName);
                if (projfile != null && projfile.Count > 0)
                {
                    SpawnProj.Spawn(data, projfile, npc);
                }
                else
                {
                    TShock.Log.ConsoleError($"{LogName} 弹幕文件不存在或为空: {projName}\n");
                }
            }
        }

        // Boss AI处理
        if (Event.AIMode?.BossAI != null)
        {
            foreach (var bossAI in Event.AIMode.BossAI)
                AISystem.TR_AI(bossAI, npc, ref handled);
        }

        // 修改防御
        npc.defense = Event.Defense > 0 ? Event.Defense : npc.defDefense;
    }
    #endregion

    #region 状态信息显示
    public static void AppendStatusInfo(NPC npc, StringBuilder mess, NpcData data, NpcState state, TimerData timerEvent)
    {
        int spCount = state?.SPCount ?? 0;
        int snCount = state?.SNCount ?? 0;
        var life = (int)(npc.life / (float)npc.lifeMax * 100);

        mess.Append($" 顺序:[c/A2E4DB:{state!.EventIndex + 1}/{data.TimerEvent.Count}] " +
                   $"血量:[c/A2E4DB:{life}%] " +
                   $"召怪:[c/A2E4DB:{snCount}] " +
                   $"弹发:[c/A2E4DB:{spCount}]\n");
    }
    #endregion

    #region 时间事件冷却倒计时方法（悬浮文本）
    public static void ShowCoolText(NPC npc, NpcData data, NpcState state, double remaining)
    {
        if ((DateTime.UtcNow - state.LastTextTime).TotalMilliseconds < data.TextInterval)
            return;

        string text = $"Time {remaining:F2} [{state.EventIndex + 1}/{data.TimerEvent?.Count ?? 1}]";
        Color color = Color.LightGoldenrodYellow;

        if (!data.TextGradient)
        {
            TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, text,
                (int)color.PackedValue, npc.position.X, npc.position.Y - 3, 0f, 0);
        }
        else
        {
            for (int i = 0; i < text.Length; i++)
            {
                var start = new Color(166, 213, 234);
                var end = new Color(245, 247, 175);
                float ratio = (float)i / (text.Length - 1);
                var gradColor = Color.Lerp(start, end, ratio);

                TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, text[i].ToString(),
                                     (int)gradColor.PackedValue,
                                     npc.position.X + (i * data.TextRange),
                                     npc.position.Y - 3, 0f, 0);
            }
        }

        state.LastTextTime = DateTime.UtcNow;
    }
    #endregion
}