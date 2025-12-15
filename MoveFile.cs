using System.Text;
using Newtonsoft.Json;
using TShockAPI;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

// 行动模式配置文件包装类
public class MoveDatas
{
    [JsonProperty("模式名称")]
    public string Name { get; set; } = "未命名";
    [JsonProperty("模式描述")]
    public string Desc { get; set; } = "";
    [JsonProperty("行动模式")]
    public MoveModeData MoveData { get; set; } = new MoveModeData();
}

// 行动模式文件管理器
public class MoveFile
{
    #region 文件路径
    public static readonly string Dir = Path.Combine(TShock.SavePath, "怪物加速", "行动模式");
    #endregion

    #region 初始化
    public static void Init()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            if (Directory.GetFiles(Dir, "*.json").Length == 0)
            {
                CreateDefault();
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 行动模式系统初始化失败: {ex.Message}");
        }
    }
    #endregion

    #region 创建默认行动模式
    private static void CreateDefault()
    {
        try
        {
            List<MoveDatas> defaults = SetDefault();

            foreach (var config in defaults)
            {
                SaveFile(config);
            }

            TShock.Log.ConsoleInfo($"{LogName} 已创建 {defaults.Count} 个行动模式文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 创建行动模式失败: {ex.Message}");
        }
    }
    #endregion

    #region 创建默认配置
    private static List<MoveDatas> SetDefault()
    {
        var defaults = new List<MoveDatas>
        {
            new MoveDatas
            {
                Name = "原地停留",
                Desc = "怪物停留在原地",
                MoveData = new MoveModeData
                {
                    Mode = MoveMode.Stay,
                    SmoothFactor = 0.15f
                }
            },
            new MoveDatas
            {
                Name = "顺时针环绕",
                Desc = "围绕目标顺时针旋转",
                MoveData = new MoveModeData
                {
                    Mode = MoveMode.Orbit,
                    OrbitDir = OrbitDirection.Clockwise,
                    OrbitRadius = 25f,
                    OrbitSpeed = 2.5f,
                    OrbitMoveSpeed = 15f,
                    SmoothFactor = 0.15f
                }
            },
            new MoveDatas
            {
                Name = "逆时针环绕",
                Desc = "围绕目标逆时针旋转",
                MoveData = new MoveModeData
                {
                    Mode = MoveMode.Orbit,
                    OrbitDir = OrbitDirection.CounterClockwise,
                    OrbitRadius = 25f,
                    OrbitSpeed = 2.5f,
                    OrbitMoveSpeed = 15f,
                    SmoothFactor = 0.15f
                }
            },
            new MoveDatas
            {
                Name = "交替环绕",
                Desc = "定期切换环绕方向",
                MoveData = new MoveModeData
                {
                    Mode = MoveMode.Orbit,
                    OrbitDir = OrbitDirection.Alternate,
                    DirTimer = 180f,
                    OrbitRadius = 25f,
                    OrbitSpeed = 2.5f,
                    OrbitMoveSpeed = 15f,
                    SmoothFactor = 0.15f
                }
            },
            new MoveDatas
            {
                Name = "随机徘徊",
                Desc = "在目标周围随机移动",
                MoveData = new MoveModeData
                {
                    Mode = MoveMode.Wander,
                    WanderRadius = 30f,
                    WanderSpeed = 10f,
                    WanderChangeInterval = 120,
                    WanderCloseDistance = 1f,
                    SmoothFactor = 0.15f
                }
            },
            new MoveDatas
            {
                Name = "快速突进",
                Desc = "向目标快速突进攻击",
                MoveData = new MoveModeData
                {
                    Mode = MoveMode.Dash,
                    DashSpeed = 50f,
                    DashWindup = 30,
                    DashDuration = 20,
                    DashCooldown = 180,
                    DashRetreatDistance = 2f,
                    DashRetreatSpeedFactor = 0.3f,
                    SmoothFactor = 0.15f
                }
            },
            new MoveDatas
            {
                Name = "保持对视",
                Desc = "在目标周围保持对视位置",
                MoveData = new MoveModeData
                {
                    Mode = MoveMode.FaceTarget,
                    FaceDistance = 30f,
                    SwitchDistance = 20f,
                    FaceSpeed = 10f,
                    FaceSmooth = 0.1f,
                    FaceSwitchChance = 4,
                    SmoothFactor = 0.15f
                }
            },
        };
        return defaults;
    }
    #endregion

    #region 获取行动模式配置
    public static MoveDatas GetFile(string name)
    {
        try
        {
            if (!Directory.Exists(Dir))
                return new MoveDatas();

            var filePath = Path.Combine(Dir, $"{name}.json");

            if (!File.Exists(filePath))
            {
                TShock.Log.ConsoleError($"{LogName} 行动模式文件不存在: {name}");
                return new MoveDatas();
            }

            var content = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<MoveDatas>(content) ?? new MoveDatas();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 读取行动模式失败: {name}, 错误: {ex.Message}");
            return new MoveDatas();
        }
    }

    public static MoveModeData GetData(string name)
    {
        var config = GetFile(name);
        if (config == null || config.MoveData == null)
        {
            TShock.Log.ConsoleError($"{LogName} 行动模式 '{name}' 不存在，使用默认停留模式");
            return new MoveModeData { Mode = MoveMode.Stay };
        }
        return config.MoveData;
    }
    #endregion

    #region 保存行动模式配置
    public static void SaveFile(MoveDatas config)
    {
        try
        {
            var file = $"{config.Name}.json";
            var path = Path.Combine(Dir, file);
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 保存行动模式失败: {config.Name}, 错误: {ex.Message}");
        }
    }
    #endregion

    #region 重载所有配置
    public static void Reload()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
                if (Directory.GetFiles(Dir, "*.json").Length == 0)
                    CreateDefault();
            }

            var all = new List<MoveDatas>();
            var jsonFiles = Directory.GetFiles(Dir, "*.json");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var file = JsonConvert.DeserializeObject<MoveDatas>(content);
                    if (file != null)
                        all.Add(file);
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"{LogName} 读取配置文件失败: " +
                        $"{Path.GetFileName(filePath)}, 错误:\n {ex.Message}");
                }
            }

            if (all.Count > 0)
                foreach (var FileData in all)
                    SaveFile(FileData);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 重载条件配置失败: {ex.Message}");
        }
    }
    #endregion
}