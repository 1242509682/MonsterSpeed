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

    [JsonProperty("指示物系统说明", Order = -13)]
    public string[] MarkerSystemInfo { get; set; } = new string[]
    {
    "【指示物系统】为怪物添加可追踪的状态标记，用于复杂行为控制",
    "【设置指示物】在【指示物修改】中配置，格式：{标记名:[操作表达式]}",
    "【检查指示物】在【指示物条件】中配置，格式：{标记名:[条件表达式]}",
    "【操作表达式】支持：+1、-1、=5、+=2、-=3、*=2、/=2、%=3",
    "【条件表达式】支持：>5、<10、>=3、<=8、==5、!=0、=10",
    "【随机范围】random:1,10 或 rm:1,10 生成1-10的随机数",
    "【引用其他标记】ref:阶段*2 或 use:计数*1.5 引用其他标记值",
    "【清除标记】clear 清除指定标记",
    "【复合操作】一个标记可执行多个操作，按顺序执行",
    "【多条件检查】一个标记可设置多个条件，需全部满足",
    "【示例修改】{阶段:[+1],计数:[rm:1,5,+=2],状态:[=ref:基础值*2]}",
    "【示例条件】{阶段:[>=3,<10],计数:[!=0],状态:[>50]}",
    "【符号说明】+加 -减 *乘 /除 %余 =赋值 >大于 <小于 >=大于等于 <=小于等于 ==等于 !=不等于",
    "【前缀说明】random:随机范围 ref:引用标记 clear:清除标记",
    "【时间事件指示物修改】在时间事件中设置：{阶段:[+1],攻击计数:[+1]}",
    "【移动模式指示物修改】行动模式中设置：{移动阶段:[=1],冷却:[=60]}",
    "【弹幕生成指示物修改】生成弹幕时设置：{弹幕波次:[+1],威力:[*=1.1]}",
    "【怪物生成指示物修改】召唤随从时设置：{随从数量:[+1],召唤冷却:[=120]}",
    "【AI赋值指示物修改】AI变化时设置：{AI状态:[=ref:血量阶段],行为模式:[+1]}",
    "【指示物注入AI】将标记值注入弹幕AI：{0:阶段,1:计数*2} 注入到ai[0]和ai[1]",
    "【速度注入AI】将速度分量注入AI：{0:2,1:3} 将速度X注入ai[0]，速度Y注入ai[1]",
    "【弹幕更新指示物修改】弹幕升级时：{升级次数:[+1],追踪强度:[+=0.2]}",
    "【范围内怪物指示物】修改范围内怪物：{协同:[=1],目标:[=ref:主目标]}",
    "【条件联动示例】血量50%触发：{狂暴:[=1],速度:[*=1.5],防御:[*=0.8]}",
    "【阶段转换示例】多阶段BOSS：{阶段:[+1],技能冷却:[=ref:阶段*30]}",
    "【计数控制示例】限制技能次数：{使用次数:[+1],剩余次数:[=10-ref:使用次数]}",
    "【状态循环示例】循环状态：{当前状态:[=ref:当前状态%4+1],状态时间:[=0]}",
    "【BOSS标志联动】同标志怪物：{团队阶段:[=ref:首领阶段],同步攻击:[=1]}",
    "【环境适应示例】根据环境：{地形适应:[=ref:当前地形],行为调整:[*=1.2]}",
    "【复合条件示例】多重条件：{阶段:[>=2],计数:[<5],状态:[!=3]}",
    "【随机行为示例】随机选择：{行为模式:[rm:1,4],持续时间:[=ref:行为模式*60]}",
    "【引用计算示例】复杂计算：{最终伤害:[=ref:基础伤害*ref:阶段+ref:加成]}",
    "【冷却管理示例】技能冷却：{冷却时间:[=30],当前冷却:[-=1]}",
    "【连锁反应示例】连锁触发：{触发计数:[+1],连锁阶段:[=ref:触发计数%3]}"
    };

    [JsonProperty("播放文件模式说明", Order = -12)]
    public string[] FileMess { get; set; } = new string[]
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
    [JsonProperty("武器条件说明", Order = -11)]
    public string WeaponType { get; set; } = "无 | 未知 | 近战 | 远程 | 魔法 | 召唤 | 投掷物";
    [JsonProperty("进度条件说明", Order = -10)]
    public string[] ProgID { get; set; } = new string[]
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
        [JsonProperty("标志", Order = -10)]
        public string Flag { get; set; } = "";
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
            "行动模式说明","步进说明","新弹幕ID说明","武器条件说明","进度条件说明","播放文件模式说明","指示物系统说明"
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
            Loop = true,
            TimerEvent = new List<TimerData>()
            {
                new TimerData
                {
                    Condition = new Conditions () { NpcLift = "0,100" },
                    MoveData = new MoveModeData(),
                    SendProj = new List<SpawnProjData>()
                    {
                        new SpawnProjData()
                        {
                            Type = 115,
                            Damage = 10,
                            Stack = 15,
                            Interval = 5f,
                            KnockBack = 8,
                            Velocity = 5f,
                            Radius = 0f,
                            Angle = 0f,
                            Rotate = 0f,
                            AI = new Dictionary<int, float>() { { 0, 50f } },
                            Life = 180,
                            UpdateTime = 500,
                            UpdateProj = new List<UpdateProjData>
                            {
                                new UpdateProjData()
                                {
                                    NewType = 77,
                                    Velocity = 10,
                                    Condition = new Conditions()
                                    {
                                        Timer = "1,2"
                                    }
                                },
                                new UpdateProjData()
                                {
                                    NewType = 454,
                                    Velocity = 20,
                                    Condition = new Conditions()
                                    {
                                        Timer = "2,4"
                                    }
                                },
                            },
                        },
                    },
                },

                new TimerData
                {
                    NextAddTimer = 0,
                    Defense = 50,
                    Condition = new Conditions()
                    {
                        NpcLift = "0,50",
                    },

                    SendProj = new List<SpawnProjData>()
                    {
                        new SpawnProjData()
                        {
                            Type = 671,
                            Damage = 10,
                            Stack = 5,
                            Interval = 10f,
                            KnockBack = 8,
                            Velocity = 100f,
                            Radius = 0f,
                            Angle = 15f,
                            Rotate = 2f,
                            AI = new Dictionary<int, float>(),
                            Life = 180,
                        }
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

                    SendProj = new List<SpawnProjData>()
                    {
                        new SpawnProjData()
                        {
                            Type = 671,
                            Life = 180,
                            Damage = 10,
                            Stack = 10,
                            Interval = 8f,
                            KnockBack = 8,
                            Radius = 10f,
                            Angle = 15f,
                            Rotate = 0f,
                            Velocity = 30.0f,
                        },
                    },
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

                    SendProj = new List<SpawnProjData>()
                    {
                        new SpawnProjData()
                        {
                            Type = 351,
                            Life = 60,
                            Damage = 10,
                            Stack = 30,
                            Interval = 1f,
                            KnockBack = 8,
                            Radius = 0f,
                            Angle = 360f,
                            Rotate = 2f,
                            Velocity = 20f,
                            AI = new Dictionary<int, float>() { { 0, 50f } },
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