using System.Drawing.Drawing2D;
using NAudio.Wave;

namespace HGmusic;

public static class WaveformRenderer
{
    private static readonly Color C_Bg = Color.FromArgb(10, 10, 18);
    private static readonly Color C_Blue = Color.FromArgb(91, 142, 239);
    private static readonly Color C_TextDim = Color.FromArgb(100, 116, 139);

    public static float[] ExtractPeaks(string filePath, int targetWidth)
    {
        if (targetWidth <= 0)
            return Array.Empty<float>();

        // 预估容量：假设 44.1kHz，每帧一个采样点
        var frames = new List<float>(44100);

        try
        {
            using var reader = new AudioFileReader(filePath);
            ReadSampleFrames(reader, frames);
        }
        catch
        {
            try
            {
                using var reader = new MediaFoundationReader(filePath);
                var provider = reader.ToSampleProvider();
                ReadSampleFrames(provider, frames);
            }
            catch
            {
                return new float[targetWidth]; // 默认初始化为 0
            }
        }

        if (frames.Count == 0)
            return new float[targetWidth];

        // 直接索引访问 List 内部数组
        int frameCount = frames.Count;
        var groupSize = (float)frameCount / targetWidth;
        var peaks = new float[targetWidth];

        for (int i = 0; i < targetWidth; i++)
        {
            int start = (int)(i * groupSize);
            int end = (int)((i + 1) * groupSize);
            if (end > frameCount) end = frameCount;
            if (start >= end) continue;

            float max = 0f;
            for (int j = start; j < end; j++)
            {
                float val = frames[j];
                if (val > max) max = val;
            }
            peaks[i] = max;
        }

        return peaks;
    }

    private static void ReadSampleFrames(ISampleProvider provider, List<float> frames)
    {
        int channels = provider.WaveFormat.Channels;
        var buffer = new float[4096 * channels];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i += channels)
            {
                float maxVal = 0f;
                for (int ch = 0; ch < channels && i + ch < read; ch++)
                {
                    float abs = Math.Abs(buffer[i + ch]);
                    if (abs > maxVal) maxVal = abs;
                }
                frames.Add(maxVal);
            }
        }
    }

    public static Bitmap Render(float[] peaks, int width, int height, double startFraction = 0, double endFraction = 1.0)
    {
        var bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighSpeed;
        g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        g.Clear(C_Bg);

        if (peaks == null || peaks.Length == 0 || width <= 0 || height <= 0)
            return bmp;

        int startX = (int)(Math.Clamp(startFraction, 0, 1) * width);
        int endX = (int)(Math.Clamp(endFraction, 0, 1) * width);
        startX = Math.Clamp(startX, 0, width - 1);
        endX = Math.Clamp(endX, 0, width);

        float halfH = height / 2f;
        using var overlayBrush = new SolidBrush(Color.FromArgb(180, 10, 10, 18));

        using (var fullPath = BuildWaveformPath(peaks, width, halfH, 0, width - 1))
        {
            using var brush = new SolidBrush(C_TextDim);
            g.FillPath(brush, fullPath);
        }

        if (startX < endX)
        {
            using var selPath = BuildWaveformPath(peaks, width, halfH, startX, endX - 1);
            using var brush = new SolidBrush(C_Blue);
            g.FillPath(brush, selPath);
        }

        if (startX > 0)
            g.FillRectangle(overlayBrush, 0, 0, startX, height);

        if (endX < width)
            g.FillRectangle(overlayBrush, endX, 0, width - endX, height);

        return bmp;
    }

    private static GraphicsPath BuildWaveformPath(float[] peaks, int width, float halfH, int fromX, int toX)
    {
        var path = new GraphicsPath();
        int count = toX - fromX + 1;
        if (count <= 0) return path;

        var top = new PointF[count];
        var bot = new PointF[count];

        for (int i = 0; i < count; i++)
        {
            int x = fromX + i;
            int idx = (int)((long)x * peaks.Length / width);
            if (idx >= peaks.Length) idx = peaks.Length - 1;
            if (idx < 0) idx = 0;
            float peak = Math.Clamp(peaks[idx], 0f, 1f);
            top[i] = new PointF(x, halfH - peak * halfH);
            bot[i] = new PointF(x, halfH + peak * halfH);
        }

        path.AddLines(top);
        Array.Reverse(bot);
        path.AddLines(bot);
        path.CloseFigure();

        return path;
    }

    public static double GetDuration(string filePath)
    {
        try
        {
            using var reader = new AudioFileReader(filePath);
            return reader.TotalTime.TotalSeconds;
        }
        catch { }

        try
        {
            using var reader = new MediaFoundationReader(filePath);
            return reader.TotalTime.TotalSeconds;
        }
        catch { }

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".mp3")
            {
                using var reader = new Mp3FileReader(filePath);
                return reader.TotalTime.TotalSeconds;
            }
            else
            {
                using var reader = new WaveFileReader(filePath);
                return reader.TotalTime.TotalSeconds;
            }
        }
        catch { return 0; }
    }
}
