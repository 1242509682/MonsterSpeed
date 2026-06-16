using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.Utilities;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

// 时间事件数据结构
public class TimerData
{
    [JsonProperty("冷却时间", Order = -1)]  // 改为独立冷却时间
    public double CD { get; set; } = 5.0;
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
    public List<MarkData> MarkerList { get; set; } = new List<MarkData>();
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

        var st = StateApi.GetState(npc);
        if (st is null) return;

        // 直接调用统一处理器（显示文本）
        Process(npc, data, data.TimerEvent, ref st.EventIndex, st.Cooldown,
            ref st.LastTextTime, data.TextInterval, data.TextGradient, data.TextRange,
            ref handled, mess, showText: true);
    }
    #endregion

    #region 处理一个事件列表
    /// <summary>
    /// 处理一个事件列表（支持冷却、条件检查、自动循环）
    /// </summary>
    /// <param name="npc">当前NPC</param>
    /// <param name="data">NPC数据配置</param>
    /// <param name="events">事件列表</param>
    /// <param name="idxRef">当前索引引用</param>
    /// <param name="cdDict">冷却时间字典</param>
    /// <param name="lastText">上次显示文本时间（可为 null，表示不显示）</param>
    /// <param name="txtInt">文本显示间隔（毫秒，0则不显示）</param>
    /// <param name="grad">是否渐变</param>
    /// <param name="range">渐变字距</param>
    /// <param name="handled">是否已处理原版AI</param>
    /// <param name="showText">是否显示冷却文本（默认 true）</param>
    public static void Process(NPC npc,
        NpcData data,
        List<TimerData> events,
        ref int idxRef,
        Dictionary<int, DateTime> cdDict,
        ref DateTime? lastText,
        double txtInt, bool grad, int range,
        ref bool handled, StringBuilder? mess = null, bool showText = true)
    {
        if (events == null || events.Count == 0) return;

        var st = StateApi.GetState(npc);
        if (st == null) return;

        // 索引范围修正
        if (idxRef < 0 || idxRef >= events.Count)
        {
            idxRef = 0;
            return;
        }

        var evt = events[idxRef];

        // 初始化冷却
        if (!cdDict.ContainsKey(idxRef))
            cdDict[idxRef] = DateTime.UtcNow;

        var elapsed = DateTime.UtcNow - cdDict[idxRef];
        double remain = Math.Max(0, evt.CD - elapsed.TotalSeconds);

        // 显示冷却文本（如果开启且 lastText 不为 null）
        if (showText && lastText.HasValue && txtInt > 0)
        {
            ShowText(npc, idxRef, events.Count, remain, ref lastText, txtInt, grad, range);
        }

        // 冷却完成 -> 进入下一个事件
        if (elapsed.TotalSeconds >= evt.CD)
        {
            if (lastText.HasValue) lastText = DateTime.UtcNow;
            Next(npc, data, events, ref idxRef, cdDict, st);
            return;
        }

        // 条件检查
        bool allow = true;
        if (!string.IsNullOrEmpty(evt.Condition))
        {
            var cond = ConditionFile.GetCondData(evt.Condition);
            Conditions.CondWork(npc, new StringBuilder(), data, cond, ref allow);
        }

        if (allow)
        {
            // 执行事件
            StartEvent(data, npc, evt, null, st, ref handled);
        }
        else
        {
            // 条件不满足 -> 跳到下一个事件
            if (lastText.HasValue) lastText = DateTime.UtcNow;
            Next(npc, data, events, ref idxRef, cdDict, st);
        }

        if (mess != null)
            AppendStatusInfo(npc, mess, data, st, evt);
    }
    #endregion

    #region 切换到下一个事件
    /// <summary>切换到下一个事件（循环）</summary>
    public static void Next(NPC npc, NpcData data, List<TimerData> events, ref int idx, Dictionary<int, DateTime> cd, NpcState st)
    {
        // 记录执行次数
        if (st.EventCounts.ContainsKey(idx))
            st.EventCounts[idx]++;
        else
            st.EventCounts[idx] = 1;

        // 重置脚本标记
        st.ScriptLoop.Remove(idx);
        st.ScriptOnec.Remove(idx);
        // 重置移动/AI状态（可根据需要调整）
        st.MoveState = new MoveState();
        st.AIState = new AIState();

        idx = (idx + 1) % events.Count;
        cd[idx] = DateTime.UtcNow;
    }
    #endregion

    #region 执行事件逻辑
    public static void StartEvent(NpcData data, NPC npc, TimerData evt, StringBuilder? mess, NpcState st, ref bool handled)
    {
        // 执行C#脚本 - 每个事件周期只执行一次
        if (!string.IsNullOrEmpty(evt.CsScript))
        {
            // 检查当前事件的脚本是否已执行过
            var Once = st.ScriptOnec.TryGetValue(st.EventIndex, out bool ok) && ok;
            bool has = st.ScriptLoop.ContainsKey(st.EventIndex);
            if (!has) st.ScriptLoop[st.EventIndex] = 0; // 标记为已执行
            if (!evt.ScriptOnce)
            {
                st.ScriptLoop[st.EventIndex]++;
                if (st.ScriptLoop[st.EventIndex] % evt.ScriptTime == 0)
                {
                    mess?.AppendLine($"循环执行脚本:{evt.CsScript}");
                    CSExecutor.SelExec(evt.CsScript, npc, data, st, mess);
                }
            }
            else if (!Once)
            {
                mess?.AppendLine($"仅执行1次脚本:{evt.CsScript}");
                CSExecutor.SelExec(evt.CsScript, npc, data, st, mess);
                st.ScriptOnec[st.EventIndex] = true;
            }
        }

        // 统一处理指示物修改（包括自身和其他NPC）
        if (evt.MarkerList != null && evt.MarkerList.Count > 0)
        {
            var rand = new UnifiedRandom();
            int Count = MarkManager.SetMstMks(evt.MarkerList, npc, ref rand);
            if (Count > 0)
                mess?.Append($" 成功修改 {Count} 个指示物\n");
        }

        // 行动模式处理
        if (!string.IsNullOrEmpty(evt.MoveMode))
            MoveManager.MoveWork(npc, data, mess, evt.MoveMode, ref handled);
        
        // 发射物品
        if (evt.ShootItemList != null && evt.ShootItemList.Count > 0)
            foreach (var item in evt.ShootItemList)
                npc.AI_87_BigMimic_ShootItem(item);

        // AI赋值
        if (evt.AIMode != null)
            AISystem.AIPairs(npc, evt.AIMode, npc.FullName, ref handled);

        // 生成怪物
        if (evt.SpawnNPC != null && evt.SpawnNPC.Count > 0)
            SpawnMonster.SpawnMonsters(data, evt.SpawnNPC, npc);

        // 生成弹幕
        if (evt.SendProj != null && evt.SendProj.Count > 0)
            foreach (var projName in evt.SendProj)
            {
                if (string.IsNullOrEmpty(projName)) continue;

                var file = SpawnProjFile.GetData(projName);
                if (file != null && file.Count > 0)
                    SpawnProj.Spawn(data, file, npc);
                else
                    TShock.Log.ConsoleError($"{LogName} 弹幕文件不存在: {projName}\n");
            }

        // Boss AI处理
        if (evt.AIMode?.BossAI != null)
            AISystem.TR_AI(evt.AIMode.BossAI, npc, ref handled);

        // 修改防御
        npc.defense = evt.Defense > 0 ? evt.Defense : npc.defDefense;
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
    /// <summary>显示冷却悬浮文本</summary>
    public static void ShowText(NPC npc, int idx, int? total, double remain, ref DateTime? last, double interval, bool grad, int range)
    {
        if (!last.HasValue) return;
        if ((DateTime.UtcNow - last.Value).TotalMilliseconds < interval) return;

        string text = $"Time {remain:F2} [{idx + 1}/{total ?? 1}]";
        Color color = Color.LightGoldenrodYellow;

        if (!grad)
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
                var gc = Color.Lerp(start, end, ratio);
                TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, text[i].ToString(),
                                     (int)gc.PackedValue,
                                     npc.position.X + (i * range),
                                     npc.position.Y - 3, 0f, 0);
            }
        }

        last = DateTime.UtcNow;
    }
    #endregion
}