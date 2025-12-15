using System.Text;
using Newtonsoft.Json;
using TShockAPI;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

// 更新弹幕文件数据结构
public class UpData
{
    [JsonProperty("更新弹幕名称")]
    public string Name { get; set; } = "未命名更新弹幕";

    [JsonProperty("更新弹幕描述")]
    public string Description { get; set; } = "";

    [JsonProperty("更新弹幕列表")]
    public List<UpdateProjData> UpdateProjectiles { get; set; } = new List<UpdateProjData>();
}

// 更新弹幕文件管理器
public class UpProjFile
{
    public static readonly string Dir = Path.Combine(TShock.SavePath, "怪物加速", "更新弹幕配置");

    #region 初始化
    public static void Init()
    {
        try
        {
            if (!Directory.Exists(Dir))
                Directory.CreateDirectory(Dir);

            if (Directory.GetFiles(Dir, "*.json").Length == 0)
                CreateDefault();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 更新弹幕系统初始化失败: {ex.Message}");
        }
    }
    #endregion

    #region 创建默认更新弹幕配置
    private static void CreateDefault()
    {
        try
        {
            var defaults = new List<UpData>
            {
                new UpData
                {
                    Name = "追踪弹幕",
                    Description = "自动追踪玩家的弹幕",
                    UpdateProjectiles = new List<UpdateProjData>
                    {
                        new UpdateProjData
                        {
                            NewType = 1,
                            UpdateTime = 250f,
                            ExtraTime = 60,
                            HomingMode = new HomingData()
                            {
                                Homing = true,
                            }
                        },
                        new UpdateProjData
                        {
                            NewType = 2,
                            UpdateTime = 500f,
                            ExtraTime = 60,
                            HomingMode = new HomingData()
                            {
                                Homing = true,
                            }
                        },
                        new UpdateProjData
                        {
                            NewType = -1,
                            UpdateTime = 250f,
                            ExtraTime = 60,
                            HomingMode = new HomingData()
                            {
                                Homing = true,
                                Avoid = true,
                            }
                        }
                    }
                },

                new UpData
                {
                    Name = "多段更新",
                    Description = "多段式更新弹幕",
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
                },


            };

            foreach (var updateProj in defaults)
            {
                SaveFile(updateProj);
            }

            TShock.Log.ConsoleInfo($"{LogName} 已创建 {defaults.Count} 个更新弹幕配置文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 创建更新弹幕配置失败: {ex.Message}");
        }
    }
    #endregion

    #region 获取更新弹幕配置 - 直接读取文件
    public static UpData GetFile(string name)
    {
        try
        {
            if (!Directory.Exists(Dir))
                return new UpData();

            var filePath = Path.Combine(Dir, $"{name}.json");

            if (!File.Exists(filePath))
                return new UpData();

            var content = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<UpData>(content)!;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 读取更新弹幕配置失败: {name}, 错误: {ex.Message}");
            return new UpData();
        }
    }

    public static List<UpdateProjData> GetData(string name)
    {
        var file = GetFile(name);
        if (file == null || file.UpdateProjectiles == null || file.UpdateProjectiles.Count == 0)
        {
            TShock.Log.ConsoleError($"{LogName} 更新弹幕配置 '{name}' 不存在或为空，使用默认更新弹幕");
            return new List<UpdateProjData> { new UpdateProjData() };
        }
        return file.UpdateProjectiles;
    }
    #endregion

    #region 保存更新弹幕配置
    public static void SaveFile(UpData update)
    {
        try
        {
            var file = $"{update.Name}.json";
            var path = Path.Combine(Dir, file);
            var json = JsonConvert.SerializeObject(update, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 保存更新弹幕配置失败: {update.Name}, 错误: {ex.Message}");
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

            var all = new List<UpData>();
            var jsonFiles = Directory.GetFiles(Dir, "*.json");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var file = JsonConvert.DeserializeObject<UpData>(content);
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