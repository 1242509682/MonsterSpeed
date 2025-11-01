using System.Text;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.TimerEvents;

namespace MonsterSpeed;

// 事件文件数据结构
public class EventFileData
{
    [JsonProperty("事件名称")]
    public string EventName { get; set; } = "未命名事件";
    [JsonProperty("事件列表")]
    public List<TimerData> TimerEvents { get; set; } = new List<TimerData>();
}

// 文件播放器状态类
public class FilePlayState
{
    public bool IsPlaying { get; set; } = false;
    public List<int> FileSequence { get; set; } = new List<int>();
    public int CurrentFileIndex { get; set; } = 0;
    public int PlayCount { get; set; } = 0;
    public bool ReverseMode { get; set; } = false;
    public List<TimerData> CurrentEvents { get; set; } = new List<TimerData>();
    public int CurrentEventIndex { get; set; } = 0;
    public DateTime EventTimer { get; set; } = DateTime.UtcNow;
}

internal class FilePlay
{
    #region 文件播放器实现
    public static Dictionary<string, FilePlayState> FilePlayStates = new Dictionary<string, FilePlayState>();

    /// <summary>
    /// 启动文件播放器
    /// </summary>
    public static void StartFilePlay(string npcName, List<int> fileJumps, int loopCount)
    {
        try
        {
            TShock.Log.ConsoleInfo($"尝试启动文件播放器: {npcName}, 文件列表: {string.Join(",", fileJumps)}, 播放次数: {loopCount}");

            var state = new FilePlayState
            {
                IsPlaying = true,
                FileSequence = new List<int>(fileJumps),
                PlayCount = 0,
                ReverseMode = loopCount == -1,
                CurrentFileIndex = 0,
                CurrentEventIndex = 0,
                EventTimer = DateTime.UtcNow
            };

            // 如果是倒序模式，反转文件序列
            if (state.ReverseMode)
            {
                state.FileSequence.Reverse();
                TShock.Log.ConsoleInfo($"倒序模式，反转文件序列: {string.Join(",", state.FileSequence)}");
            }

            // 加载第一个文件
            if (!LoadNextFile(npcName, state))
            {
                // 如果加载失败，停止播放
                FilePlayStates.Remove(npcName);
                TShock.Log.ConsoleError($"文件播放器启动失败: 无法加载第一个文件");
                return;
            }

            FilePlayStates[npcName] = state;
            TShock.Log.ConsoleInfo($"启动文件播放器: {npcName}, 文件数: {fileJumps.Count}, 模式: {(loopCount == -1 ? "倒序播放一次" : "播放一次")}");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"启动文件播放器失败: {npcName}, 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理文件播放器逻辑
    /// </summary>
    public static void HandleFilePlay(NPC npc, StringBuilder mess, NpcData data, int life)
    {
        var npcName = npc.FullName;
        if (!FilePlayStates.ContainsKey(npcName))
        {
            TShock.Log.ConsoleError($"文件播放器状态不存在: {npcName}");
            return;
        }

        var state = FilePlayStates[npcName];
        // 检查当前事件是否有效
        if (state.CurrentEvents == null || state.CurrentEvents.Count == 0)
        {
            TShock.Log.ConsoleError($"当前事件列表为空: {npcName}");
            StopFilePlay(npcName);
            return;
        }

        if (state.CurrentEventIndex >= state.CurrentEvents.Count)
        {
            TShock.Log.ConsoleError($"事件索引越界: {npcName}, 索引: {state.CurrentEventIndex}, 总数: {state.CurrentEvents.Count}");
            StopFilePlay(npcName);
            return;
        }

        var Event = state.CurrentEvents[state.CurrentEventIndex];

        // 检查事件冷却 - 使用BOSS的ActiveTime
        if ((DateTime.UtcNow - state.EventTimer).TotalSeconds >= data.ActiveTime)
        {
            TShock.Log.ConsoleInfo($"执行文件事件: {npcName}, 文件:{state.CurrentFileIndex + 1}, 事件:{state.CurrentEventIndex + 1}");

            // 执行当前事件
            ExecuteEvent(npc, Event);

            // 前进到下一个事件
            state.CurrentEventIndex++;

            // 如果当前文件的事件执行完毕
            if (state.CurrentEventIndex >= state.CurrentEvents.Count)
            {
                state.CurrentEventIndex = 0;
                state.CurrentFileIndex++;

                // 如果所有文件执行完毕
                if (state.CurrentFileIndex >= state.FileSequence.Count)
                {
                    TShock.Log.ConsoleInfo($"所有文件播放完毕: {npcName}");
                    // 停止文件播放器
                    StopFilePlay(npcName);

                    // 继续正常事件流程
                    var (CD_Count, CD_Timer) = GetIndex_SetTime(npcName);
                    var origi = data.TimerEvent[CD_Count];
                    NextEvent(ref CD_Count, ref CD_Timer, data, origi.Timer, npcName);
                    return;
                }
                else
                {
                    // 加载下一个文件
                    if (!LoadNextFile(npcName, state))
                    {
                        TShock.Log.ConsoleError($"加载下一个文件失败: {npcName}");
                        StopFilePlay(npcName);
                        return;
                    }
                }
            }

            state.EventTimer = DateTime.UtcNow;
        }

        // 显示文件播放器状态
        mess.Append($" [文件播放:{state.CurrentFileIndex + 1}/{state.FileSequence.Count}]");
        mess.Append($" 事件:{state.CurrentEventIndex + 1}/{state.CurrentEvents.Count}");
        mess.Append($" 血量:[c/A2E4DB:{life}%]");
        mess.Append($" 召怪:[c/A2E4DB:{MyMonster.SNCount}]");
        mess.Append($" 弹发:[c/A2E4DB:{MyProjectile.SPCount}]\n");
    }

    /// <summary>
    /// 加载下一个文件
    /// </summary>
    public static bool LoadNextFile(string npcName, FilePlayState state)
    {
        try
        {
            var fileNumber = state.FileSequence[state.CurrentFileIndex];
            TShock.Log.ConsoleInfo("————————");
            TShock.Log.ConsoleInfo($"尝试加载文件: {fileNumber}");

            var events = LoadEventFile(fileNumber);

            if (events == null || events.Count == 0)
            {
                TShock.Log.ConsoleError($"文件播放器加载失败: 文件序号 {fileNumber} 不存在或为空");
                return false;
            }

            state.CurrentEvents = events;
            state.CurrentEventIndex = 0;
            state.EventTimer = DateTime.UtcNow;

            TShock.Log.ConsoleInfo($"文件播放器加载文件: {npcName} -> 文件{fileNumber}, 事件数: {events.Count}");
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"文件播放器加载文件失败: {npcName}, 错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 停止文件播放器
    /// </summary>
    public static void StopFilePlay(string npcName)
    {
        if (FilePlayStates.ContainsKey(npcName))
        {
            FilePlayStates.Remove(npcName);
            TShock.Log.ConsoleInfo($"文件播放器停止: {npcName}");
        }
    }
    #endregion

    #region 加载事件文件（根据序号）
    public static List<TimerData>? LoadEventFile(int fileNumber)
    {
        try
        {
            var directory = Path.Combine("tshock", "怪物加速_时间事件集");
            if (!Directory.Exists(directory))
            {
                TShock.Log.ConsoleError($"事件文件夹不存在: {directory}");
                return null;
            }

            // 查找以指定序号开头的文件
            var pattern = $"{fileNumber}.*.json";
            var files = Directory.GetFiles(directory, pattern);

            if (files.Length == 0)
            {
                TShock.Log.ConsoleError($"未找到序号为 {fileNumber} 的事件文件");
                return null;
            }

            // 取第一个匹配的文件
            var filePath = files[0];
            var fileContent = File.ReadAllText(filePath);
            var eventFile = JsonConvert.DeserializeObject<EventFileData>(fileContent);
            if (eventFile?.TimerEvents == null || eventFile.TimerEvents.Count == 0)
            {
                TShock.Log.ConsoleError($"事件文件内容为空或格式错误: {filePath}");
                return null;
            }
            TShock.Log.ConsoleInfo($"成功加载事件文件: {Path.GetFileName(filePath)}");
            return eventFile?.TimerEvents;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"加载事件文件失败: {fileNumber}, 错误: {ex.Message}");
            return null;
        }
    }
    #endregion
}
