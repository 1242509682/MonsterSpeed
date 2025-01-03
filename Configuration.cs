﻿using Newtonsoft.Json;
using TShockAPI;

namespace Plugin;

internal class Configuration
{
    #region 实例变量
    [JsonProperty("插件开关", Order = -1)]
    public bool Enabled { get; set; } = true;
    [JsonProperty("触发监控", Order = 0)]
    public bool Monitor { get; set; } = false;
    [JsonProperty("监控间隔", Order = 0)]
    public double Monitorinterval { get; set; } = 100f;
    [JsonProperty("默认速度", Order = 1)]
    public int Speed { get; set; } = 12;
    [JsonProperty("速度上限", Order = 2)]
    public int MaxSpeed { get; set; } = 35;
    [JsonProperty("触发秒数上限", Order = 3)]
    public double MaxActive { get; set; } = 15;
    [JsonProperty("触发距离上限", Order = 4)]
    public float MaxRange { get; set; } = 84;
    [JsonProperty("触发距离外回血", Order = 5)]
    public bool HealEffect { get; set; } = true;
    [JsonProperty("默认追击距离", Order = 5)]
    public float TrackRange { get; set; } = 50f;
    [JsonProperty("击败后加速度", Order = 5)]
    public int Killed { get; set; } = 2;
    [JsonProperty("击败后减冷却", Order = 6)]
    public double Ratio { get; set; } = 0.5;
    [JsonProperty("怪物ID表", Order = 7)]
    public List<int> NpcList { get; set; }
    [JsonProperty("怪物数据表", Order = 8)]
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

        Dict!["史莱姆王"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, 10f, MaxRange, TrackRange)
        {
            LifeEvent = new List<LifeData>()
            {
                new LifeData
                {
                    MinLife = 50, MaxLife = 100,
                    AIPairs = new Dictionary<int, float>() { { 3, 7200 } }
                },

                new LifeData
                {
                    MinLife = 0, MaxLife = 50, 
                    SpawnMonster = new List<int>{ 184,204 }, NpcStack = 15,
                    AIPairs = new Dictionary<int, float>() {},
                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            ID = 671,
                            Left = 120,
                            Count = 30,
                            Damage = 20,
                            KnockBack = 8,
                            Velocity = 50.0f,
                        },
                    }
                },
            },

        };

        Dict!["克苏鲁之眼"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, 10f, MaxRange, TrackRange)
        {
            TimerEvent = new List<TimerData>()
            {
                new TimerData
                {
                    Order = 1, SpawnMonster = new List<int>{ 5 }, NpcCount = 15,
                    AIPairs = new Dictionary<int, float>() { },
                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            ID = 454,
                            Left = 120,
                            Count = 5,
                            Damage = 20,
                            KnockBack = 8,
                            Velocity = 10f,
                        },
                    }
                },
                new TimerData
                {
                    Order = 2, SpawnMonster = new List<int>{ 133 }, NpcCount = 5,
                    AIPairs = new Dictionary<int, float>() { },
                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            ID = 814,
                            Left = 60,
                            Count = 5,
                            Damage = 30,
                            KnockBack = 8,
                            Velocity = 30f
                        },
                    }
                },
            }

        };
        Dict!["世界吞噬怪"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, 10f, MaxRange, TrackRange * 2.5f) { };
        Dict!["毁灭者"] = new NpcData(true, 0, Speed, MaxSpeed, 5, 5f, MaxActive, 10f, MaxRange, TrackRange * 2f) { };
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
        public float TrackRange { get; set; }
        [JsonProperty("最低加速", Order = 1)]
        public int Speed { get; set; }
        [JsonProperty("最高加速", Order = 2)]
        public int MaxSpeed { get; set; }
        [JsonProperty("触发秒数", Order = 3)]
        public double InActive { get; set; }
        [JsonProperty("触发秒数上限", Order = 4)]
        public double MaxActive { get; set; }
        [JsonProperty("触发最小距离", Order = 5)]
        public float Range { get; set; }
        [JsonProperty("触发最大距离", Order = 6)]
        public float MaxRange { get; set; }
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
            this.Range = range;
            this.MaxRange = maxRange;
            this.TrackRange = trackRange;
        }
    }

    public class LifeData
    {
        [JsonProperty("最小生命", Order = -1)]
        public int MinLife { get; set; }
        [JsonProperty("最大生命", Order = 0)]
        public int MaxLife { get; set; }
        [JsonProperty("AI赋值", Order = 1)]
        public Dictionary<int, float> AIPairs { get; set; } = new Dictionary<int, float>();
        [JsonProperty("召唤怪物", Order = 2)]
        public List<int> SpawnMonster = new List<int>();
        [JsonProperty("召唤数量", Order = 3)]
        public int NpcStack = 5;
        [JsonProperty("生成弹幕", Order = 4)]
        public List<ProjData> SendProj { get; set; } = new List<ProjData>();
    }

    public class TimerData
    {
        [JsonProperty("顺序", Order = 0)]
        public int Order { get; set; }
        [JsonProperty("AI赋值", Order = 1)]
        public Dictionary<int, float> AIPairs { get; set; } = new Dictionary<int, float>();
        [JsonProperty("召唤怪物", Order = 2)]
        public List<int> SpawnMonster = new List<int>();
        [JsonProperty("召唤数量", Order = 3)]
        public int NpcCount = 5;
        [JsonProperty("生成弹幕", Order = 4)]
        public List<ProjData> SendProj { get; set; } = new List<ProjData>();
    }

    public class ProjData
    {
        [JsonProperty("弹幕ID", Order = 0)]
        public int ID = 0;
        [JsonProperty("数量", Order = 1)]
        public int Count = 5;
        [JsonProperty("伤害", Order = 2)]
        public int Damage = 30;
        [JsonProperty("击退", Order = 3)]
        public int KnockBack = 5;
        [JsonProperty("速度", Order = 4)]
        public float Velocity = 60f;
        [JsonProperty("持续帧数", Order = 5)]
        public int Left = -1;
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