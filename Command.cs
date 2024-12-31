using TShockAPI;
using static Plugin.MonsterSpeed;
using static Plugin.Configuration;

namespace MonsterSpeed;

internal class Command
{
    public static void CMD(CommandArgs args)
    {
        if(args.Parameters.Count == 0)
        {
            args.Player.SendMessage("《怪物加速》\n" +
                "/reload —— 重载配置文件\n" +
                "/ms reset —— 清空怪物数据(保留参考)",240,250,150);
            return;
        }

        if (args.Parameters.Count == 1 && Config.Dict != null && 
            args.Parameters[0].ToLower() == "reset")
        {
            Config.Dict.Clear();
            Config.Write();
            args.Player.SendSuccessMessage($"已清理《怪物加速》的怪物数据");
            return;
        }
    }
}
