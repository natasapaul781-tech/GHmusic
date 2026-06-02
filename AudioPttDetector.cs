using NAudio.Wave;

namespace Soundboard;

public class AudioPttDetector : IDisposable
{
    private WaveInEvent? _waveIn;
    private readonly object _lock = new();
    private bool _pttActive;
    private bool _disposed;

    private const float DefaultThreshold = 0.015f;
    private const int RestartDelayMs = 1500;

    public event Action? PttActivated;
    public event Action? PttDeactivated;

    public bool IsPttActive
    {
        get { lock (_lock) return _pttActive; }
    }

    public AudioPttDetector(int deviceNumber = -1, float threshold = DefaultThreshold)
    {
        if (deviceNumber < 0)
        {
            deviceNumber = FindPhysicalMicDevice();
            if (deviceNumber < 0)
                deviceNumber = 0;
        }

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50,
                NumberOfBuffers = 3,
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
        }
        catch { }
    }

    private static int FindPhysicalMicDevice()
    {
        var cableIndex = FindCableRecordingDevice();
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            if (i == cableIndex) continue;
            return i;
        }
        return -1;
    }

    private static int FindCableRecordingDevice()
    {
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            if (caps.ProductName.Contains("CABLE", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        float rms = CalculateRms(e.Buffer, e.BytesRecorded);
        bool nowActive = rms > DefaultThreshold;

        lock (_lock)
        {
            if (nowActive && !_pttActive)
            {
                _pttActive = true;
                PttActivated?.Invoke();
            }
            else if (!nowActive && _pttActive)
            {
                _pttActive = false;
                PttDeactivated?.Invoke();
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed) return;
        Task.Delay(RestartDelayMs).ContinueWith(_ =>
        {
            if (!_disposed)
            {
                try { _waveIn?.StartRecording(); } catch { }
            }
        });
    }

    private static float CalculateRms(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded < 2) return 0;
        double sum = 0;
        int samples = bytesRecorded / 2;
        for (int i = 0; i < bytesRecorded - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(buffer, i);
            sum += (double)sample * sample;
        }
        return (float)Math.Sqrt(sum / samples) / 32768f;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
        }
        catch { }
    }
}
