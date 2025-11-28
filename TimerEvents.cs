using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;

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
    [JsonProperty("指示物修改", Order = 2)]
    public List<MstMarkerMod> MarkerList { get; set; } = new List<MstMarkerMod>();
    [JsonProperty("发射物品", Order = 3)]
    public HashSet<int> ShootItemList { get; set; } = new HashSet<int>();
    [JsonProperty("行动模式", Order = 4)]
    public MoveModeData MoveData { get; set; }
    [JsonProperty("生成怪物", Order = 5)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 6)]
    public List<string> SendProj { get; set; } = new List<string>(); 
    [JsonProperty("AI赋值", Order = 7)]
    public AIModes AIMode { get; set; }
}

internal class TimerEvents
{
    #region 时间事件主入口
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData data, ref bool handled)
    {
        if (data?.TimerEvent == null || data.TimerEvent.Count <= 0) return;

        var state = StateUtil.GetState(npc);
        if (state is null) return;

        var Event = data.TimerEvent[state.EventIndex];

        // 检查事件索引有效性
        if (state.EventIndex < 0 || state.EventIndex >= data.TimerEvent.Count)
        {
            state.EventIndex = 0;
            return;
        }

        // 初始化冷却时间
        if (!state.CooldownTime.TryGetValue(state.EventIndex, out var time)) 
        {
            state.CooldownTime[state.EventIndex] = DateTime.UtcNow;
        }

        // 显示冷却文本
        ShowCoolText(npc, data, state, Event.CoolTime);

        // 检查主事件冷却 - 使用事件的独立冷却时间
        if ((DateTime.UtcNow - time) >= TimeSpan.FromSeconds(Event.CoolTime))
        {
            NextEvent(data, npc, state); // 切换到下个事件
        }
        else
        {
            // 条件检查
            bool allow = true;

            if (!string.IsNullOrEmpty(Event.Condition))
            {
                var cond = CondFileManager.GetCondData(Event.Condition);
                Conditions.Condition(npc, mess, data, cond, ref allow);
            }
            
            if (allow) // 满足条件则执行事件
            {
                StartEvent(data, npc, Event, mess, state, ref handled);
            }
            else
            {
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

        // 重置移动状态
        state.MoveState = new MoveModeState();
        // 重置悬浮文本计时
        state.LastTextTime = DateTime.UtcNow;

        // 移动到下个事件（自动循环）
        state.EventIndex = (state.EventIndex + 1) % data.TimerEvent.Count;
        // 设置下个事件的冷却时间
        var nextEvent = data.TimerEvent[state.EventIndex];
        state.CooldownTime[state.EventIndex] = DateTime.UtcNow;

        // 确保事件索引在有效范围内
        if (state.EventIndex < 0 || state.EventIndex >= data.TimerEvent.Count)
        {
            state.EventIndex = 0;
        }
    }
    #endregion

    #region 执行事件逻辑
    public static void StartEvent(NpcData data, NPC npc, TimerData Event, StringBuilder mess, NpcState state, ref bool handled)
    {
        // 统一处理指示物修改（包括自身和其他NPC）
        if (Event.MarkerList != null && Event.MarkerList.Count > 0)
        {
            int Count = MarkerUtil.SetMstMarkers(Event.MarkerList, npc, ref Main.rand);
            if (Count > 0)
            {
                mess.Append($" 指示物修改: 成功修改 {Count} 个目标\n");
            }
        }

        // 移动模式处理
        if (Event.MoveData != null)
        {
            MoveMod.HandleMoveMode(npc, data, Event, mess, ref handled);
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

                var projfile = SpawnProjectileFile.GetData(projName);
                if (projfile != null && projfile.Count > 0)
                {
                    SpawnProjectile.SpawnProj(data, projfile, npc);
                }
                else
                {
                    mess.Append($" 弹幕文件不存在或为空: {projName}\n");
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

        // 显示关键指示物
        if (state?.Markers != null && state.Markers.Count > 0)
        {
            mess.Append($" [指示物] ");
            int count = 0;
            foreach (var marker in state.Markers)
            {
                if (count >= 3) break;
                if (marker.Key.StartsWith("phase") || marker.Key.StartsWith("count") || marker.Key.StartsWith("step"))
                {
                    mess.Append($"{marker.Key}:{marker.Value} ");
                    count++;
                }
            }
            if (count > 0) mess.Append("\n");
        }

        mess.Append($" 顺序:[c/A2E4DB:{state!.EventIndex + 1}/{data.TimerEvent.Count}] " +
                   $"血量:[c/A2E4DB:{life}%] " +
                   $"召怪:[c/A2E4DB:{snCount}] " +
                   $"弹发:[c/A2E4DB:{spCount}]\n");
    }
    #endregion

    #region 时间事件冷却倒计时方法（悬浮文本）
    public static void ShowCoolText(NPC npc, NpcData data, NpcState state, double coolTime)
    {
        if ((DateTime.UtcNow - state.LastTextTime).TotalMilliseconds < data.TextInterval)
            return;

        TimeSpan actionTime = TimeSpan.FromSeconds(coolTime) - (DateTime.UtcNow - state.CooldownTime[state.EventIndex]);
        
        // 确保时间不为负
        if (actionTime.TotalSeconds < 0)
            actionTime = TimeSpan.Zero;

        string text = $"Time {actionTime.TotalSeconds:F2} [{state.EventIndex + 1}/{data.TimerEvent?.Count ?? 1}]";
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