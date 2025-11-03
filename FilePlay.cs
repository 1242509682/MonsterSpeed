using System.Text;
using Newtonsoft.Json;
using Microsoft.Xna.Framework;
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
    [JsonProperty("冷却延长")]
    public double MoreActiveTime { get; set; }
    [JsonProperty("事件列表")]
    public List<TimerData> TimerEvents { get; set; } = new List<TimerData>();
}

// 文件状态管理类
public class FilePlayState
{
    public bool Playing { get; set; } = false; // 是否正在播放文件
    public List<int> FileSeq { get; set; } = new List<int>(); // 文件播放序列
    public int FileIndex { get; set; } = 0; // 当前播放的文件索引
    public int EventIndex { get; set; } = 0; // 当前播放的事件索引
    public DateTime EventTimer { get; set; } = DateTime.UtcNow; // 事件计时器
    public int TotalCount { get; set; } = 1; // 总播放次数
    public int PlayCount { get; set; } = 0; // 当前播放次数
    public bool Reverse { get; set; } = false; // 是否反向播放
    public List<TimerData> Events { get; set; } = new List<TimerData>(); // 当前文件的事件列表
    public double MoreActiveTime { get; set; } = 0; // 冷却延长
    public bool NoCond { get; set; } = false; // 无条件播放
    public bool ByFile { get; set; } = false; // 限次播放
}

internal class FilePlay
{
    #region 处理文件播放
    public static void HandleFilePlay(NPC npc, StringBuilder mess, NpcData data, int life, TimerState state, ref bool handled)
    {
        var fs = state.FileState;
        var Event = fs.Events[fs.EventIndex];

        // 状态验证
        if (fs == null || !fs.Playing ||
            fs.FileSeq == null || fs.FileSeq.Count <= 0 ||
            fs.Events == null || fs.Events.Count <= 0 ||
            fs.EventIndex < 0 || fs.EventIndex >= fs.Events.Count)
        {
            state.FileState = new FilePlayState();
            return;
        }

        // 计算文件播放模式下的实际冷却时间和剩余时间
        double ActiveTime = data.ActiveTime + fs.MoreActiveTime;
        double remaining = ActiveTime - (DateTime.UtcNow - fs.EventTimer).TotalSeconds;
        remaining = Math.Max(0, remaining); // 确保不为负数

        // 检查暂停时间 - 如果是强制播放，跳过暂停
        if (Event.PauseTime > 0 && fs.NoCond)
        {
            PauseMode(npc, mess, data, state, life, Event);
            return;
        }

        // 计算玩家距离（用于条件检查）
        float range = 0;
        Player plr = Main.player[npc.target];
        if (plr != null && plr.active && !plr.dead)
        {
            range = Vector2.Distance(plr.Center, npc.Center);
        }

        // 检查当前文件事件的条件，如果开启强制播放则跳过条件检查
        bool all = true;
        bool loop = false;
        if (!fs.NoCond) // 只有非强制播放时才检查条件
        {
            Conditions.Condition(npc, mess, data, range, life, Event, ref all, ref loop);
        }

        // 显示冷却文本
        ShowCoolText(npc, data, state);

        // 显示状态（包含剩余时间）- 调整显示顺序
        string playMode = "";
        if (fs.NoCond)
            playMode += "[强制]";
        if (fs.ByFile)
            playMode += "[限次]";

        // 强制播放或条件满足
        if (fs.NoCond || all)
        {
            // 检查事件冷却
            if ((DateTime.UtcNow - fs.EventTimer).TotalSeconds >= ActiveTime)
            {
                UpdateFilePlay(npc, state); // 更新文件播放进度
                if (!state.FileState.Playing) // 如果文件播放已完成，返回
                {
                    var curEvt = data.TimerEvent[state.Index];
                    NextEvent(data, curEvt.NextAddTimer, npc, state);
                    return;
                }
            }
            else
            {
                // 冷却时间未到，但如果条件满足仍然执行事件
                StartEvent(npc, Event, ref handled);
            }

            // 显示状态（包含剩余时间）
            mess.Append($" {playMode}文件:[c/A2E4DB:{fs.FileIndex + 1}/{fs.FileSeq.Count}] " +
                        $"事件:[c/A2E4DB:{fs.EventIndex + 1}/{fs.Events.Count}] " +
                        $"次数:[c/A2E4DB:{fs.PlayCount + 1}/{fs.TotalCount}] " +
                        $"剩余:[c/A2E4DB:{remaining:F1}秒]\n");

            // 不是暂停状态时显示血量和召怪弹发数量
            if (!state.PauseState.Paused)
            {
                // 获取召怪和弹发数量用于广播显示
                int spCount = MyProjectile.GetState(npc)?.SPCount ?? 0;
                int snCount = MyMonster.GetState(npc)?.SNCount ?? 0;
                mess.Append($" 血量:[c/A2E4DB:{life}%] 召怪:[c/A2E4DB:{snCount}] 弹发:[c/A2E4DB:{spCount}]\n");
            }
        }
        else
        {
            // 显示状态（包含剩余时间）
            mess.Append($" {playMode}文件:[c/A2E4DB:{fs.FileIndex + 1}/{fs.FileSeq.Count}] " +
                        $"事件:[c/A2E4DB:{fs.EventIndex + 1}/{fs.Events.Count}] " +
                        $"次数:[c/A2E4DB:{fs.PlayCount + 1}/{fs.TotalCount}] " +
                        $"剩余:[c/A2E4DB:{remaining:F1}秒]\n");

            // 不是暂停状态时显示血量和召怪弹发数量
            if (!state.PauseState.Paused)
            {
                // 获取召怪和弹发数量用于广播显示
                int spCount = MyProjectile.GetState(npc)?.SPCount ?? 0;
                int snCount = MyMonster.GetState(npc)?.SNCount ?? 0;
                mess.Append($" 血量:[c/A2E4DB:{life}%] 召怪:[c/A2E4DB:{snCount}] 弹发:[c/A2E4DB:{spCount}]\n");
            }

            mess.Append($" [c/FF6B6B:文件事件条件未满足]\n");

            UpdateFilePlay(npc, state);  // 更新文件播放进度
            fs.EventTimer = DateTime.UtcNow; // 重置计时器，等待下一次检查

            // 如果文件播放已完成，前进到下一个主事件
            if (!state.FileState.Playing)
            {
                NextEvent(data, data.TimerEvent[state.Index].NextAddTimer, npc, state);
            }
        }
    }
    #endregion

    #region 更新文件播放进度
    public static void UpdateFilePlay(NPC npc, TimerState state)
    {
        var fs = state.FileState;

        // 前进到下一个事件
        fs.EventIndex++;
        fs.EventTimer = DateTime.UtcNow;

        // 检查当前文件的事件是否已全部执行完毕
        if (fs.EventIndex >= fs.Events.Count)
        {
            // 当前文件执行完毕，移动到下一个文件
            fs.EventIndex = 0;
            fs.FileIndex++;

            // 如果开启限次播放，每次文件播放完毕就增加播放计数
            if (fs.ByFile)
            {
                fs.PlayCount++;
            }

            // 检查是否所有文件都已执行完毕
            if (fs.FileIndex >= fs.FileSeq.Count)
            {
                // 如果没有开启限次播放，则在所有文件播放完毕后增加播放计数
                if (!fs.ByFile)
                {
                    fs.PlayCount++;
                }

                //  当前播放次数超过总次数
                if (fs.PlayCount >= fs.TotalCount)
                {
                    // 文件播放完成，重置状态
                    state.FileState = new FilePlayState();
                    return;
                }
                else
                {
                    // 重新开始播放序列
                    fs.FileIndex = 0;
                    if (!LoadNextFile(fs, npc.FullName))
                    {
                        state.FileState = new FilePlayState();
                        return;
                    }
                }
            }
            else
            {
                // 加载下一个文件
                if (!LoadNextFile(fs, npc.FullName))
                {
                    state.FileState = new FilePlayState();
                    return;
                }
            }

            // 如果开启限次播放，检查是否达到播放次数
            if (fs.ByFile && fs.PlayCount >= fs.TotalCount)
            {
                state.FileState = new FilePlayState();
                return;
            }
        }
    }
    #endregion

    #region 开始文件播放
    public static void StartFilePlay(string npcName, List<int> fileList, int playCount, bool noCond, bool byFile, TimerState state)
    {
        try
        {
            // 直接创建新的 FileState 并赋值给 state
            var fs = new FilePlayState();

            fs.Playing = true;
            fs.FileSeq = new List<int>(fileList);
            fs.FileIndex = 0;
            fs.EventIndex = 0;
            fs.EventTimer = DateTime.UtcNow;
            fs.TotalCount = Math.Abs(playCount);
            fs.PlayCount = 0;
            fs.Reverse = playCount < 0;
            fs.MoreActiveTime = 0;
            fs.NoCond = noCond;
            fs.ByFile = byFile;

            if (fs.Reverse)
            {
                fs.FileSeq.Reverse();
            }

            // 加载第一个文件
            if (!LoadNextFile(fs, npcName))
            {
                TShock.Log.ConsoleError($"文件播放器启动失败: 无法加载文件");
                state.FileState = new FilePlayState();
                return;
            }

            state.FileState = fs;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"启动文件播放器失败: {npcName}, 错误: {ex.Message}");
            state.FileState = new FilePlayState();
        }
    }
    #endregion

    #region 加载下一个文件播放
    private static bool LoadNextFile(FilePlayState fs, string npcName)
    {
        try
        {
            var fileNum = fs.FileSeq[fs.FileIndex];

            var (events, moreActiveTime) = LoadEventFile(fileNum);

            if (events == null || events.Count == 0)
            {
                TShock.Log.ConsoleError($"文件加载失败: 文件{fileNum} 不存在或为空");
                return false;
            }

            fs.Events = events;
            fs.MoreActiveTime = moreActiveTime; // 设置冷却延长
            fs.EventIndex = 0;
            fs.EventTimer = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"文件加载失败: {npcName}, 错误: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region 加载文件播放（核心方法）
    public static (List<TimerData>? events, double moreActiveTime) LoadEventFile(int fileNum)
    {
        try
        {
            var dir = Path.Combine("tshock", "怪物加速_时间事件集");
            if (!Directory.Exists(dir))
            {
                TShock.Log.ConsoleError($"事件文件夹不存在: {dir}");
                return (null, 0);
            }

            var pattern = $"{fileNum}.*.json";
            var files = Directory.GetFiles(dir, pattern);

            if (files.Length == 0)
            {
                TShock.Log.ConsoleError($"未找到文件: {fileNum}");
                return (null, 0);
            }

            var filePath = files[0];
            var content = File.ReadAllText(filePath);
            var fileData = JsonConvert.DeserializeObject<EventFileData>(content);

            if (fileData?.TimerEvents == null || fileData.TimerEvents.Count == 0)
            {
                TShock.Log.ConsoleError($"文件内容错误: {filePath}");
                return (null, 0);
            }

            return (fileData.TimerEvents, fileData.MoreActiveTime);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"加载文件失败: {fileNum}, 错误: {ex.Message}");
            return (null, 0);
        }
    }
    #endregion
}
