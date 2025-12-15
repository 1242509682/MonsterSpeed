using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TShockAPI;

namespace MonsterSpeed;

public class Configuration
{
    #region 实例变量
    [JsonProperty("插件开关", Order = -100)]
    public bool Enabled { get; set; } = true;
    [JsonProperty("指示物系统说明", Order = -13)]
    public string[] MarkerSystemInfo { get; set; } = new string[0];
    [JsonProperty("播放文件模式说明", Order = -12)]
    public string[] FileMess { get; set; } = new string[0];
    [JsonProperty("武器条件说明", Order = -11)]
    public string WeaponType { get; set; } = "无 | 未知 | 近战 | 远程 | 魔法 | 召唤 | 投掷物";
    [JsonProperty("进度条件说明", Order = -10)]
    public string[] ProgID { get; set; } = new string[0];

    [JsonProperty("隐藏默认配置项", Order = -2)]
    public bool HideConfig { get; set; } = false;
    [JsonProperty("强制隐藏配置项", Order = -1)]
    public List<string> CustomHideList { get; set; } = new List<string>();
    [JsonProperty("怪物ID表", Order = 0)]
    public List<int> NpcList { get; set; }
    [JsonProperty("监控间隔", Order = 1)]
    public double Monitorinterval { get; set; } = 0;
    [JsonProperty("脚本配置", Order = 2)]
    public ScriptConfig ScriptCfg { get; set; } = new();
    [JsonProperty("怪物数据表", Order = 3)]
    public List<NpcData>? NpcDatas { get; set; } = new List<NpcData>();
    #endregion

    #region 怪物数据结构
    public class NpcData
    {
        [JsonProperty("怪物ID", Order = -9)]
        public List<int> Type { get; set; } = new List<int>();
        [JsonProperty("死亡次数", Order = -3)]
        public int DeadCount { get; set; }
        [JsonProperty("自动仇恨", Order = -2)]
        public bool AutoTarget { get; set; } = true;
        [JsonProperty("追击模式", Order = -2)]
        public bool AutoTrack { get; set; } = true;
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
        [JsonProperty("倒时间隔", Order = 23)]
        public double TextInterval { get; set; } = 1000f;
        [JsonProperty("倒时渐变", Order = 23)]
        public bool TextGradient { get; set; } = false;
        [JsonProperty("渐变字距", Order = 24)]
        public int TextRange { get; set; } = 16;
        [JsonProperty("执行文件", Order = 26)]
        public List<FilePlayData> FilePlay { get; set; } = new List<FilePlayData>();
        [JsonProperty("时间事件", Order = 27)]
        public List<TimerData> TimerEvent { get; set; }

        public NpcData() { }
        public NpcData(int deadCount, float trackRange, float trackstopRange, int trackSpeed, double activeTimer)
        {
            this.DeadCount = deadCount;
            this.TrackRange = trackRange;
            this.TrackStopRange = trackstopRange;
            this.TrackSpeed = trackSpeed;
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
    internal static readonly string Paths = Path.Combine(TShock.SavePath, "怪物加速");
    public static readonly string FilePath = Path.Combine(Paths, "怪物加速.json");
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
        ScriptCfg.Usings = new List<string>()
        {
            "using MonsterSpeed;",
            "using static MonsterSpeed.MonsterSpeed;"
        };

        NpcDatas = new List<NpcData>()
        {
            new NpcData(0, 62, 25f, 35, 5)
            {
                Type = new List<int> { 4, 50 },
                AutoHealInterval = 5,
                TimerEvent = new List<TimerData>()
                {
                    new TimerData
                    {
                        CoolTime = 5,
                        Condition = "默认配置",
                        CsScript = "BOSS阶段弹幕脚本",
                        SendProj = new List<string>() { "克眼二更弹幕" },
                    },

                    new TimerData
                    {
                        CoolTime = 10,
                        Defense = 50,
                        Condition = "半血",
                        CsScript = "定时弹幕脚本",
                        ScriptTime = 1,
                        SendProj = new List<string>(){ "默认弹幕" },
                        MoveMode = "顺时针环绕",
                    },

                    new TimerData
                    {
                        Defense = 50,
                        Condition = "史王不在场",
                        SpawnNPC = new List<SpawnNpcData>()
                        {
                            new SpawnNpcData()
                            {
                                NPCID = new List<int>{ 50 },
                                NpcStack = 1
                            }
                        },
                        MarkerList = new List<MstMarkerMod>()
                        {
                            new MstMarkerMod()
                            {
                                Flag = "",
                                MarkerMods = new Dictionary<string, string[]>()
                                {
                                    { "已召唤史王", new string[] { "=1" } }
                                }
                            }
                        }
                    }
                }
            },

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

        MarkerSystemInfo = new string[]
        {
            "【指示物系统】为怪物添加可追踪的状态标记，用于复杂行为控制",
            "【设置位置】时间事件/行动模式/弹幕生成/怪物生成/弹幕更新",
            "【操作表达式】支持：+1、-1、=5、+=2、-=3、*=2、/=2、%=3、random:1,10",
            "【条件表达式】支持：>5、<10、>=3、<=8、==5、!=0、=10",
            "【引用机制】ref:阶段*2 引用其他标记值，支持跨标记计算",
            "【NPC属性】[序号]、[被击]、[击杀]、[耗时]、[x坐标]、[y坐标]、[血量]、[ai0]、[ai1]、[ai2]、[ai3]",
            "【清除操作】clear 清除指定标记",
            "【注入AI】将标记值注入弹幕AI：{0:阶段,1:计数*2}",
            "【速度注入】将速度分量注入AI：{0:2,1:3} X→ai[0], Y→ai[1]",
            "【多条件检查】一个标记可设多个条件，需全部满足才触发",
            "【复合操作】一个标记可执行多个操作，按顺序执行",
            "【循环检测】自动检测循环引用，避免无限递归",
            "【应用场景】阶段转换、技能计数、状态循环、冷却管理、随机行为",
            "【示例修改】{阶段:[+1],计数:[rm:1,5],状态:[=ref:基础值*2]}",
            "【示例条件】{阶段:[>=3,<10],计数:[!=0],状态:[>50]}"
        };

        FileMess = new string[]
        {
            "使用 /mos all 导出时间事件 再用/mos list 列出文件,在[文件播放器]填写文件【序号】",
            "导出的时间事件文件生成路径在: tshock/怪物加速_时间事件集 ",
            "播放类型 1并行播放:主事件运行同时可按次数循环播放文件 2同时播放:主事件运行同时点播所有文件",
            "播放次数 0不播放，>1正序播放一次,<1倒序播放一次",
            "播放时间 默认以怪物数据表本身的【冷却时间】(默认5秒) + 文件里的【冷却延长】构成",
            "强制播放 可忽略【触发条件】与【暂停间隔】进行无约束播放,【循环执行】无法跳过",
            "按次播放 每播放1个文件则算1次播放 3次规律为:1->2->1-> 回到主事件",
            "暂停间隔 无法干涉文件的【强制播放】,单位毫秒,0为不暂停,超过0则触发,会循环暂停释放直到本次时间事件结束",
            "释放间隔 【暂停间隔】为2000毫秒时,1:1默认释放2000毫秒,用于自定义暂停结束后释放时间"
        };

        ProgID = new string[]
        {
            "0 无 | 1 克眼 | 2 史王 | 3 世吞 | 4克脑 | 5世吞或克脑 | 6 巨鹿 | 7 蜂王 | 8 骷髅王前 | 9 骷髅王后",
            "10 肉前 | 11 肉后 | 12 毁灭者 | 13 双子魔眼 | 14 机械骷髅王 | 15 世花 | 16 石巨人 | 17 史后 | 18 光女 | 19 猪鲨",
            "20 拜月 | 21 月总 | 22 哀木 | 23 南瓜王 | 24 尖叫怪 | 25 冰雪女王 | 26 圣诞坦克 | 27 火星飞碟 | 28 小丑",
            "29 日耀柱 | 30 星旋柱 | 31 星云柱 | 32 星尘柱 | 33 一王后 | 34 三王后 | 35 一柱后 | 36 四柱后",
            "37 哥布林 | 38 海盗 | 39 霜月 | 40 血月 | 41 雨天 | 42 白天 | 43 夜晚 | 44 大风天 | 45 万圣节 | 46 圣诞节 | 47 派对",
            "48 旧日一 | 49 旧日二 | 50 旧日三 | 51 醉酒种子 | 52 十周年 | 53 ftw种子 | 54 蜜蜂种子 | 55 饥荒种子",
            "56 颠倒种子 | 57 陷阱种子 | 58 天顶种子",
            "59 森林 | 60 丛林 | 61 沙漠 | 62 雪原 | 63 洞穴 | 64 海洋 | 65 地表 | 66 太空 | 67 地狱 | 68 神圣 | 69 蘑菇",
            "70 腐化 | 71 猩红 | 72 邪恶 | 73 地牢 | 74 墓地 | 75 蜂巢 | 76 神庙 | 77 沙尘暴 | 78 天空",
            "79 满月 | 80 亏凸月 | 81 下弦月 | 82 残月 | 83 新月 | 84 娥眉月 | 85 上弦月 | 86 盈凸月"
        };

        CustomHideList = new List<string>()
        {
            "行动模式说明","步进说明","新弹幕ID说明","武器条件说明","进度条件说明","播放文件模式说明","指示物系统说明"
        };
    }
    #endregion
}