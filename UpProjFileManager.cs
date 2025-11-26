using System.Text;
using Newtonsoft.Json;
using TShockAPI;

namespace MonsterSpeed;

// 更新弹幕文件数据结构
public class UpProjFileData
{
    [JsonProperty("更新弹幕名称")]
    public string Name { get; set; } = "未命名更新弹幕";

    [JsonProperty("更新弹幕描述")]
    public string Description { get; set; } = "";

    [JsonProperty("更新弹幕列表")]
    public List<UpdateProjData> UpdateProjectiles { get; set; } = new List<UpdateProjData>();
}

// 更新弹幕文件管理器
internal class UpProjFileManager
{
    #region 文件路径
    public static readonly string UpdateProjectileDir = Path.Combine(TShock.SavePath, "怪物加速", "更新弹幕配置");
    #endregion

    #region 初始化
    public static void Init()
    {
        try
        {
            if (!Directory.Exists(UpdateProjectileDir))
            {
                Directory.CreateDirectory(UpdateProjectileDir);
            }

            if (Directory.GetFiles(UpdateProjectileDir, "*.json").Length == 0)
            {
                CreateDefault();
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"更新弹幕系统初始化失败: {ex.Message}");
        }
    }
    #endregion

    #region 创建默认更新弹幕配置
    private static void CreateDefault()
    {
        try
        {
            var defaults = new List<UpProjFileData>
            {
                new UpProjFileData
                {
                    Name = "默认更新弹幕",
                    Description = "基础更新弹幕配置",
                    UpdateProjectiles = new List<UpdateProjData>
                    {
                        new UpdateProjData
                        {
                            NewType = 0,
                            ExtraTime = 0,
                            Velocity = 0f
                        }
                    }
                },

                new UpProjFileData
                {
                    Name = "追踪弹幕",
                    Description = "自动追踪玩家的弹幕",
                    UpdateProjectiles = new List<UpdateProjData>
                    {
                        new UpdateProjData
                        {
                            NewType = 0,
                            ExtraTime = 60,
                            Homing = true,
                            TarType = 1,
                            HomingStrength = 0.1f,
                            MaxHomingAngle = 30f
                        }
                    }
                },

                new UpProjFileData
                {
                    Name = "二次更新",
                    Description = "二次更新弹幕",
                    UpdateProjectiles = new List<UpdateProjData>
                    {
                        new UpdateProjData()
                        {
                            NewType = 77,
                            Velocity = 10,
                            Condition = "默认配置"
                        },
                        new UpdateProjData()
                        {
                            NewType = 454,
                            Velocity = 20,
                            Condition = "默认配置"
                        },
                    }
                }

            };

            foreach (var updateProj in defaults)
            {
                Save(updateProj);
            }

            TShock.Log.ConsoleInfo($"已创建 {defaults.Count} 个更新弹幕配置文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"创建更新弹幕配置失败: {ex.Message}");
        }
    }
    #endregion

    #region 获取更新弹幕配置 - 直接读取文件
    public static UpProjFileData GetFile(string name)
    {
        try
        {
            if (!Directory.Exists(UpdateProjectileDir))
                return new UpProjFileData();

            var filePath = Path.Combine(UpdateProjectileDir, $"{name}.json");

            if (!File.Exists(filePath))
                return new UpProjFileData();

            var content = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<UpProjFileData>(content)!;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"读取更新弹幕配置失败: {name}, 错误: {ex.Message}");
            return new UpProjFileData();
        }
    }

    public static List<UpdateProjData> GetData(string name)
    {
        var file = GetFile(name);
        if (file == null || file.UpdateProjectiles == null || file.UpdateProjectiles.Count == 0)
        {
            TShock.Log.ConsoleError($"更新弹幕配置 '{name}' 不存在或为空，使用默认更新弹幕");
            return new List<UpdateProjData> { new UpdateProjData() };
        }
        return file.UpdateProjectiles;
    }
    #endregion

    #region 保存更新弹幕配置
    public static void Save(UpProjFileData update)
    {
        try
        {
            var file = $"{update.Name}.json";
            var path = Path.Combine(UpdateProjectileDir, file);
            var json = JsonConvert.SerializeObject(update, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"保存更新弹幕配置失败: {update.Name}, 错误: {ex.Message}");
        }
    }
    #endregion
}