namespace HGmusic;

public class HotkeyBinding
{
    // 缓存计算结果，避免每次访问属性时重新计算
    private string? _cachedKeyDisplay;
    private string? _cachedDurationDisplay;
    private string? _cachedTrimDisplay;
    private string? _cachedFileName;
    private int _lastKey = -1;
    private int _lastModifiers = -1;

    public int Key
    {
        get => _key;
        set { if (_key != value) { _key = value; InvalidateCache(); } }
    }
    private int _key;

    public int Modifiers
    {
        get => _modifiers;
        set { if (_modifiers != value) { _modifiers = value; InvalidateCache(); } }
    }
    private int _modifiers;

    public string FilePath
    {
        get => _filePath;
        set { if (_filePath != value) { _filePath = value; _cachedFileName = null; } }
    }
    private string _filePath = string.Empty;

    public double DurationSeconds
    {
        get => _durationSeconds;
        set { if (_durationSeconds != value) { _durationSeconds = value; _cachedDurationDisplay = null; _cachedTrimDisplay = null; } }
    }
    private double _durationSeconds;

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    public double TrimStartSeconds
    {
        get => _trimStartSeconds;
        set { if (_trimStartSeconds != value) { _trimStartSeconds = value; _cachedTrimDisplay = null; } }
    }
    private double _trimStartSeconds;

    public double TrimEndSeconds
    {
        get => _trimEndSeconds;
        set { if (_trimEndSeconds != value) { _trimEndSeconds = value; _cachedTrimDisplay = null; } }
    }
    private double _trimEndSeconds;

    public bool HasTrim => TrimStartSeconds > 0 || (TrimEndSeconds > 0 && TrimEndSeconds < Duration.TotalSeconds);

    public string TrimDisplay
    {
        get
        {
            if (_cachedTrimDisplay != null) return _cachedTrimDisplay;
            if (!HasTrim) { _cachedTrimDisplay = ""; return ""; }
            var start = TrimStartSeconds > 0 ? $"{TrimStartSeconds:F1}s" : "0s";
            var end = TrimEndSeconds > 0 && TrimEndSeconds < Duration.TotalSeconds
                ? $"{TrimEndSeconds:F1}s" : $"{Duration.TotalSeconds:F1}s";
            _cachedTrimDisplay = $"{start} ~ {end}";
            return _cachedTrimDisplay;
        }
    }

    public string KeyDisplay
    {
        get
        {
            if (_cachedKeyDisplay != null && _lastKey == _key && _lastModifiers == _modifiers)
                return _cachedKeyDisplay;
            _lastKey = _key;
            _lastModifiers = _modifiers;

            var mods = (Keys)_modifiers;
            var key = (Keys)_key;
            // 使用 Span 栈分配避免 List 堆分配
            Span<char> buffer = stackalloc char[64];
            int pos = 0;

            if (mods.HasFlag(Keys.Control)) { "Ctrl".CopyTo(buffer[pos..]); pos += 4; buffer[pos++] = '+'; }
            if (mods.HasFlag(Keys.Alt)) { "Alt".CopyTo(buffer[pos..]); pos += 3; buffer[pos++] = '+'; }
            if (mods.HasFlag(Keys.Shift)) { "Shift".CopyTo(buffer[pos..]); pos += 5; buffer[pos++] = '+'; }

            var keyStr = KeyToString(key);
            keyStr.CopyTo(buffer[pos..]);
            pos += keyStr.Length;

            _cachedKeyDisplay = new string(buffer[..pos]);
            return _cachedKeyDisplay;
        }
    }

    public string FileName
    {
        get
        {
            if (_cachedFileName != null) return _cachedFileName;
            _cachedFileName = Path.GetFileName(_filePath);
            return _cachedFileName;
        }
    }

    public string DurationDisplay
    {
        get
        {
            if (_cachedDurationDisplay != null) return _cachedDurationDisplay;
            var dur = Duration;
            _cachedDurationDisplay = dur.TotalSeconds > 0
                ? $"{(int)dur.TotalMinutes}:{dur.Seconds:D2}"
                : "--:--";
            return _cachedDurationDisplay;
        }
    }

    public bool IsPlaying { get; set; }

    public bool ConflictsWith(HotkeyBinding other)
    {
        if (other == null || other.Key == (int)Keys.None) return false;
        return _key == other.Key && _modifiers == other.Modifiers;
    }

    private void InvalidateCache()
    {
        _cachedKeyDisplay = null;
    }

    private static string KeyToString(Keys key)
    {
        return key switch
        {
            >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => $"NumPad{(int)(key - Keys.NumPad0)}",
            >= Keys.F1 and <= Keys.F24 => $"F{(int)(key - Keys.F1 + 1)}",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.Oem1 => ";",
            Keys.Oem7 => "'",
            Keys.Oem5 => "\\",
            Keys.OemQuestion => "/",
            Keys.Oemtilde => "`",
            Keys.Space => "Space",
            _ => key.ToString()
        };
    }
}
