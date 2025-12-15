using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonoMod.InlineRT.MonoModRule;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

public class Command
{
    public static void CMD(CommandArgs args)
    {
        if (args.Parameters.Count == 0)
        {
            Help(args);
            return;
        }

        if (args.Parameters.Count >= 1 && Config.NpcDatas != null)
        {
            var subCmd = args.Parameters[0].ToLower();

            switch (subCmd)
            {
                case "hd":
                case "hide":
                    Config.HideConfig = !Config.HideConfig;
                    Config.Write();
                    args.Player.SendSuccessMessage($"{LogName} 已切换多余配置项显示状态为: {(Config.HideConfig ? "隐藏" : "显示")}。");
                    break;

                case "all":
                    bool overwrite = args.Parameters.Contains("-f");
                    ExportTimerEvents(args, overwrite);
                    break;

                case "now":
                    ExportCurrentBossEvent(args);
                    break;

                case "ls":
                case "list":
                    ListTimerEvents(args);
                    break;

                case "cl":
                case "clear":
                    ClearTimerEvents(args);
                    break;

                case "rs":
                case "reset":
                    Commands.HandleCommand(args.Player, "/butcher");

                    StateApi.ClearAll();

                    Config.NpcDatas.Clear();

                    // 修改：检查是否存在克眼配置，不存在则添加
                    bool hasEyeOfCthulhu = Config.NpcDatas.Any(npcData => npcData.Type.Contains(4));
                    if (!hasEyeOfCthulhu)
                    {
                        var nd = NewData();
                        nd.Type = new List<int>() { 4 }; // 克眼ID
                        Config.NpcDatas.Add(nd);
                    }

                    Config.Write();
                    args.Player.SendSuccessMessage($"{LogName} 已清理怪物数据(自动击杀当前存在敌对怪物)");
                    break;

                default:
                    Help(args);
                    break;
            }
        }
    }

    #region 菜单指令
    private static void Help(CommandArgs args)
    {
        args.Player.SendMessage("《怪物加速》\n" +
            "/mos hd —— 显示与隐藏多余配置项\n" +
            "/mos all [-f] —— 导出所有BOSS时间事件(-f覆盖)\n" +
            "/mos now —— 导出当前BOSS当前事件\n" +
            "/mos ls —— 列出时间事件文件夹中的文件\n" +
            "/mos cl —— 清空时间事件文件夹\n" +
            "/mos rs —— 清空主配置数据(保留参考)\n" +
            "/reload —— 重载配置文件", 240, 250, 150);
    }
    #endregion

    #region 列出时间事件文件夹文件指令
    public static void ListTimerEvents(CommandArgs args)
    {
        var dir = Path.Combine(Paths, "时间事件");

        if (!Directory.Exists(dir))
        {
            args.Player.SendErrorMessage($"时间事件文件夹不存在: {dir}");
            return;
        }

        try
        {
            var files = Directory.GetFiles(dir, "*.json")
                .Select(Path.GetFileName)
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                args.Player.SendInfoMessage("时间事件文件夹中没有任何JSON文件。");
                return;
            }

            args.Player.SendSuccessMessage($"时间事件文件夹中的文件 ({files.Count} 个):");

            int pageSize = 10;
            int totalPages = (int)Math.Ceiling(files.Count / (double)pageSize);
            int curPage = 1;

            if (args.Parameters.Count > 1 && int.TryParse(args.Parameters[1], out int page) && page > 0 && page <= totalPages)
            {
                curPage = page;
            }

            int start = (curPage - 1) * pageSize;
            int end = Math.Min(start + pageSize, files.Count);

            for (int i = start; i < end; i++)
            {
                args.Player.SendInfoMessage(files[i]);
            }

            if (totalPages > 1)
            {
                args.Player.SendInfoMessage($"第 {curPage} 页，共 {totalPages} 页。使用 /mos list [页码] 查看其他页。");
            }
        }
        catch (Exception ex)
        {
            args.Player.SendErrorMessage($"{LogName} 列出时间事件文件失败: {ex.Message}");
            TShock.Log.ConsoleError($"列出时间事件文件失败: {ex}");
        }
    }
    #endregion

    #region 导出时间事件指令
    public static void ExportTimerEvents(CommandArgs args, bool overwrite = false)
    {
        var dir = Path.Combine(Paths, "时间事件");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (overwrite)
        {
            try
            {
                var jsonFiles = Directory.GetFiles(dir, "*.json");
                foreach (var file in jsonFiles)
                {
                    File.Delete(file);
                }
                args.Player.SendInfoMessage("已清空原有事件文件。");
            }
            catch (Exception ex)
            {
                args.Player.SendErrorMessage($"清空原有文件失败: {ex.Message}");
                return;
            }
        }

        int cnt = 0;
        var all = new List<(string name, int idx, TimerData data)>();

        // 修改：遍历List<NpcData>而不是字典
        foreach (var data in Config.NpcDatas!)
        {
            var name = $"怪物{data.Type.FirstOrDefault()}";

            if (data.TimerEvent != null && data.TimerEvent.Count > 0)
            {
                for (int i = 0; i < data.TimerEvent.Count; i++)
                {
                    all.Add((name, i + 1, data.TimerEvent[i]));
                }
            }
        }

        all = all.OrderBy(e => e.name).ThenBy(e => e.idx).ToList();

        // 如果不覆盖，找到最大序号
        int startNum = 1;
        if (!overwrite)
        {
            var existing = Directory.GetFiles(dir, "*.json");
            foreach (var file in existing)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (int.TryParse(fileName.Split('.')[0], out var num) && num >= startNum)
                {
                    startNum = num + 1;
                }
            }
        }

        for (int i = 0; i < all.Count; i++)
        {
            try
            {
                var (name, idx, data) = all[i];
                var num = startNum + i;

                var fd = new EventFileData
                {
                    EventName = $"{name}的事件{idx}",
                    TimerEvents = new List<TimerData> { data }
                };

                var json = JsonConvert.SerializeObject(fd, Formatting.Indented);
                var fileName = $"{num}.{name}_事件{idx}.json";
                var path = Path.Combine(dir, fileName);

                File.WriteAllText(path, json, Encoding.UTF8);
                cnt++;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"导出事件失败 序号:{startNum + i}, 错误: {ex.Message}");
            }
        }

        args.Player.SendSuccessMessage($"成功导出 {cnt} 个时间事件到: {dir}");
        args.Player.SendInfoMessage($"文件命名格式: 序号.boss名称_事件索引.json");
        if (!overwrite)
        {
            args.Player.SendInfoMessage($"模式: 追加导出 (从序号{startNum}开始)\n" +
                                        $"使用/mos all -f 可清空后导出");
        }
    }
    #endregion

    #region 导出当前BOSS当前事件指令
    public static void ExportCurrentBossEvent(CommandArgs args)
    {
        var plr = args.Player;
        var dir = Path.Combine(Paths, "时间事件");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        try
        {
            var near = Main.npc
                .Where(npc => npc != null && npc.active && !npc.friendly && npc.boss)
                .Where(npc => Vector2.Distance(plr.TPlayer.position, npc.position) < 85 * 16f)
                .ToList();

            if (near.Count == 0)
            {
                plr.SendErrorMessage("附近没有找到BOSS！");
                return;
            }

            var npc = near.OrderBy(n => Vector2.Distance(plr.TPlayer.position, n.position)).First();

            // 修改：通过怪物ID查找对应的NpcData
            var data = Config.NpcDatas!.FirstOrDefault(nd => nd.Type.Contains(npc.type));

            if (data == null)
            {
                plr.SendErrorMessage($"未找到BOSS '{npc.FullName}' 的配置数据！");
                return;
            }

            var name = $"怪物{data.Type.FirstOrDefault()}";

            if (data.TimerEvent == null || data.TimerEvent.Count == 0)
            {
                plr.SendErrorMessage($"BOSS '{name}' 没有时间事件配置！");
                return;
            }

            var idx = StateApi.GetState(npc)!.EventIndex;
            if (idx < 0 || idx >= data.TimerEvent.Count)
            {
                plr.SendErrorMessage($"BOSS '{name}' 的当前事件索引无效！");
                return;
            }

            var evt = data.TimerEvent[idx];
            var evtIdx = idx + 1;

            var existing = Directory.GetFiles(dir, "*.json");
            var maxNum = 0;

            foreach (var file in existing)
            {
                var fileName2 = Path.GetFileNameWithoutExtension(file);
                if (int.TryParse(fileName2.Split('.')[0], out var num2) && num2 > maxNum)
                {
                    maxNum = num2;
                }
            }

            var num = maxNum + 1;

            var fd = new EventFileData
            {
                EventName = $"{name}的当前事件",
                TimerEvents = new List<TimerData> { evt }
            };

            var json = JsonConvert.SerializeObject(fd, Formatting.Indented);
            var fileName = $"{num}.{name}_事件{evtIdx}.json";
            var path = Path.Combine(dir, fileName);

            File.WriteAllText(path, json, Encoding.UTF8);

            plr.SendSuccessMessage($"成功导出BOSS '{name}' 的当前事件到: {fileName}");
            plr.SendInfoMessage($"事件索引: {evtIdx}/{data.TimerEvent.Count}，文件序号: {num}");
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
        var dir = Path.Combine(Paths, "时间事件");

        if (!Directory.Exists(dir))
        {
            args.Player.SendErrorMessage($"时间事件文件夹不存在: {dir}");
            return;
        }

        try
        {
            var jsonFiles = Directory.GetFiles(dir, "*.json");
            int delCnt = 0;

            foreach (var file in jsonFiles)
            {
                try
                {
                    File.Delete(file);
                    delCnt++;
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"删除文件失败: {file}, 错误: {ex.Message}");
                }
            }

            if (delCnt > 0)
            {
                args.Player.SendSuccessMessage($"已清空时间事件文件夹，删除了 {delCnt} 个文件。");
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