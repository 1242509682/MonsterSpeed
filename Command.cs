using Terraria;
using TShockAPI;
using static MonsterSpeed.MonsterSpeed;

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
                    CoolTimer = 5f,
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
