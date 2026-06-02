using System.Text.Json;

namespace HGmusic;

public class AppConfig
{
    // 缓存 JsonSerializerOptions，避免每次序列化/反序列化时重新创建
    private static readonly JsonSerializerOptions CachedJsonOptions = new() { WriteIndented = true };

    private const int CurrentVersion = 1;

    public int ConfigVersion { get; set; } = CurrentVersion;
    public int GameDeviceNumber { get; set; }
    public int LocalDeviceNumber { get; set; }
    public float GameVolume { get; set; } = 1.0f;
    public float LocalVolume { get; set; } = 1.0f;
    public bool StartMinimized { get; set; }
    public bool AllowOverlap { get; set; } = false;

    public int PttKey { get; set; } = (int)Keys.V;
    public int PttModifiers { get; set; }

    public int PlayTriggerKey { get; set; } = (int)Keys.P;
    public int PlayTriggerModifiers { get; set; }

    public int StopKey { get; set; } = (int)Keys.S;
    public int StopModifiers { get; set; } = (int)Keys.Control;

    public int PlayDurationSeconds { get; set; } = 15;

    public int MicDeviceNumber { get; set; } = -1;
    public float MicVolume { get; set; } = 1.0f;
    public bool PttEnable { get; set; } = true;
    public bool MicPassthrough { get; set; }

    public List<HotkeyBinding> Bindings { get; set; } = new();

    public static string CurrentPresetName { get; set; } = "默认预设";

    private static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HGmusic");

    private static string PresetDir(string presetName) => Path.Combine(ConfigDir, presetName);

    private static string ConfigPath => Path.Combine(PresetDir(CurrentPresetName), "config.json");

    private static string PresetConfigPath(string presetName) => Path.Combine(PresetDir(presetName), "config.json");

    public static List<string> GetPresets()
    {
        var presets = new List<string>();
        try
        {
            if (!Directory.Exists(ConfigDir))
                return new List<string> { "默认预设" };

            foreach (var dir in Directory.EnumerateDirectories(ConfigDir))
            {
                var name = Path.GetFileName(dir);
                if (File.Exists(PresetConfigPath(name)))
                    presets.Add(name);
            }
        }
        catch { }

        if (presets.Count == 0)
            presets.Add("默认预设");

        return presets;
    }

    public static void SetPreset(string name)
    {
        var presetDir = PresetDir(name);
        try
        {
            Directory.CreateDirectory(presetDir);
        }
        catch { }

        CurrentPresetName = name;
    }

    public static void CreatePreset(string name)
    {
        var presetDir = PresetDir(name);
        Directory.CreateDirectory(presetDir);
        var cfg = new AppConfig();
        var path = PresetConfigPath(name);
        var json = JsonSerializer.Serialize(cfg, CachedJsonOptions);
        File.WriteAllText(path, json);
    }

    public static void DeletePreset(string name)
    {
        var presetDir = PresetDir(name);
        try
        {
            if (Directory.Exists(presetDir))
                Directory.Delete(presetDir, true);
        }
        catch { }
    }

    public static void DuplicatePreset(string sourceName, string newName)
    {
        try
        {
            var srcDir = PresetDir(sourceName);
            var dstDir = PresetDir(newName);
            if (!Directory.Exists(srcDir))
                return;

            Directory.CreateDirectory(dstDir);
            foreach (var file in Directory.EnumerateFiles(srcDir))
                File.Copy(file, Path.Combine(dstDir, Path.GetFileName(file)), overwrite: true);
        }
        catch { }
    }

    public static AppConfig Load()
    {
        try
        {
            var presetDir = PresetDir(CurrentPresetName);
            Directory.CreateDirectory(presetDir);

            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null)
                {
                    if (cfg.ConfigVersion < CurrentVersion)
                    {
                        var backupPath = ConfigPath + ".bak";
                        try { File.Copy(ConfigPath, backupPath, overwrite: true); } catch { }
                        Migrate(cfg, cfg.ConfigVersion);
                        cfg.ConfigVersion = CurrentVersion;
                        cfg.Save();
                    }
                    cfg.PlayDurationSeconds = Math.Clamp(cfg.PlayDurationSeconds, 1, 60);
                    return cfg;
                }
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var presetDir = PresetDir(CurrentPresetName);
            Directory.CreateDirectory(presetDir);
            var json = JsonSerializer.Serialize(this, CachedJsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    private static void Migrate(AppConfig cfg, int fromVersion)
    {
    }
}
