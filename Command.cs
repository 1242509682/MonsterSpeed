using Newtonsoft.Json;
using System.Text;
using Terraria;
using TShockAPI;
using Microsoft.Xna.Framework;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

internal class Command
{
    public static void CMD(CommandArgs args)
    {
        if (args.Parameters.Count == 0)
        {
            args.Player.SendMessage("《怪物加速》\n" +
                "/mos hide —— 显示与隐藏多余配置项\n" +
                "/mos all —— 导出所有BOSS时间事件\n" +
                "/mos now —— 导出当前BOSS当前事件\n" +
                "/mos list —— 列出时间事件文件夹中的文件\n" +
                "/mos clear —— 清空时间事件文件夹\n" +
                "/mos reset —— 清空主配置数据(保留参考)\n" +
                "/reload —— 重载配置文件", 240, 250, 150);
            return;
        }

        if (args.Parameters.Count == 1 && Config.Dict != null)
        {
            if (args.Parameters[0].ToLower() == "hide")
            {
                // 切换当前配置的隐藏设置
                Config.HideConfig = !Config.HideConfig;
                Config.Write();
                args.Player.SendSuccessMessage($"已切换多余配置项显示状态为: {(Config.HideConfig ? "隐藏" : "显示")}。");
                return;
            }

            if (args.Parameters[0].ToLower() == "all")
            {
                // 导出所有BOSS时间事件
                ExportTimerEvents(args);
                return;
            }

            if (args.Parameters[0].ToLower() == "now")
            {
                // 导出当前BOSS当前事件
                ExportCurrentBossEvent(args);
                return;
            }

            if (args.Parameters[0].ToLower() == "list")
            {
                // 列出时间事件文件夹中的文件
                ListTimerEvents(args);
                return;
            }

            if (args.Parameters[0].ToLower() == "clear")
            {
                // 清空时间事件文件夹
                ClearTimerEvents(args);
                return;
            }

            if (args.Parameters[0].ToLower() == "reset")
            {
                Commands.HandleCommand(args.Player, "/butcher");
                Config.Dict.Clear();

                var NewNpc = !Config.Dict!.ContainsKey(Lang.GetNPCNameValue(Terraria.ID.NPCID.EyeofCthulhu));
                if (NewNpc)
                {
                    var newData = new Configuration.NpcData()
                    {
                        DeadCount = 0,
                        AutoTarget = true,
                        TrackSpeed = 35,
                        TrackRange = 62,
                        TrackStopRange = 25,
                        ActiveTime = 5f,
                        TextInterval = 1000f,
                        TimerEvent = new List<TimerData>()
                    {
                        new TimerData()
                        {
                            Condition = new List<Conditions>()
                            {
                                new Conditions()
                                {
                                    NpcLift = "0,100"
                                }
                            },

                            SendProj = new List<ProjData>()
                            {
                                new ProjData()
                                {
                                    Type = 115,
                                    Damage = 30,
                                    stack = 15,
                                    interval = 60f,
                                    KnockBack = 5,
                                    Velocity = 10f,
                                }
                            },
                        }
                    },
                    };
                    Config.Dict[Lang.GetNPCNameValue(4)] = newData;
                }

                Config.Write();
                args.Player.SendSuccessMessage($"已清理《怪物加速》的怪物数据(自动击杀当前存在敌对怪物)");
                return;
            }
        }
    }

    #region 列出时间事件文件夹文件指令
    public static void ListTimerEvents(CommandArgs args)
    {
        var directory = Path.Combine("tshock", "怪物加速_时间事件集");

        if (!Directory.Exists(directory))
        {
            args.Player.SendErrorMessage($"时间事件文件夹不存在: {directory}");
            return;
        }

        try
        {
            // 获取所有json文件
            var files = Directory.GetFiles(directory, "*.json")
                .Select(Path.GetFileName)
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                args.Player.SendInfoMessage("时间事件文件夹中没有任何JSON文件。");
                return;
            }

            args.Player.SendSuccessMessage($"时间事件文件夹中的文件 ({files.Count} 个):");

            // 分页显示，每页10个文件
            int pageSize = 10;
            int totalPages = (int)Math.Ceiling(files.Count / (double)pageSize);
            int currentPage = 1;

            // 检查是否有页码参数
            if (args.Parameters.Count > 1 && int.TryParse(args.Parameters[1], out int page) && page > 0 && page <= totalPages)
            {
                currentPage = page;
            }

            int startIndex = (currentPage - 1) * pageSize;
            int endIndex = Math.Min(startIndex + pageSize, files.Count);

            // 直接显示文件名，不添加额外序号
            for (int i = startIndex; i < endIndex; i++)
            {
                args.Player.SendInfoMessage(files[i]);
            }

            if (totalPages > 1)
            {
                args.Player.SendInfoMessage($"第 {currentPage} 页，共 {totalPages} 页。使用 /mos list [页码] 查看其他页。");
            }
        }
        catch (Exception ex)
        {
            args.Player.SendErrorMessage($"列出时间事件文件失败: {ex.Message}");
            TShock.Log.ConsoleError($"列出时间事件文件失败: {ex}");
        }
    }
    #endregion

    #region 导出时间事件指令
    public static void ExportTimerEvents(CommandArgs args)
    {
        var directory = Path.Combine("tshock", "怪物加速_时间事件集");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        int Count = 0;
        // 获取所有事件并排序
        var allEvents = new List<(string npcName, int eventIndex, TimerData data)>();

        foreach (var kvp in Config.Dict!)
        {
            var npcName = kvp.Key;
            var npcData = kvp.Value;

            if (npcData.TimerEvent != null && npcData.TimerEvent.Count > 0)
            {
                for (int i = 0; i < npcData.TimerEvent.Count; i++)
                {
                    allEvents.Add((npcName, i + 1, npcData.TimerEvent[i]));
                }
            }
        }

        // 按BOSS名称和事件索引排序
        allEvents = allEvents.OrderBy(e => e.npcName).ThenBy(e => e.eventIndex).ToList();

        // 重新编号并导出
        for (int i = 0; i < allEvents.Count; i++)
        {
            try
            {
                var (npcName, eventIndex, data) = allEvents[i];
                var sequenceNumber = i + 1;

                // 创建事件文件数据
                var FileData = new EventFileData
                {
                    EventName = $"{npcName}的事件{eventIndex}",
                    TimerEvents = new List<TimerData> { data }
                };

                // 序列化为JSON
                var json = JsonConvert.SerializeObject(FileData, Formatting.Indented);

                // 生成新格式的文件名：序号.boss名称_事件索引.json
                var fileName = $"{sequenceNumber}.{npcName}_事件{eventIndex}.json";
                var filePath = Path.Combine(directory, fileName);

                // 写入文件
                File.WriteAllText(filePath, json, Encoding.UTF8);
                Count++;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"导出事件失败 序号:{i + 1}, 错误: {ex.Message}");
            }
        }

        args.Player.SendSuccessMessage($"成功导出 {Count} 个时间事件到: {directory}");
        args.Player.SendInfoMessage($"文件命名格式: 序号.boss名称_事件索引.json");
    }
    #endregion

    #region 导出当前BOSS当前事件指令
    public static void ExportCurrentBossEvent(CommandArgs args)
    {
        var plr = args.Player;
        var directory = Path.Combine("tshock", "怪物加速_时间事件集");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            // 获取玩家周围的BOSS
            var near = Main.npc
                .Where(npc => npc != null && npc.active && !npc.friendly && npc.boss)
                .Where(npc => Vector2.Distance(plr.TPlayer.position, npc.position) < 85 * 16f) // 50格范围内
                .ToList();

            if (near.Count == 0)
            {
                plr.SendErrorMessage("附近没有找到BOSS！");
                return;
            }

            // 找到最近的BOSS
            var npc = near.OrderBy(npc => Vector2.Distance(plr.TPlayer.position, npc.position)).First();

            // 查找配置中对应的BOSS数据
            var Entry = Config.Dict!.FirstOrDefault(kvp =>
                kvp.Key.Contains(npc.FullName, StringComparison.OrdinalIgnoreCase) ||
                npc.FullName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

            if (Entry.Equals(default(KeyValuePair<string, Configuration.NpcData>)))
            {
                plr.SendErrorMessage($"未找到BOSS '{npc.FullName}' 的配置数据！");
                return;
            }

            var npcName = Entry.Key;
            var npcData = Entry.Value;

            if (npcData.TimerEvent == null || npcData.TimerEvent.Count == 0)
            {
                plr.SendErrorMessage($"BOSS '{npcName}' 没有时间事件配置！");
                return;
            }

            // 获取当前事件索引
            var (Index, _) = TimerEvents.GetIndex_SetTime(npc.FullName);
            if (Index < 0 || Index >= npcData.TimerEvent.Count)
            {
                plr.SendErrorMessage($"BOSS '{npcName}' 的当前事件索引无效！");
                return;
            }

            var Event = npcData.TimerEvent[Index];
            var eventIndex = Index + 1;

            // 查找下一个可用的序号
            var existingFiles = Directory.GetFiles(directory, "*.json");
            var maxSequence = 0;

            foreach (var file in existingFiles)
            {
                var fileName2 = Path.GetFileNameWithoutExtension(file);
                if (int.TryParse(fileName2.Split('.')[0], out var seq) && seq > maxSequence)
                {
                    maxSequence = seq;
                }
            }

            var Number = maxSequence + 1;

            // 创建事件文件数据
            var FileData = new EventFileData
            {
                EventName = $"{npcName}的当前事件",
                TimerEvents = new List<TimerData> { Event }
            };

            // 序列化为JSON
            var json = JsonConvert.SerializeObject(FileData, Formatting.Indented);

            // 生成新格式的文件名：序号.boss名称_事件索引.json
            var fileName = $"{Number}.{npcName}_事件{eventIndex}.json";
            var filePath = Path.Combine(directory, fileName);

            // 写入文件
            System.IO.File.WriteAllText(filePath, json, Encoding.UTF8);

            plr.SendSuccessMessage($"成功导出BOSS '{npcName}' 的当前事件到: {fileName}");
            plr.SendInfoMessage($"事件索引: {eventIndex}/{npcData.TimerEvent.Count}，文件序号: {Number}");
        }
        catch (Exception ex)
        {
            plr.SendErrorMessage($"导出当前BOSS事件失败: {ex.Message}");
            TShock.Log.ConsoleError($"导出当前BOSS事件失败: {ex}");
        }
    }
    #endregion

    #region 清空时间事件文件夹指令
    public static void ClearTimerEvents(CommandArgs args)
    {
        var directory = Path.Combine("tshock", "怪物加速_时间事件集");

        if (!Directory.Exists(directory))
        {
            args.Player.SendErrorMessage($"时间事件文件夹不存在: {directory}");
            return;
        }

        try
        {
            // 获取所有json文件
            var jsonFiles = Directory.GetFiles(directory, "*.json");
            int deletedCount = 0;

            foreach (var file in jsonFiles)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"删除文件失败: {file}, 错误: {ex.Message}");
                }
            }

            if (deletedCount > 0)
            {
                args.Player.SendSuccessMessage($"已清空时间事件文件夹，删除了 {deletedCount} 个文件。");
            }
            else
            {
                args.Player.SendInfoMessage("时间事件文件夹中没有任何JSON文件。");
            }
        }
        catch (Exception ex)
        {
            args.Player.SendErrorMessage($"清空时间事件文件夹失败: {ex.Message}");
            TShock.Log.ConsoleError($"清空时间事件文件夹失败: {ex}");
        }
    }
    #endregion
}