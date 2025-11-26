using System.Text;
using Microsoft.Xna.Framework;
using Terraria.Utilities;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.Conditions;

namespace MonsterSpeed;

#region 重构数据结构（短变量名）
public class IndiePlay
{
    [JsonProperty("事件名称")]
    public string Name { get; set; } = "";
    [JsonProperty("触发条件")]
    public string Cond { get; set; } = "默认配置";
    [JsonProperty("文件列表")]
    public List<string> Files { get; set; } = new List<string>();
    [JsonProperty("播放次数")]
    public int Count { get; set; } = 1;
    [JsonProperty("独立冷却")]
    public bool SoloCD { get; set; } = true;
    [JsonProperty("冷却时间")]
    public double CD { get; set; } = 5.0;
    [JsonProperty("强制播放")]
    public bool Force { get; set; } = false;
    [JsonProperty("限次播放")]
    public bool ByFile { get; set; } = false;
    [JsonProperty("并行执行")]
    public bool Parallel { get; set; } = false;
    [JsonProperty("优先级")]
    public int Prio { get; set; } = 0;
    [JsonProperty("标志")]
    public string Flag { get; set; } = "";
}

public class IndieState
{
    public bool Playing { get; set; } = false;
    public List<string> FileSeq { get; set; } = new List<string>();
    public int FileIdx { get; set; } = 0;
    public int EvtIdx { get; set; } = 0;
    public DateTime Timer { get; set; } = DateTime.UtcNow;
    public int Total { get; set; } = 1;
    public int Played { get; set; } = 0;
    public bool Reverse { get; set; } = false;
    public List<TimerData> Events { get; set; } = new List<TimerData>();
    public double MoreTime { get; set; } = 0;
    public bool NoCond { get; set; } = false;
    public bool ByFile { get; set; } = false;
    public bool SoloCD { get; set; } = true;
    public DateTime LastTrig { get; set; } = DateTime.MinValue;
    public string PName { get; set; } = "";

    // 新增：指示物存储
    public Dictionary<string, int> Markers { get; set; } = new Dictionary<string, int>();
}
#endregion

internal class IndieManager
{
    private static readonly object Locks = new object();

    #region 处理所有独立播放器
    public static void HandleAll(NPC npc, StringBuilder sb, NpcData data, NpcState state, ref bool handled)
    {
        if (data?.IndiePlayers == null || data.IndiePlayers.Count == 0)
            return;

        // 按优先级排序处理
        var players = data.IndiePlayers
            .Where(p => p != null)
            .OrderByDescending(p => p.Prio)
            .ToList();

        foreach (var p in players)
        {
            if (p.Parallel)
            {
                // 并行执行：立即处理不等待
                HandleSingle(npc, sb, data, state, p, ref handled);
            }
            else
            {
                // 串行执行：如果前一个播放器还在运行，跳过后续
                if (!HandleSingle(npc, sb, data, state, p, ref handled))
                    break;
            }
        }
    }
    #endregion

    #region 处理单个独立播放器
    private static bool HandleSingle(NPC npc, StringBuilder sb, NpcData data,
        NpcState state, IndiePlay play, ref bool handled)
    {
        // 使用锁防止竞争
        lock (Locks)
        {
            // 获取或创建播放器状态
            var pState = GetState(state, play.Name);

            // 如果正在播放，处理播放逻辑
            if (pState.Playing)
            {
                return HandlePlay(npc, sb, data, state, play, pState, ref handled);
            }

            // 检查冷却时间
            if ((DateTime.UtcNow - pState.LastTrig).TotalSeconds < play.CD)
                return true;

            // 检查触发条件
            bool allow = true;
            bool loop = false;

            if (!string.IsNullOrEmpty(play.Cond) && !play.Force)
            {
                var cond = CondFileManager.GetCondData(play.Cond);
                Condition(npc, sb, data, cond, ref allow, ref loop);
            }

            // 触发播放
            if (play.Force || allow)
            {
                StartPlay(npc, play, state, pState);
                pState.LastTrig = DateTime.UtcNow;
            }

            return true;
        }
    }
    #endregion

    #region 处理独立文件播放
    private static bool HandlePlay(NPC npc, StringBuilder sb, NpcData data,
        NpcState state, IndiePlay play, IndieState pState, ref bool handled)
    {
        var evt = pState.Events[pState.EvtIdx];

        // 状态验证
        if (!pState.Playing || pState.Events == null || pState.EvtIdx < 0 ||
            pState.EvtIdx >= pState.Events.Count)
        {
            ResetState(pState);
            return true;
        }

        // 计算实际冷却时间
        double activeTime = GetActiveTime(pState, play);
        TimeSpan elapsed = DateTime.UtcNow - pState.Timer;
        double remaining = Math.Max(0, activeTime - elapsed.TotalSeconds);

        // 显示冷却文本
        ShowCoolText(npc, pState, remaining, activeTime, sb);

        // 检查事件条件
        bool all = true;
        bool loop = false;
        if (!string.IsNullOrEmpty(evt.Condition) && !pState.NoCond)
        {
            var cond = CondFileManager.GetCondData(evt.Condition);
            Condition(npc, sb, data, cond, ref all, ref loop);
        }

        // 强制播放或条件满足
        if (pState.NoCond || all)
        {
            // 检查事件冷却
            if ((DateTime.UtcNow - pState.Timer).TotalSeconds >= activeTime)
            {
                // 执行指示物修改
                if (evt.MarkerMods != null && evt.MarkerMods.Count > 0)
                {
                    SetMarkers(pState, evt.MarkerMods, ref Main.rand, npc);
                }

                // 执行事件动作
                ExecuteEvt(data, npc, evt, sb, state, pState, play, ref handled);

                // 更新播放进度
                UpdateProgress(npc, state, pState, play);

                if (!pState.Playing)
                    return true;
            }
            else
            {
                // 冷却时间未到，但如果条件满足仍然执行事件
                ExecuteEvt(data, npc, evt, sb, state, pState, play, ref handled);
            }

            // 显示状态信息
            AppendStatus(npc, sb, pState, remaining);
        }
        else
        {
            // 条件不满足
            AppendStatus(npc, sb, pState, remaining);
            sb.Append($" [c/FF6B6B:独立事件条件未满足]\n");

            UpdateProgress(npc, state, pState, play);
            pState.Timer = DateTime.UtcNow;

            if (!pState.Playing)
                return true;
        }

        return true;
    }
    #endregion

    #region 执行独立事件动作
    private static void ExecuteEvt(NpcData data, NPC npc, TimerData evt,
        StringBuilder sb, NpcState state, IndieState pState, IndiePlay play, ref bool handled)
    {
        // 执行指示物修改
        if (evt.MarkerMods != null && evt.MarkerMods.Count > 0)
        {
            SetMarkers(pState, evt.MarkerMods, ref Main.rand, npc);
        }

        // 移动模式处理
        if (evt.MoveData != null)
        {
            MoveMod.HandleMoveMode(npc, data, evt, sb, ref handled);
        }

        // 发射物品
        if (evt.ShootItemList != null)
        {
            foreach (var item in evt.ShootItemList)
            {
                npc.AI_87_BigMimic_ShootItem(item);
            }
        }

        // AI赋值
        if (evt.AIMode != null)
            AISystem.AIPairs(npc, evt.AIMode, npc.FullName, ref handled);

        // 生成怪物
        if (evt.SpawnNPC != null && evt.SpawnNPC.Count > 0)
            MyMonster.SpawnMonsters(data, evt.SpawnNPC, npc);

        // 发射弹幕 - 支持多个弹幕文件同时发射
        if (evt.SendProj != null && evt.SendProj.Count > 0)
        {
            var allProj = new List<SpawnProjData>();

            foreach (var proj in evt.SendProj)
            {
                var file = ProjFileManager.GetData(proj);
                if (file != null && file.Count > 0)
                {
                    allProj.AddRange(file);
                }
                else
                {
                    sb.Append($" 弹幕文件不存在: {proj}\n");
                }
            }

            if (allProj.Count > 0)
            {
                MyProjectile.SpawnProjectile(data, allProj, npc);
            }
        }

        // Boss AI
        if (evt.AIMode?.BossAI != null)
        {
            foreach (var bossAI in evt.AIMode.BossAI)
                AISystem.TR_AI(bossAI, npc, ref handled);
        }

        // 修改防御
        npc.defense = evt.Defense > 0 ? evt.Defense : npc.defDefense;
    }
    #endregion

    #region 更新独立播放进度
    private static void UpdateProgress(NPC npc, NpcState state,
        IndieState pState, IndiePlay play)
    {
        pState.EvtIdx++;
        pState.Timer = DateTime.UtcNow;

        // 检查当前文件的事件是否已全部执行完毕
        if (pState.EvtIdx >= pState.Events.Count)
        {
            pState.EvtIdx = 0;
            pState.FileIdx++;

            // 限次播放计数
            if (pState.ByFile)
            {
                pState.Played++;
            }

            // 检查是否所有文件都已执行完毕
            if (pState.FileIdx >= pState.FileSeq.Count)
            {
                // 非限次播放计数
                if (!pState.ByFile)
                {
                    pState.Played++;
                }

                // 检查播放次数
                if (pState.Played >= pState.Total)
                {
                    ResetState(pState);
                    return;
                }
                else
                {
                    // 重新开始播放序列
                    pState.FileIdx = 0;
                    if (!LoadFile(pState, play))
                    {
                        ResetState(pState);
                        return;
                    }
                }
            }
            else
            {
                // 加载下一个文件
                if (!LoadFile(pState, play))
                {
                    ResetState(pState);
                    return;
                }
            }

            // 限次播放检查
            if (pState.ByFile && pState.Played >= pState.Total)
            {
                ResetState(pState);
                return;
            }
        }
    }
    #endregion

    #region 启动独立文件播放
    public static void StartPlay(NPC npc, IndiePlay play,
        NpcState state, IndieState pState)
    {
        try
        {
            if (play.Files == null || play.Files.Count == 0)
            {
                return;
            }

            pState.Playing = true;
            pState.FileSeq = new List<string>(play.Files);
            pState.FileIdx = 0;
            pState.EvtIdx = 0;
            pState.Timer = DateTime.UtcNow;
            pState.Total = Math.Abs(play.Count);
            pState.Played = 0;
            pState.Reverse = play.Count < 0;
            pState.MoreTime = 0;
            pState.NoCond = play.Force;
            pState.ByFile = play.ByFile;
            pState.SoloCD = play.SoloCD;
            pState.PName = play.Name;

            if (pState.Reverse)
            {
                pState.FileSeq.Reverse();
            }

            // 加载第一个文件
            if (!LoadFile(pState, play))
            {
                ResetState(pState);
                return;
            }

            // 更新播放计数
            UpdateCount(state, play.Files);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"启动独立播放器失败: {play.Name}, 错误: {ex.Message}");
            ResetState(pState);
        }
    }
    #endregion

    #region 辅助方法（线程安全）
    private static IndieState GetState(NpcState state, string name)
    {
        if (state.IndieStates == null)
            state.IndieStates = new Dictionary<string, IndieState>();

        if (!state.IndieStates.ContainsKey(name))
            state.IndieStates[name] = new IndieState();

        return state.IndieStates[name];
    }

    private static void ResetState(IndieState pState)
    {
        pState.Playing = false;
        pState.FileSeq.Clear();
        pState.FileIdx = 0;
        pState.EvtIdx = 0;
        pState.Events.Clear();
        pState.Played = 0;
        pState.Markers.Clear(); // 清理指示物防止内存泄露
    }

    private static double GetActiveTime(IndieState pState, IndiePlay player)
    {
        return pState.SoloCD ? pState.MoreTime : player.CD;
    }

    private static bool LoadFile(IndieState pState, IndiePlay player)
    {
        try
        {
            // 添加边界检查
            if (pState.FileSeq == null || pState.FileSeq.Count == 0)
            {
                return false;
            }

            if (pState.FileIdx < 0 || pState.FileIdx >= pState.FileSeq.Count)
            {
                return false;
            }

            var fileName = pState.FileSeq[pState.FileIdx];

            // 添加文件名检查
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var (events, moreTime, soloCD) = FilePlayManager.LoadEventFile(fileName);

            if (events == null || events.Count == 0)
            {
                return false;
            }

            pState.Events = events;
            pState.MoreTime = moreTime;
            pState.SoloCD = soloCD;
            pState.EvtIdx = 0;
            pState.Timer = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"文件加载失败: {player.Name}, 错误: {ex.Message}");
            return false;
        }
    }

    private static void UpdateCount(NpcState state, List<string> files)
    {
        if (state.PlayCounts == null)
            state.PlayCounts = new Dictionary<string, int>();

        foreach (string file in files)
        {
            var key = $"indie_{file}";
            if (state.PlayCounts.ContainsKey(key))
            {
                state.PlayCounts[key]++;
            }
            else
            {
                state.PlayCounts[key] = 1;
            }
        }
    }

    // 简化版指示物设置
    private static void SetMarkers(IndieState state, Dictionary<string, string[]> markers, ref UnifiedRandom rand, NPC npc = null)
    {
        if (markers == null) return;

        foreach (var marker in markers)
        {
            if (marker.Value != null && marker.Value.Length > 0)
            {
                // 简化处理：只取第一个操作
                var op = marker.Value[0];
                if (int.TryParse(op, out int value))
                {
                    state.Markers[marker.Key] = value;
                }
            }
        }
    }
    #endregion

    #region 显示相关方法
    private static void ShowCoolText(NPC npc, IndieState pState,
        double remaining, double activeTime, StringBuilder sb)
    {
        string info = "";
        if (pState.NoCond) info += "[强制]";
        if (pState.ByFile) info += "[限次]";
        if (pState.SoloCD) info += "[独立]";

        sb.Append($" {info}[{pState.PName}] " +
               $"文件:[c/A2E4DB:{pState.FileIdx + 1}/{pState.FileSeq.Count}] " +
               $"事件:[c/A2E4DB:{pState.EvtIdx + 1}/{pState.Events.Count}] " +
               $"次数:[c/A2E4DB:{pState.Played + 1}/{pState.Total}] " +
               $"冷却:[c/A2E4DB:{activeTime}秒] " +
               $"剩余:[c/A2E4DB:{remaining:F1}秒]\n");
    }

    private static void AppendStatus(NPC npc, StringBuilder sb,
        IndieState pState, double remaining)
    {
        sb.Append($" 独立播放器:[c/A2E4DB:{pState.PName}] " +
               $"进度:{pState.EvtIdx + 1}/{pState.Events.Count} " +
               $"剩余:{remaining:F1}秒\n");
    }
    #endregion
}