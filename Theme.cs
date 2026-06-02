namespace HGmusic;

public static class Theme
{
    public static class Colors
    {
        public static Color Bg = Color.FromArgb(10, 10, 18);
        public static Color Surface = Color.FromArgb(16, 16, 28);
        public static Color Elevated = Color.FromArgb(24, 24, 40);
        public static Color HeaderBg = Color.FromArgb(14, 14, 24);
        public static Color FooterBg = Color.FromArgb(12, 12, 20);

        public static Color TextPrimary = Color.FromArgb(226, 232, 240);
        public static Color TextSecondary = Color.FromArgb(148, 163, 184);
        public static Color TextMuted = Color.FromArgb(100, 116, 139);

        public static Color Accent = Color.FromArgb(91, 142, 239);
        public static Color AccentHover = Color.FromArgb(110, 158, 245);
        public static Color AccentDim = Color.FromArgb(40, 60, 110);

        public static Color Success = Color.FromArgb(74, 222, 128);
        public static Color Warning = Color.FromArgb(245, 158, 11);
        public static Color Error = Color.FromArgb(248, 113, 113);
        public static Color Info = Color.FromArgb(56, 189, 248);

        public static Color Border = Color.FromArgb(32, 32, 50);
        public static Color BorderHover = Color.FromArgb(55, 55, 80);
        public static Color Divider = Color.FromArgb(28, 28, 44);

        public static Color CardBgTop = Color.FromArgb(30, 31, 44);
        public static Color CardBgBot = Color.FromArgb(24, 25, 37);
        public static Color CardHoverTop = Color.FromArgb(42, 43, 58);
        public static Color CardHoverBot = Color.FromArgb(34, 35, 48);
        public static Color CardBorder = Color.FromArgb(50, 52, 66);
        public static Color CardBorderHover = Color.FromArgb(90, 110, 160);
        public static Color CardPlayingBorder = Color.FromArgb(72, 210, 130);
        public static Color CardPlayingDot = Color.FromArgb(72, 210, 130);
        public static Color CardIcon = Color.FromArgb(120, 140, 240);

        public static Color AddCardBgTop = Color.FromArgb(20, 21, 32);
        public static Color AddCardBgBot = Color.FromArgb(16, 17, 26);
        public static Color AddCardBorder = Color.FromArgb(42, 44, 58);
        public static Color AddCardBorderHover = Color.FromArgb(70, 90, 145);
        public static Color AddCardText = Color.FromArgb(70, 72, 90);
        public static Color AddCardTextHover = Color.FromArgb(150, 152, 175);

        public static Color ButtonDefault = Color.FromArgb(30, 30, 50);
        public static Color ButtonHover = Color.FromArgb(45, 45, 70);
        public static Color ButtonPrimary = Color.FromArgb(40, 80, 160);
        public static Color ButtonPrimaryHover = Color.FromArgb(55, 100, 180);
        public static Color ButtonDanger = Color.FromArgb(60, 40, 40);
        public static Color ButtonDangerHover = Color.FromArgb(80, 50, 50);

        public static Color Overlay = Color.FromArgb(160, 0, 0, 0);
        public static Color TrackBarTrack = Color.FromArgb(40, 40, 60);
        public static Color TrackBarThumb = Color.FromArgb(91, 142, 239);
        public static Color ScrollBarTrack = Color.FromArgb(20, 20, 30);
        public static Color ScrollBarThumb = Color.FromArgb(60, 60, 80);
        public static Color ScrollBarThumbHover = Color.FromArgb(80, 80, 105);
    }

    public static class Fonts
    {
        // 注意：不能缓存 Font 对象，因为调用方使用 `using` 会 dispose 掉缓存实例
        // Font 创建开销很小，每次新建是安全且正确的做法
        public static Font Title(float scale = 1.25f) => new("Segoe UI", 16f * scale, FontStyle.Bold);
        public static Font Subtitle(float scale = 1.25f) => new("Segoe UI", 12f * scale, FontStyle.Bold);
        public static Font Body(float scale = 1.25f) => new("Segoe UI", 11f * scale, FontStyle.Regular);
        public static Font BodyBold(float scale = 1.25f) => new("Segoe UI", 11f * scale, FontStyle.Bold);
        public static Font Caption(float scale = 1.25f) => new("Segoe UI", 9.5f * scale, FontStyle.Regular);
        public static Font Small(float scale = 1.25f) => new("Segoe UI", 8.5f * scale, FontStyle.Regular);

        public const string Family = "Segoe UI";
    }

    public static class Spacing
    {
        public static int XS(float scale = 1.25f) => (int)(6 * scale);
        public static int SM(float scale = 1.25f) => (int)(12 * scale);
        public static int MD(float scale = 1.25f) => (int)(22 * scale);
        public static int LG(float scale = 1.25f) => (int)(34 * scale);
        public static int XL(float scale = 1.25f) => (int)(46 * scale);
        public static int XXL(float scale = 1.25f) => (int)(64 * scale);
    }

    public static class Radius
    {
        public static int SM(float scale = 1.25f) => (int)(6 * scale);
        public static int MD(float scale = 1.25f) => (int)(12 * scale);
        public static int LG(float scale = 1.25f) => (int)(20 * scale);
    }

    public static class Duration
    {
        public const int Fast = 180;
        public const int Normal = 300;
        public const int Slow = 500;
    }

    public static class Easing
    {
        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        public static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
        }
    }

    public static class Icons
    {
        public const string Play = "\u25b6";
        public const string Stop = "\u25a0";
        public const string Settings = "\u2630";
        public const string Add = "+";
        public const string Delete = "\u2715";
        public const string Save = "\u2714";
        public const string KeyBind = "\u2328";
        public const string Trim = "\u2702";
        public const string Check = "\u2714";
        public const string Cross = "\u2716";
        public const string Warning = "\u26a0";
        public const string Info = "\u2139";
        public const string Gear = "\u2699";
        public const string Arrow = "\u25b8";
        public const string Music = "\u266b";
        public const string FileAudio = "\u266a";
        public const string Wave = "\u25c6";
        public const string DotActive = "\u25cf";
        public const string DotInactive = "\u25cb";
        public const string DotProgress = "\u25d0";
    }
}
