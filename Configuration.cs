using Newtonsoft.Json;
using TShockAPI;


namespace MonsterSpeed;

internal class Configuration
{
    #region 实例变量
    [JsonProperty("武器条件说明", Order = -11)]
    public string WeaponType { get; set; } = "无 | 未知 | 近战 | 远程 | 魔法 | 召唤 | 悠悠球 | 投掷物";
    [JsonProperty("进度条件说明", Order = -10)]
    public string[] ProgID { get; set; } = new string[]
    {
        "-1不判断 | 0 无 | 1 克眼 | 2 史王 | 3 世吞克脑 | 4 骷髅王 | 5 蜂王 | 6 巨鹿 | 7 肉后",
        "8 一王后 | 9 双子魔眼 | 10 毁灭者 | 11 铁骷髅王 | 12 世花 | 13 石巨人 | 14 猪鲨",
        "15 拜月 | 16 月总 | 17 光女 | 18 史后 | 19 哀木 | 20 南瓜王 | 21 尖叫怪 | 22 冰雪女皇",
        "23 圣诞坦克 | 24 飞碟 | 25 小丑 | 26 日耀柱 | 27 星旋柱 | 28 星云柱 | 29 星尘柱",
        "30 哥布林 | 31 海盗 | 32 霜月 | 33 血月 | 34 旧日一 | 35 旧日二 | 36 双足翼龙",
        "37 雨天 | 38 白天 | 39 夜晚 | 40 大风天 | 41 万圣节 | 42 派对 | 43 醉酒种子 | 44 十周年",
        "45 ftw种子 | 46 颠倒种子 | 47 蜂蜜种子 | 48 饥荒种子 | 49 天顶种子 | 50 陷阱种子",
        "51 满月 | 52 亏凸月 | 53 下弦月 | 54 残月 | 55 新月 | 56 娥眉月 | 57 上弦月 | 58 盈凸月",
    };

    [JsonProperty("插件开关", Order = -1)]
    public bool Enabled { get; set; } = true;
    [JsonProperty("监控间隔", Order = 0)]
    public double Monitorinterval { get; set; } = 0;
    [JsonProperty("怪物ID表", Order = 1)]
    public List<int> NpcList { get; set; }
    [JsonProperty("怪物数据表", Order = 2)]
    public Dictionary<string, NpcData>? Dict { get; set; } = new Dictionary<string, NpcData>();
    #endregion

    #region 预设参数方法
    public void Ints()
    {
        NpcList = new List<int>()
        {
            4, 13, 35, 50, 113, 115,
            125, 126, 127, 134, 216,
            222, 243, 245, 262, 266,
            325, 327, 344, 345, 346,
            370, 395, 398, 439, 477,
            491, 541, 551, 618, 620,
            621, 636, 657, 668
        };

        Dict!["史莱姆王"] = new NpcData(0, 62f, 25, 35, 5)
        {
            Loop = true,
            Teleport = 10,
            TimerEvent = new List<TimerData>()
            {
                new TimerData
                {
                    Condition = new List < ConditionData >() { new ConditionData() { NpcLift = "50,100" } },
                    SpawnNPC = new List<SpawnNpcData>()
                    {
                        new SpawnNpcData()
                        {
                            NpcStack = 5, Interval = 300, NPCID = new List<int>(){ 184, 204 },
                        }
                    },

                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            Type = 671,
                            Lift = 180,
                            Damage = 10,
                            stack = 10,
                            interval = 8f,
                            KnockBack = 8,
                            Radius = 10f,
                            Angle = 15f,
                            Rotate = 0f,
                            Velocity = 30.0f,
                            UpdateProj = new List<ProjData2>()
                            {
                                new ProjData2()
                                {
                                    Type = 814,
                                    Interval = 1.0f,
                                    Velocity = 1.0f,
                                    Radius = 40f,
                                    Angle = 30f,
                                }
                            }
                        },
                    }
                },

                new TimerData
                {
                    Defense = 50,
                    AutoHealInterval = 5,
                    Condition = new List < ConditionData >() { new ConditionData() { NpcLift = "0,50" } },
                    SpawnNPC = new List<SpawnNpcData>()
                    {
                        new SpawnNpcData()
                        {
                            NpcStack = 2, Interval = 300, NPCID = new List<int>(){ 658, 659, 660 }
                        }
                    },

                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            Type = 351,
                            Lift = 60,
                            Damage = 10,
                            stack = 30,
                            interval = 1f,
                            KnockBack = 8,
                            Radius = 0f,
                            Angle = 360f,
                            Rotate = 2f,
                            Velocity = 20f,
                            ai = new Dictionary<int, float>() { { 0, 50f } },
                        },
                    }
                },
            },

        };

        Dict!["克苏鲁之眼"] = new NpcData(0, 62, 25f, 35, 5)
        {
            TimerEvent = new List<TimerData>()
            {
                new TimerData
                {
                    AutoHealInterval = 5,
                    Condition = new List < ConditionData >() { new ConditionData() { NpcLift = "0,100" } },
                    SpawnNPC = new List<SpawnNpcData>()
                    {
                        new SpawnNpcData()
                        {
                            NpcStack = 5, Interval = 300, NPCID = new List<int>(){ 5 }
                        }
                    },

                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            Type = 115,
                            Damage = 10,
                            stack = 15,
                            interval = 5f,
                            KnockBack = 8,
                            Velocity = 20f,
                            Radius = 15f,
                            Angle = 5f,
                            Rotate = 0f,
                            ai = new Dictionary<int, float>() { { 0, 50f } },
                            Lift = 180,
                            TarCenter = false,
                            UpdateProj = new List<ProjData2>()
                            {
                                new ProjData2()
                                {
                                    Interval = 2,
                                    Velocity = 25,
                                    Angle = 10,
                                }
                            }
                        },
                        new ProjData()
                        {
                            Type = 44,
                            Damage = 10,
                            stack = 15,
                            interval = 5f,
                            KnockBack = 8,
                            Velocity = 10f,
                            Radius = 4f,
                            Angle = 15f,
                            Rotate = 0f,
                            ai = new Dictionary<int, float>(),
                            Lift = 120,
                            TarCenter = false,
                        },

                    }
                },

                new TimerData
                {
                    Timer = 10,
                    Defense = 50,
                    Condition = new List < ConditionData >() { new ConditionData() { NpcLift = "0,100" } },
                    SpawnNPC = new List<SpawnNpcData>()
                    {
                        new SpawnNpcData()
                        {
                            NpcStack = 2, Interval = 300, NPCID = new List<int>(){ 133 }
                        }
                    },

                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            Type = 671,
                            Damage = 10,
                            stack = 15,
                            interval = 10f,
                            KnockBack = 8,
                            Velocity = 100f,
                            Radius = 0f,
                            Angle = 15f,
                            Rotate = 2f,
                            ai = new Dictionary<int, float>(),
                            Lift = 180,
                            TarCenter = false,
                        },
                        new ProjData()
                        {
                            Type = 454,
                            Damage = 20,
                            stack = 15,
                            interval = 10f,
                            KnockBack = 8,
                            Velocity = 105f,
                            Radius = 0f,
                            Angle = 5f,
                            Rotate = 0f,
                            ai = new Dictionary<int, float>() { { 0, 50f } },
                            Lift = 60,
                            TarCenter = false,
                        },
                    }
                },
            }

        };
        Dict!["世界吞噬怪"] = new NpcData(0, 62 * 2.5f, 25f, 35, 5)
        {
            Teleport = 20,
            TimerEvent = new List<TimerData>()
            {
                new TimerData() { Condition = new List < ConditionData >() { new ConditionData() { NpcLift = "0,100" } } }
            }
        };
        Dict!["毁灭者"] = new NpcData(0, 62 * 2f, 25f, 35, 5)
        {
            Teleport = 20,
            TimerEvent = new List<TimerData>()
            {
                new TimerData() { Condition = new List < ConditionData >() { new ConditionData() { NpcLift = "0,100" } } }
            }
        };
    }
    #endregion

    #region 怪物数据结构
    public class NpcData
    {
        [JsonProperty("死亡次数", Order = -3)]
        public int DeadCount { get; set; }
        [JsonProperty("自动仇恨", Order = -2)]
        public bool AutoTarget { get; set; } = true;
        [JsonProperty("追击距离", Order = -1)]
        public float TrackRange { get; set; } = 62f;
        [JsonProperty("停追距离", Order = 0)]
        public float TrackStopRange { get; set; } = 25f;
        [JsonProperty("追击速度", Order = 1)]
        public int TrackSpeed { get; set; }
        [JsonProperty("传送冷却", Order = 1)]
        public int Teleport { get; set; } = -1;

        [JsonProperty("倒计时文字间隔", Order = 20)]
        public double TextInterval { get; set; } = 1000f;
        [JsonProperty("循环执行", Order = 21)]
        public bool Loop { get; set; }
        [JsonProperty("冷却时间", Order = 22)]
        public double CoolTimer { get; set; }
        [JsonProperty("时间事件", Order = 23)]
        public List<TimerData> TimerEvent { get; set; } = new();


        public NpcData() { }
        public NpcData(int deadCount, float trackRange, float trackstopRange, int trackSpeed, double coolTimer)
        {
            this.DeadCount = deadCount;
            this.TrackRange = trackRange;
            this.TrackStopRange = trackstopRange;
            this.TrackSpeed = trackSpeed;
            this.CoolTimer = coolTimer;
        }
    }
    #endregion

    #region 读取与创建配置文件方法
    public static readonly string FilePath = Path.Combine(TShock.SavePath, "怪物加速.json");
    public void Write()
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    public static Configuration Read()
    {
        if (!File.Exists(FilePath))
        {
            var NewConfig = new Configuration();
            NewConfig.Ints();
            new Configuration().Write();
            return NewConfig;
        }
        else
        {
            var jsonContent = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<Configuration>(jsonContent)!;
        }
    }
    #endregion
}