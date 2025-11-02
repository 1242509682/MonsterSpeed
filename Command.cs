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
                "/mos all [-f] —— 导出所有BOSS时间事件(-f覆盖)\n" +
                "/mos now —— 导出当前BOSS当前事件\n" +
                "/mos list —— 列出时间事件文件夹中的文件\n" +
                "/mos clear —— 清空时间事件文件夹\n" +
                "/mos reset —— 清空主配置数据(保留参考)\n" +
                "/reload —— 重载配置文件", 240, 250, 150);
            return;
        }

        if (args.Parameters.Count >= 1 && Config.Dict != null)
        {
            if (args.Parameters[0].ToLower() == "hide")
            {
                Config.HideConfig = !Config.HideConfig;
                Config.Write();
                args.Player.SendSuccessMessage($"已切换多余配置项显示状态为: {(Config.HideConfig ? "隐藏" : "显示")}。");
                return;
            }

            if (args.Parameters[0].ToLower() == "all")
            {
                bool overwrite = args.Parameters.Contains("-f");
                ExportTimerEvents(args, overwrite);
                return;
            }

            if (args.Parameters[0].ToLower() == "now")
            {
                ExportCurrentBossEvent(args);
                return;
            }

            if (args.Parameters[0].ToLower() == "list")
            {
                ListTimerEvents(args);
                return;
            }

            if (args.Parameters[0].ToLower() == "clear")
            {
                ClearTimerEvents(args);
                return;
            }

            if (args.Parameters[0].ToLower() == "reset")
            {
                Commands.HandleCommand(args.Player, "/butcher");

                MyProjectile.ClearAllStates();
                MyProjectile.ClearAllStates();
                TimerEvents.ClearAllStates();

                Config.Dict.Clear();

                var newNpc = !Config.Dict!.ContainsKey(Lang.GetNPCNameValue(Terraria.ID.NPCID.EyeofCthulhu));
                if (newNpc)
                {
                    var nd = NewData();
                    Config.Dict[Lang.GetNPCNameValue(4)] = nd;
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
        var dir = Path.Combine("tshock", "怪物加速_时间事件集");

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
            args.Player.SendErrorMessage($"列出时间事件文件失败: {ex.Message}");
            TShock.Log.ConsoleError($"列出时间事件文件失败: {ex}");
        }
    }
    #endregion

    #region 导出时间事件指令
    public static void ExportTimerEvents(CommandArgs args, bool overwrite = false)
    {
        var dir = Path.Combine("tshock", "怪物加速_时间事件集");
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
        var allEvents = new List<(string name, int idx, TimerData data)>();

        foreach (var kvp in Config.Dict!)
        {
            var name = kvp.Key;
            var data = kvp.Value;

            if (data.TimerEvent != null && data.TimerEvent.Count > 0)
            {
                for (int i = 0; i < data.TimerEvent.Count; i++)
                {
                    allEvents.Add((name, i + 1, data.TimerEvent[i]));
                }
            }
        }

        allEvents = allEvents.OrderBy(e => e.name).ThenBy(e => e.idx).ToList();

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

        for (int i = 0; i < allEvents.Count; i++)
        {
            try
            {
                var (name, idx, data) = allEvents[i];
                var num = startNum + i;

                var fd = new EventFileData
                {
                    EventName = $"{name}的事件{idx}",
                    MoreActiveTime = 0,
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
            args.Player.SendInfoMessage($"模式: 追加导出 (从序号{startNum}开始),使用/mos all -f 可覆盖导出");
        }
    }
    #endregion

    #region 导出当前BOSS当前事件指令
    public static void ExportCurrentBossEvent(CommandArgs args)
    {
        var plr = args.Player;
        var dir = Path.Combine("tshock", "怪物加速_时间事件集");
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

            var entry = Config.Dict!.FirstOrDefault(kvp =>
                kvp.Key.Contains(npc.FullName, StringComparison.OrdinalIgnoreCase) ||
                npc.FullName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

            if (entry.Equals(default(KeyValuePair<string, Configuration.NpcData>)))
            {
                plr.SendErrorMessage($"未找到BOSS '{npc.FullName}' 的配置数据！");
                return;
            }

            var name = entry.Key;
            var data = entry.Value;

            if (data.TimerEvent == null || data.TimerEvent.Count == 0)
            {
                plr.SendErrorMessage($"BOSS '{name}' 没有时间事件配置！");
                return;
            }

            var idx = TimerEvents.GetState(npc)!.Index;
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
                MoreActiveTime = 0,
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
        var dir = Path.Combine("tshock", "怪物加速_时间事件集");

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