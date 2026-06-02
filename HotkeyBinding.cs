namespace Soundboard;

public class HotkeyBinding
{
    public int Key { get; set; }
    public int Modifiers { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    public double TrimStartSeconds { get; set; }
    public double TrimEndSeconds { get; set; }

    public bool HasTrim => TrimStartSeconds > 0 || (TrimEndSeconds > 0 && TrimEndSeconds < Duration.TotalSeconds);

    public string TrimDisplay
    {
        get
        {
            if (!HasTrim) return "";
            var start = TrimStartSeconds > 0 ? $"{TrimStartSeconds:F1}s" : "0s";
            var end = TrimEndSeconds > 0 && TrimEndSeconds < Duration.TotalSeconds
                ? $"{TrimEndSeconds:F1}s" : $"{Duration.TotalSeconds:F1}s";
            return $"{start} ~ {end}";
        }
    }

    public string KeyDisplay
    {
        get
        {
            var parts = new List<string>();
            var mods = (Keys)Modifiers;
            var key = (Keys)Key;
            if (mods.HasFlag(Keys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(Keys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(Keys.Shift)) parts.Add("Shift");
            parts.Add(KeyToString(key));
            return string.Join("+", parts);
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public string DurationDisplay => Duration.TotalSeconds > 0
        ? $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}"
        : "--:--";

    public bool IsPlaying { get; set; }

    public bool ConflictsWith(HotkeyBinding other)
    {
        if (other == null || other.Key == (int)Keys.None) return false;
        return Key == other.Key && Modifiers == other.Modifiers;
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
