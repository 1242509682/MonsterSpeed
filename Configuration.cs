using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TShockAPI;

namespace MonsterSpeed;

internal class Configuration
{
    #region 实例变量
    [JsonProperty("插件开关", Order = -100)]
    public bool Enabled { get; set; } = true;

    [JsonProperty("播放文件模式说明", Order = -12)]
    public string[] FileMess { get; set; } = new string[]
{
        "使用 /mos all 导出时间事件 再用/mos list 列出文件,在[文件播放器]填写文件【序号】",
        "导出的时间事件文件生成路径在: tshock/怪物加速_时间事件集 ",
        "播放次数 0不播放，>1正序播放一次,<1倒序播放一次",
        "播放时间 默认以怪物数据表本身的【冷却时间】(默认5秒) + 文件里的【冷却延长】构成",
        "强制播放 可忽略【触发条件】与【暂停间隔】进行无约束播放,【循环执行】无法跳过",
        "按次播放 每播放1个文件则算1次播放 3次规律为:1->2->1-> 回到主事件",
        "暂停间隔 无法干涉文件的【强制播放】,单位毫秒,0为不暂停,超过0则触发,会循环暂停释放直到本次时间事件结束",
        "释放间隔 【暂停间隔】为2000毫秒时,1:1默认释放2000毫秒,用于自定义暂停结束后释放时间"
};
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

    [JsonProperty(PropertyName = "隐藏默认配置项", Order = -2)]
    public bool HideConfig { get; set; } = false;
    [JsonProperty(PropertyName = "强制隐藏配置项", Order = -1)]
    public List<string> CustomHideList { get; set; } = new List<string>();
    [JsonProperty("监控间隔", Order = 0)]
    public double Monitorinterval { get; set; } = 0;
    [JsonProperty("怪物ID表", Order = 1)]
    public List<int> NpcList { get; set; }
    [JsonProperty("怪物数据表", Order = 2)]
    public Dictionary<string, NpcData>? Dict { get; set; } = new Dictionary<string, NpcData>();
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

        [JsonProperty("回血间隔", Order = 18)]
        public int AutoHealInterval { get; set; } = 10;
        [JsonProperty("百分比回血", Order = 19)]
        public int AutoHeal { get; set; } = 1;

        [JsonProperty("循环执行", Order = 22)]
        public bool Loop { get; set; }
        [JsonProperty("倒时间隔", Order = 23)]
        public double TextInterval { get; set; } = 1000f;
        [JsonProperty("倒时渐变", Order = 23)]
        public bool TextGradient { get; set; } = true;
        [JsonProperty("渐变字距", Order = 24)]
        public int TextRange { get; set; } = 16;
        [JsonProperty("冷却时间", Order = 25)]
        public double ActiveTime { get; set; }
        [JsonProperty("时间事件", Order = 26)]
        public List<TimerData> TimerEvent { get; set; }

        public NpcData() { }
        public NpcData(int deadCount, float trackRange, float trackstopRange, int trackSpeed, double activeTimer)
        {
            this.DeadCount = deadCount;
            this.TrackRange = trackRange;
            this.TrackStopRange = trackstopRange;
            this.TrackSpeed = trackSpeed;
            this.ActiveTime = activeTimer;
        }
    }
    #endregion

    #region 隐藏默认值的ContractResolver
    public class ContractResolver : DefaultContractResolver
    {
        private readonly bool hide;
        public ContractResolver(bool hide) => this.hide = hide;

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty py = base.CreateProperty(member, memberSerialization);

            // 获取属性的JsonProperty特性中定义的名称
            var Attr = member.GetCustomAttribute<JsonPropertyAttribute>();
            string NameInJson = Attr?.PropertyName ?? member.Name;

            // 处理自定义强制隐藏 - 使用JSON中的属性名进行比较
            var custom = MonsterSpeed.Config.CustomHideList;
            if (hide && custom.Contains(NameInJson))
            {
                py.ShouldSerialize = instance => false;
                return py; // 直接返回，不执行后续逻辑
            }

            // 处理属性默认值隐藏
            if (member is PropertyInfo Info)
            {
                py.ShouldSerialize = instance =>
                {
                    // 如果不启用隐藏默认值，总是序列化
                    if (!MonsterSpeed.Config.HideConfig)
                        return true;

                    var value = Info.GetValue(instance);

                    // 安全地获取默认值
                    object deValue = GetDefaultValue(Info.PropertyType);

                    // 只有当值不等于默认值时才序列化
                    return !Equals(value, deValue);
                };
            }

            return py;
        }

        // 安全地获取各种类型的默认值
        private object GetDefaultValue(Type type)
        {
            if (type == typeof(string))
                return null; // 字符串的默认值是 null
            else if (type == typeof(string[]))
                return null; // 字符串数组的默认值是 null
            else if (type.IsValueType)
                return Activator.CreateInstance(type); // 值类型使用无参构造函数
            else
                return null; // 其他引用类型的默认值是 null
        }
    }
    #endregion

    #region 读取与创建配置文件方法
    public static readonly string FilePath = Path.Combine(TShock.SavePath, "怪物加速.json");
    public void Write()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = HideConfig ? NullValueHandling.Ignore : NullValueHandling.Include,
            DefaultValueHandling = HideConfig ? DefaultValueHandling.Ignore : DefaultValueHandling.Include,
            ContractResolver = new ContractResolver(HideConfig),
        };

        var json = JsonConvert.SerializeObject(this, settings);
        File.WriteAllText(FilePath, json, new System.Text.UTF8Encoding(false));
    }

    public static Configuration Read()
    {
        if (!File.Exists(FilePath))
        {
            var NewConfig = new Configuration();
            NewConfig.SetDefault();
            NewConfig.Write();
            return NewConfig;
        }
        else
        {
            var jsonContent = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<Configuration>(jsonContent)!;
        }
    }
    #endregion

    #region 预设参数方法
    public void SetDefault()
    {
        CustomHideList = new List<string>()
        {
            "武器条件说明","进度条件说明","播放文件模式说明"
        };

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

        Dict!["克苏鲁之眼"] = new NpcData(0, 62, 25f, 35, 5)
        {
            AutoHealInterval = 5,
            TimerEvent = new List<TimerData>()
            {
                new TimerData
                {
                    Condition = new Conditions () { NpcLift = "0,100" },
                    SendProj = new List<ProjData>()
                    {
                        new ProjData()
                        {
                            Type = 115,
                            Damage = 10,
                            stack = 15,
                            interval = 5f,
                            KnockBack = 8,
                            Velocity = 5f,
                            Radius = 15f,
                            Angle = 5f,
                            Rotate = 0f,
                            ai = new Dictionary<int, float>() { { 0, 50f } },
                            Lift = 180,
                            TarCenter = false,
                            UpdateTime = 500f,
                            UpdateProj = new List<UpdateProjData>()
                            {
                                new UpdateProjData()
                                {
                                    Backer = true,
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
                    NextAddTimer = 0,
                    Defense = 50,
                    Condition = new Conditions()
                    {
                            NpcLift = "0,50",
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

                new TimerData
                {
                    NextAddTimer = 0,
                    Defense = 50,
                    Condition = new Conditions()
                    {
                        NpcLift = "0,25",
                        AIPairs = new Dictionary<int, string[]>()
                        {
                            { 0,new string[]{ "!=5", ">=3", "<20" }},
                            { 1,new string[]{ ">2", "<100" }},
                            { 2,new string[]{ ">=0" }},
                            { 3,new string[]{ "<=3" }},
                        }
                    },

                    ShootItemList = new HashSet<int>(){ 71,75 },

                    SpawnNPC = new List<SpawnNpcData>()
                    {
                        new SpawnNpcData()
                        {
                            NpcStack = 2, Interval = 300, NPCID = new List<int>(){ 133 }
                        }
                    },
                },
            }
        };

        Dict!["史莱姆王"] = new NpcData(0, 62f, 25, 35, 5)
        {
            Loop = true,
            Teleport = 10,
            AutoHealInterval = 5,
            TimerEvent = new List<TimerData>()
            {
                new TimerData
                {
                    Condition = new Conditions() { NpcLift = "50,100" },
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
                            UpdateTime = 2000f,
                            UpdateProj = new List<UpdateProjData>()
                            {
                                new UpdateProjData()
                                {
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
                    Condition = new Conditions () { NpcLift = "0,50" },
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

        Dict!["世界吞噬怪"] = new NpcData(0, 62 * 2.5f, 35f, 35, 5)
        {
            Teleport = 20,
        };

        Dict!["毁灭者"] = new NpcData(0, 62 * 2f, 35f, 35, 5)
        {
            Teleport = 20,
        };
    }
    #endregion
}