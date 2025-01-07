using MonsterSpeed.Progress;
using Newtonsoft.Json;
using TShockAPI;


namespace MonsterSpeed;

internal class Configuration
{
    #region 实例变量
    [JsonProperty("插件开关", Order = -1)]
    public bool Enabled { get; set; } = true;
    [JsonProperty("触发监控", Order = 0)]
    public bool Monitor { get; set; } = false;
    [JsonProperty("监控间隔", Order = 1)]
    public double Monitorinterval { get; set; } = 100f;
    [JsonProperty("默认速度", Order = 2)]
    public int Speed { get; set; } = 12;
    [JsonProperty("速度上限", Order = 3)]
    public int MaxSpeed { get; set; } = 35;
    [JsonProperty("触发秒数上限", Order = 4)]
    public double MaxActive { get; set; } = 15;
    [JsonProperty("默认最小触发距离", Order = 5)]
    public float MinRange { get; set; } = 25f;
    [JsonProperty("默认最大触发距离", Order = 6)]
    public float MaxRange { get; set; } = 84;
    [JsonProperty("触发距离外回血", Order = 7)]
    public bool HealEffect { get; set; } = true;
    [JsonProperty("距离外每帧回血", Order = 8)]
    public int HealCount { get; set; } = 1;
    [JsonProperty("默认追击距离", Order = 9)]
    public float TrackRange { get; set; } = 62f;
    [JsonProperty("击败后加速度", Order = 10)]
    public int Killed { get; set; } = 2;
    [JsonProperty("击败后减冷却", Order = 11)]
    public double Ratio { get; set; } = 0.5;
    [JsonProperty("怪物ID表", Order = 12)]
    public List<int> NpcList { get; set; }
    [JsonProperty("怪物数据表", Order = 13)]
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

        Dict!["史莱姆王"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, MinRange, MaxRange, TrackRange)
        {
            LifeEvent = new List<LifeData>()
            {
                new LifeData
                {
                    MinLife = 75, MaxLife = 100,
                    SpawnNPC = new List<SpawnNpcData>()
                    {
                        new SpawnNpcData()
                        {
                            NpcStack = 5, Interval = 300, NPCID = new List<int>(){ 184, 204 },
                        }
                    },
                },

                new LifeData
                {
                    MinLife = 0, MaxLife = 75, 
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
                            ProjID = 671,
                            Lift = 120,
                            Damage = 10,
                            stack = 10,
                            interval = 5f,
                            KnockBack = 8,
                            Radius = 10f,
                            Offset = 15f,
                            Rotate = 0f,
                            Velocity = 50.0f,
                        },
                        new ProjData()
                        {
                            ProjID = 351,
                            Lift = 60,
                            Damage = 10,
                            stack = 30,
                            interval = 1f,
                            KnockBack = 8,
                            Radius = 0f,
                            Offset = 360f,
                            Rotate = 2f,
                            Velocity = 20f,
                            ai = new Dictionary<int, float>() { { 0, 50f } },
                        },
                    }
                },
            },

        };

        Dict!["克苏鲁之眼"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, MinRange, MaxRange, TrackRange)
        {
            TimerEvent = new List<TimerData>()
            {
                new TimerData
                {
                    Order = 1,
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
                            ProjID = 115,
                            Damage = 10,
                            stack = 15,
                            interval = 2f,
                            KnockBack = 8,
                            Velocity = 25f,
                            Radius = 15f,
                            Offset = 5f,
                            Rotate = 0f,
                            ai = new Dictionary<int, float>() { { 0, 50f } },
                            Lift = 180,
                            TarCenter = false,
                        },
                        new ProjData()
                        {
                            ProjID = 44,
                            Damage = 10,
                            stack = 15,
                            interval = 5f,
                            KnockBack = 8,
                            Velocity = 10f,
                            Radius = 4f,
                            Offset = 15f,
                            Rotate = 0f,
                            ai = new Dictionary<int, float>(),
                            Lift = 120,
                            TarCenter = false,
                        },
                        
                    }
                },
                new TimerData
                {
                    Order = 2,
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
                            ProjID = 814,
                            Damage = 10,
                            stack = 15,
                            interval = 10f,
                            KnockBack = 8,
                            Velocity = 100f,
                            Radius = 0f,
                            Offset = 15f,
                            Rotate = 2f,
                            ai = new Dictionary<int, float>(),
                            Lift = 180,
                            TarCenter = false,
                        },
                        new ProjData()
                        {
                            ProjID = 454,
                            Damage = 20,
                            stack = 15,
                            interval = 10f,
                            KnockBack = 8,
                            Velocity = 105f,
                            Radius = 0f,
                            Offset = 5f,
                            Rotate = 0f,
                            ai = new Dictionary<int, float>() { { 0, 50f } },
                            Lift = 60,
                            TarCenter = false,
                        },
                    }
                },
            }

        };
        Dict!["世界吞噬怪"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, MinRange, MaxRange, TrackRange * 2.5f) { };
        Dict!["毁灭者"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, MinRange, MaxRange, TrackRange * 2f) { };
    }
    #endregion

    #region 怪物数据结构
    public class NpcData
    {
        [JsonProperty("冷却时间", Order = -3)]
        public double CoolTimer { get; set; }
        [JsonProperty("自动仇恨", Order = -2)]
        public bool AutoTarget { get; set; }
        [JsonProperty("追击模式", Order = -1)]
        public bool Track { get; set; }
        [JsonProperty("追击距离", Order = 0)]
        public float TrackRange { get; set; } = 62f;
        [JsonProperty("最低加速", Order = 1)]
        public int Speed { get; set; }
        [JsonProperty("最高加速", Order = 2)]
        public int MaxSpeed { get; set; }
        [JsonProperty("触发秒数", Order = 3)]
        public double InActive { get; set; }
        [JsonProperty("触发秒数上限", Order = 4)]
        public double MaxActive { get; set; }
        [JsonProperty("触发最小距离", Order = 5)]
        public float MinRange { get; set; } = 25f;
        [JsonProperty("触发最大距离", Order = 6)]
        public float MaxRange { get; set; } = 84f;
        [JsonProperty("血量事件", Order = 7)]
        public List<LifeData> LifeEvent { get; set; } = new();
        [JsonProperty("时间事件", Order = 8)]
        public List<TimerData> TimerEvent { get; set; } = new();
        [JsonProperty("死亡次数", Order = 9)]
        public int DeadCount { get; set; }

        public NpcData() { }
        public NpcData(bool enabled, int count, int speed, int maxSpeed, double coolTimer, double inActive, double maxActive, float range, float maxRange, float trackRange)
        {
            this.Track = enabled;
            this.DeadCount = count;
            this.Speed = speed;
            this.MaxSpeed = maxSpeed;
            this.CoolTimer = coolTimer;
            this.InActive = inActive;
            this.MaxActive = maxActive;
            this.MinRange = range;
            this.MaxRange = maxRange;
            this.TrackRange = trackRange;
        }
    }

    //血量事件数据结构
    public class LifeData 
    {
        [JsonProperty("最小生命", Order = -1)]
        public int MinLife { get; set; }
        [JsonProperty("最大生命", Order = 0)]
        public int MaxLife { get; set; }
        [JsonProperty("怪物AI", Order = 1)]
        public Dictionary<int, float> AIPairs { get; set; } = new Dictionary<int, float>();
        [JsonProperty("生成怪物", Order = 2)]
        public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
        [JsonProperty("生成弹幕", Order = 3)]
        public List<ProjData> SendProj { get; set; } = new List<ProjData>();
    }

    //时间事件数据结构
    public class TimerData
    {
        [JsonProperty("顺序", Order = 0)]
        public int Order { get; set; }
        [JsonProperty("怪物AI", Order = 1)]
        public Dictionary<int, float> AIPairs { get; set; } = new Dictionary<int, float>();
        [JsonProperty("生成怪物", Order = 2)]
        public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
        [JsonProperty("生成弹幕", Order = 3)]
        public List<ProjData> SendProj { get; set; } = new List<ProjData>();
    }

    //随从怪物结构
    public class SpawnNpcData
    {
        [JsonProperty("怪物ID", Order = 0)]
        public List<int> NPCID = new List<int>();
        [JsonProperty("范围", Order = 1)]
        public int Range = 25;
        [JsonProperty("数量", Order = 2)]
        public int NpcStack = 5;
        [JsonProperty("间隔", Order = 3)]
        public float Interval = 300;
        [JsonProperty("进度限制", Order = 4)]
        public ProgressType isProgress { get; set; } = ProgressType.None;
        [JsonProperty("以玩家为中心", Order = 5)]
        public bool TarCenter = false;
    }

    //弹幕数据结构
    public class ProjData
    {
        [JsonProperty("弹幕ID", Order = 0)]
        public int ProjID = 0;
        [JsonProperty("伤害", Order = 1)]
        public int Damage = 30;
        [JsonProperty("数量", Order = 2)]
        public int stack = 5;
        [JsonProperty("间隔", Order = 3)]
        public float interval = 15f;
        [JsonProperty("击退", Order = 4)]
        public int KnockBack = 5;
        [JsonProperty("速度", Order = 5)]
        public float Velocity = 10f;
        [JsonProperty("半径", Order = 7)]
        public float Radius = 0f;
        [JsonProperty("偏移", Order = 8)]
        public float Offset = 15f;
        [JsonProperty("旋转", Order = 9)]
        public float Rotate = 5f;
        [JsonProperty("弹幕AI", Order = 10)]
        public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
        [JsonProperty("生命", Order = 11)]
        public int Lift = 120;
        [JsonProperty("进度限制", Order = 12)]
        public ProgressType isProgress { get; set; } = ProgressType.None;
        [JsonProperty("以玩家为中心", Order = 13)]
        public bool TarCenter = false;
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