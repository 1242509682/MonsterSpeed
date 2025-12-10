using System.Text;
using Newtonsoft.Json;
using Terraria;
using Terraria.Utilities;
using TShockAPI;
using static MonsterSpeed.Conditions;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

// 事件文件数据结构
public class EventFileData
{
    [JsonProperty("事件名称")]
    public string EventName { get; set; } = "未命名事件";
    [JsonProperty("事件列表")]
    public List<TimerData> TimerEvents { get; set; } = new List<TimerData>();
}

public class FilePlayData
{
    [JsonProperty("名称")]
    public string Name { get; set; } = "";
    [JsonProperty("标志")]
    public string Flag { get; set; } = "";
    [JsonProperty("优先级")]
    public int Prio { get; set; } = 0;
    [JsonProperty("触发条件")]
    public string Cond { get; set; } = "默认配置";
    [JsonProperty("文件列表")]
    public List<int> Files { get; set; } = new List<int>();
    [JsonProperty("播放次数")]
    public int Count { get; set; } = 1;
    [JsonProperty("强制播放")]
    public bool Force { get; set; } = false;
    [JsonProperty("限次播放")]
    public bool ByFile { get; set; } = false;
    [JsonProperty("并行执行")]
    public bool Parallel { get; set; } = false;
}

public class FilePlayState
{
    public bool Playing { get; set; } = false;
    public List<int> FileSeq { get; set; } = new List<int>();
    public int FileIdx { get; set; } = 0;
    public int EvtIdx { get; set; } = 0;
    public DateTime Timer { get; set; } = DateTime.UtcNow;
    public int Total { get; set; } = 1;
    public int Played { get; set; } = 0;
    public bool Reverse { get; set; } = false;
    public List<TimerData> Events { get; set; } = new List<TimerData>();
    public bool NoCond { get; set; } = false;
    public bool ByFile { get; set; } = false;
    public DateTime LastTrig { get; set; } = DateTime.MinValue;
    public string PName { get; set; } = "";
    public Dictionary<string, int> Markers { get; set; } = new Dictionary<string, int>();
}

public class FilePlayManager
{
    private static readonly object Locks = new object();

    #region 处理所有执行文件
    public static void HandleAll(NPC npc, StringBuilder sb, NpcData data, NpcState state, ref bool handled)
    {
        if (data?.FilePlay == null || data.FilePlay.Count == 0)
            return;

        // 按优先级排序处理
        var players = data.FilePlay
            .Where(p => p != null)
            .OrderByDescending(p => p.Prio)
            .ToList();

        foreach (var play in players)
        {
            if (play.Parallel)
            {
                // 并行执行：立即处理不等待
                HandleSingle(npc, sb, data, state, play, ref handled);
            }
            else
            {
                // 串行执行：如果前一个播放器还在运行，跳过后续
                if (!HandleSingle(npc, sb, data, state, play, ref handled))
                    break;
            }
        }
    }
    #endregion

    #region 处理单个执行文件
    private static bool HandleSingle(NPC npc, StringBuilder sb, NpcData data,
        NpcState state, FilePlayData play, ref bool handled)
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

            // 检查触发条件
            bool allow = true;

            if (!string.IsNullOrEmpty(play.Cond) && !play.Force)
            {
                var cond = ConditionFile.GetCondData(play.Cond);
                Condition(npc, sb, data, cond, ref allow);
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

    #region 处理执行文件
    private static bool HandlePlay(NPC npc, StringBuilder sb, NpcData data,
        NpcState state, FilePlayData play, FilePlayState pState, ref bool handled)
    {
        var evt = pState.Events[pState.EvtIdx];

        // 状态验证
        if (!pState.Playing || pState.Events == null || pState.EvtIdx < 0 ||
            pState.EvtIdx >= pState.Events.Count)
        {
            ResetState(pState);
            return true;
        }

        // 使用文件中定义的冷却时间
        double coolTime = evt.CoolTime;
        TimeSpan elapsed = DateTime.UtcNow - pState.Timer;
        double remaining = Math.Max(0, coolTime - elapsed.TotalSeconds);

        // 显示冷却文本
        ShowCoolText(npc, pState, remaining, coolTime, sb);

        // 检查事件条件
        bool allow = true;
        if (!string.IsNullOrEmpty(evt.Condition) && !pState.NoCond)
        {
            var cond = ConditionFile.GetCondData(evt.Condition);
            Condition(npc, sb, data, cond, ref allow);
        }

        // 强制播放或条件满足
        if (pState.NoCond || allow)
        {
            // 检查事件冷却
            if ((DateTime.UtcNow - pState.Timer).TotalSeconds >= coolTime)
            {
                // 更新播放进度
                Next(npc, state, pState, play);

                if (!pState.Playing)
                    return true;
            }
            else
            {
                // 冷却时间未到，但如果条件满足仍然执行事件
                StartEvent(data, npc, evt, sb, state, pState, play, ref handled);
            }

            // 显示状态信息
            AppendStatus(npc, sb, pState, remaining);
        }
        else
        {
            // 条件不满足
            AppendStatus(npc, sb, pState, remaining);
            sb.Append($" [c/FF6B6B:执行文件事件条件未满足]\n");

            Next(npc, state, pState, play);
            pState.Timer = DateTime.UtcNow;

            if (!pState.Playing)
                return true;
        }

        return true;
    }
    #endregion

    #region 执行执行文件动作
    private static void StartEvent(NpcData data, NPC npc, TimerData evt,
        StringBuilder sb, NpcState state, FilePlayState pState, FilePlayData play, ref bool handled)
    {
        // 执行C#脚本 - 使用文件播放器专用键
        if (!string.IsNullOrEmpty(evt.CsScript))
        {
            // 创建唯一的键：文件播放器名称 + 文件序号 + 事件索引
            string key = $"{pState.PName}_{pState.FileSeq[pState.FileIdx]}_{pState.EvtIdx}";
            int hashKey = key.GetHashCode();

            // 检查是否已执行过
            bool has = state.ScriptLoop.ContainsKey(hashKey);
            if (!has) state.ScriptLoop[hashKey] = 0;

            state.ScriptLoop[hashKey]++;
            if (state.ScriptLoop[hashKey] % evt.ScriptTime == 0)
            {
                sb?.AppendLine($" 执行脚本:{evt.CsScript}");
                AsyncExec.Exec(evt.CsScript, npc, data, state, sb, evt.AsyncExec);
                state.ScriptLoop[hashKey] = 0;
            }
        }

        // 执行指示物修改
        if (evt.MarkerList != null && evt.MarkerList.Count > 0)
        {
            int count = MarkerUtil.SetMstMarkers(evt.MarkerList, npc, ref Main.rand);
            if (count > 0)
            {
                sb.Append($" 指示物修改: 成功修改 {count} 个目标\n");
            }
        }

        // 行动模式处理
        if (!string.IsNullOrEmpty(evt.MoveMode))
        {
            MoveMod.MoveModes(npc, data, sb, evt.MoveMode, ref handled);
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
            SpawnMonster.SpawnMonsters(data, evt.SpawnNPC, npc);

        // 发射弹幕 - 支持多个弹幕文件同时发射
        if (evt.SendProj != null && evt.SendProj.Count > 0)
        {
            var allProj = new List<SpawnProjData>();

            foreach (var proj in evt.SendProj)
            {
                var file = SpawnProjFile.GetData(proj);
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
                SpawnProj.Spawn(data, allProj, npc);
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

    #region 更新执行文件进度
    private static void Next(NPC npc, NpcState state,
        FilePlayState pState, FilePlayData play)
    {
        string scriptKey = $"{pState.PName}_{pState.FileSeq[pState.FileIdx]}_{pState.EvtIdx}";
        state.ScriptLoop.Remove(scriptKey.GetHashCode());

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

    #region 启动执行文件播放
    public static void StartPlay(NPC npc, FilePlayData play,
        NpcState state, FilePlayState pState)
    {
        try
        {
            if (play.Files == null || play.Files.Count == 0)
            {
                return;
            }

            pState.Playing = true;
            pState.FileSeq = new List<int>(play.Files);
            pState.FileIdx = 0;
            pState.EvtIdx = 0;
            pState.Timer = DateTime.UtcNow;
            pState.Total = Math.Abs(play.Count);
            pState.Played = 0;
            pState.Reverse = play.Count < 0;
            pState.NoCond = play.Force;
            pState.ByFile = play.ByFile;
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
            TShock.Log.ConsoleError($"启动执行文件失败: {play.Name}, 错误: {ex.Message}");
            ResetState(pState);
        }
    }
    #endregion

    #region 辅助方法（线程安全）
    private static FilePlayState GetState(NpcState state, string name)
    {
        if (state.IndieStates == null)
            state.IndieStates = new Dictionary<string, FilePlayState>();

        if (!state.IndieStates.ContainsKey(name))
            state.IndieStates[name] = new FilePlayState();

        return state.IndieStates[name];
    }

    private static void ResetState(FilePlayState pState)
    {
        pState.Playing = false;
        pState.FileSeq.Clear();
        pState.FileIdx = 0;
        pState.EvtIdx = 0;
        pState.Events.Clear();
        pState.Played = 0;
        pState.Markers.Clear(); // 清理指示物防止内存泄露
    }

    private static bool LoadFile(FilePlayState pState, FilePlayData play)
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

            // 修改：不再需要 moreTime 和 soloCD 参数
            var events = LoadEventsFromFile(fileName);

            if (events == null || events.Count == 0)
            {
                return false;
            }

            pState.Events = events;
            pState.EvtIdx = 0;
            pState.Timer = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"文件加载失败: {play.Name}, 错误: {ex.Message}");
            return false;
        }
    }

    // 新增：简化版文件加载方法
    private static List<TimerData> LoadEventsFromFile(int fileNumber)
    {
        try
        {
            var dir = Path.Combine(Paths, "时间事件");
            if (!Directory.Exists(dir))
                return new List<TimerData>();

            // 查找匹配的文件
            var files = Directory.GetFiles(dir, "*.json")
            .Where(f => Path.GetFileName(f).StartsWith(fileNumber + "."))
            .ToList();

            if (files.Count == 0)
            {
                TShock.Log.ConsoleError($"未找到序号为 {fileNumber} 的事件文件");
                return new List<TimerData>();
            }

            // 如果有多个匹配，取第一个
            var filePath = files[0];
            var content = File.ReadAllText(filePath);
            var fileData = JsonConvert.DeserializeObject<EventFileData>(content);

            return fileData?.TimerEvents ?? new List<TimerData>();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"加载执行文件失败: {fileNumber}, 错误: {ex.Message}");
            return new List<TimerData>();
        }
    }

    private static void UpdateCount(NpcState state, List<int> files)
    {
        if (state.PlayCounts == null)
            state.PlayCounts = new Dictionary<string, int>();

        foreach (int file in files)
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
    private static void SetMarkers(FilePlayState state, Dictionary<string, string[]> markers, ref UnifiedRandom rand, NPC npc = null)
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
    private static void ShowCoolText(NPC npc, FilePlayState pState,
        double remaining, double coolTime, StringBuilder sb)
    {
        string info = "";
        if (pState.NoCond) info += "[强制]";
        if (pState.ByFile) info += "[限次]";

        sb.Append($" {info}[{pState.PName}] " +
               $"序号:[c/A2E4DB:{pState.FileSeq[pState.FileIdx]}] " + // 显示文件序号
               $"文件:[c/A2E4DB:{pState.FileIdx + 1}/{pState.FileSeq.Count}] " +
               $"事件:[c/A2E4DB:{pState.EvtIdx + 1}/{pState.Events.Count}] " +
               $"次数:[c/A2E4DB:{pState.Played + 1}/{pState.Total}] " +
               $"冷却:[c/A2E4DB:{coolTime}秒] " +
               $"剩余:[c/A2E4DB:{remaining:F1}秒]\n");
    }

    private static void AppendStatus(NPC npc, StringBuilder sb,
        FilePlayState pState, double remaining)
    {
        var num = pState.FileSeq.Count > pState.FileIdx ?
            pState.FileSeq[pState.FileIdx].ToString() : "未知";

        sb.Append($" 执行文件序号:[c/A2E4DB:{num}] " +
               $"进度:{pState.EvtIdx + 1}/{pState.Events.Count} " +
               $"剩余:{remaining:F1}秒\n");
    }
    #endregion
}