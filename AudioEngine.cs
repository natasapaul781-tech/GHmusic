using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard;

public class AudioEngine : IDisposable
{
    private readonly object _lock = new();
    private readonly List<PlaybackSession> _activeSessions = new();

    private int _gameDeviceNumber;
    private int _localDeviceNumber;
    private int _micDeviceNumber = -1;
    private float _gameVolume = 1.0f;
    private float _localVolume = 1.0f;
    private float _micVolume = 1.0f;
    private int _playDurationMs = 15000;
    private bool _pttEnabled = true;
    private Keys _pttKey = Keys.V;
    private Keys _pttModifiers;
    private bool _pttCurrentlyDown;
    private bool _micPassthroughEnabled;

    // 麦克风混音相关
    private WaveInEvent? _micCapture;
    private WaveOutEvent? _micOutput;
    private BufferedWaveProvider? _micBuffer;
    private VolumeSampleProvider? _micVolumeProvider;
    private bool _micPassthroughActive;
    private readonly object _micLock = new();

    public event Action<string>? PlaybackStarted;
    public event Action<string>? PlaybackStopped;
    public event Action? PttStateChanged;

    public int GameDeviceNumber { get => _gameDeviceNumber; set => _gameDeviceNumber = value; }
    public int LocalDeviceNumber { get => _localDeviceNumber; set => _localDeviceNumber = value; }
    public int MicDeviceNumber { get => _micDeviceNumber; set => _micDeviceNumber = value; }
    public float GameVolume { get => _gameVolume; set => _gameVolume = Math.Clamp(value, 0f, 1f); }
    public float LocalVolume { get => _localVolume; set => _localVolume = Math.Clamp(value, 0f, 1f); }
    public float MicVolume { get => _micVolume; set => _micVolume = Math.Clamp(value, 0f, 1f); }
    public int PlayDurationMs { get => _playDurationMs; set => _playDurationMs = Math.Clamp(value, 1000, 60000); }
    public bool PttEnabled { get => _pttEnabled; set => _pttEnabled = value; }
    public Keys PttKey { get => _pttKey; set => _pttKey = value; }
    public Keys PttModifiers { get => _pttModifiers; set => _pttModifiers = value; }
    public bool IsPttDown => _pttCurrentlyDown;
    public bool MicPassthroughEnabled
    {
        get => _micPassthroughEnabled;
        set
        {
            if (_micPassthroughEnabled != value)
            {
                _micPassthroughEnabled = value;
                if (value)
                    StartMicPassthrough();
                else
                    StopMicPassthrough();
            }
        }
    }
    public bool IsMicPassthroughActive => _micPassthroughActive;

    public int ActiveSessionCount { get { lock (_lock) return _activeSessions.Count; } }
    public int PlayingCount => ActiveSessionCount;
    public bool IsPlayingAny => PlayingCount > 0;

    public AudioEngine()
    {
    }

    public AudioEngine(int gameDeviceNumber, int localDeviceNumber,
        float gameVolume = 1.0f, float localVolume = 1.0f)
    {
        _gameDeviceNumber = gameDeviceNumber;
        _localDeviceNumber = localDeviceNumber;
        _gameVolume = Math.Clamp(gameVolume, 0f, 1f);
        _localVolume = Math.Clamp(localVolume, 0f, 1f);
    }

    public void Play(string filePath)
    {
        PlayInternal(filePath, _gameDeviceNumber, _localDeviceNumber, _gameVolume, _localVolume, _playDurationMs);
    }

    public void Play(string filePath, double trimStartSec, double trimEndSec)
    {
        PlayInternal(filePath, _gameDeviceNumber, _localDeviceNumber, _gameVolume, _localVolume, _playDurationMs, trimStartSec, trimEndSec);
    }

    public void PlayWithoutPtt(string filePath, double trimStartSec = 0, double trimEndSec = 0)
    {
        if (trimStartSec > 0 || trimEndSec > 0)
            Play(filePath, trimStartSec, trimEndSec);
        else
            Play(filePath);
    }

    private void PlayInternal(string filePath, int devGame, int devLocal, float volGame, float volLocal, int maxDurationMs,
        double trimStartSec = 0, double trimEndSec = 0)
    {
        if (!File.Exists(filePath)) return;

        devGame = ValidateDevice(devGame);
        devLocal = ValidateDevice(devLocal);

        if (devGame < 0 && devLocal < 0) return;

        byte[] fileBytes;
        try { fileBytes = File.ReadAllBytes(filePath); }
        catch { return; }

        var session = new PlaybackSession();
        int totalDevices = 0;

        try
        {
            if (devGame >= 0)
            {
                var ms = new MemoryStream(fileBytes.ToArray());
                var reader = CreateReader(ms, filePath);
                ApplyTrim(reader, trimStartSec);
                var wave = new WaveOutEvent { DeviceNumber = devGame, Volume = volGame };
                wave.Init(reader);

                session.WaveGame = wave;
                session.ReaderGame = reader;
                session.StreamGame = ms;
                totalDevices++;
            }

            if (devLocal >= 0)
            {
                var ms = new MemoryStream(fileBytes.ToArray());
                var reader = CreateReader(ms, filePath);
                ApplyTrim(reader, trimStartSec);
                var wave = new WaveOutEvent { DeviceNumber = devLocal, Volume = volLocal };
                wave.Init(reader);

                session.WaveLocal = wave;
                session.ReaderLocal = reader;
                session.StreamLocal = ms;
                totalDevices++;
            }

            session.TotalCount = totalDevices;
            var playingFile = filePath;

            EventHandler<StoppedEventArgs> onStopped = (s, e) =>
            {
                bool allDone;
                lock (_lock)
                {
                    session.CompletedCount++;
                    allDone = session.CompletedCount >= session.TotalCount;
                    if (allDone)
                        _activeSessions.Remove(session);
                }

                if (allDone)
                {
                    session.Dispose();
                    ReleasePtt();
                    PlaybackStopped?.Invoke(playingFile);
                }
            };

            if (session.WaveGame != null)
                session.WaveGame.PlaybackStopped += onStopped;
            if (session.WaveLocal != null)
                session.WaveLocal.PlaybackStopped += onStopped;

            lock (_lock)
            {
                _activeSessions.Add(session);
            }

            session.WaveGame?.Play();
            session.WaveLocal?.Play();

            try { PressPtt(); } catch { }

            PlaybackStarted?.Invoke(playingFile);

            var effectiveDurationMs = maxDurationMs;
            if (trimEndSec > 0)
            {
                var trimDurationMs = (int)((trimEndSec - trimStartSec) * 1000);
                if (trimDurationMs > 0)
                    effectiveDurationMs = effectiveDurationMs > 0 ? Math.Min(effectiveDurationMs, trimDurationMs) : trimDurationMs;
            }

            if (effectiveDurationMs > 0)
            {
                session.StopTimer = new System.Threading.Timer(_ =>
                {
                    session.StopTimer?.Dispose();
                    session.StopTimer = null;
                    session.Stop();
                }, null, effectiveDurationMs, System.Threading.Timeout.Infinite);
            }
        }
        catch
        {
            session.Dispose();
            try { ReleasePtt(); } catch { }
        }
    }

    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var s in _activeSessions.ToList())
            {
                s.Stop();
                _activeSessions.Remove(s);
                s.Dispose();
            }
        }
        try { ReleasePtt(); } catch { }
    }

    public void UpdateVolumes(float gameVol, float localVol, float micVol = 1.0f)
    {
        _gameVolume = Math.Clamp(gameVol, 0f, 1f);
        _localVolume = Math.Clamp(localVol, 0f, 1f);
        _micVolume = Math.Clamp(micVol, 0f, 1f);
        UpdateMicPassthroughVolume();
    }

    public void UpdateMicPassthroughVolume()
    {
        lock (_micLock)
        {
            if (_micVolumeProvider != null)
            {
                _micVolumeProvider.Volume = _micVolume;
            }
        }
    }

    private void StartMicPassthrough()
    {
        lock (_micLock)
        {
            if (_micPassthroughActive) return;
            try
            {
                // 查找物理麦克风设备（第一个非虚拟设备）
                int physicalMicDevice = FindPhysicalMicDevice();
                if (physicalMicDevice < 0)
                {
                    // 如果没找到物理麦克风，使用默认设备
                    physicalMicDevice = 0;
                }

                // 创建麦克风捕获 - 优化延迟设置
                _micCapture = new WaveInEvent
                {
                    DeviceNumber = physicalMicDevice,
                    // 使用 16kHz 采样率，降低延迟同时保持语音清晰度
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    // 设置较小的缓冲区，减少延迟（20ms）
                    BufferMilliseconds = 20,
                    NumberOfBuffers = 3
                };

                // 创建缓冲区 - 使用极小的缓冲区减少延迟（100ms）
                _micBuffer = new BufferedWaveProvider(_micCapture.WaveFormat)
                {
                    BufferLength = _micCapture.WaveFormat.AverageBytesPerSecond / 10, // 约 100ms 缓冲
                    DiscardOnBufferOverflow = true
                };

                // 创建音量控制
                _micVolumeProvider = new VolumeSampleProvider(_micBuffer.ToSampleProvider())
                {
                    Volume = _micVolume
                };

                // 创建输出设备（使用游戏输出设备，即虚拟麦克风）
                int outputDevice = ValidateDevice(_gameDeviceNumber);
                if (outputDevice < 0)
                {
                    outputDevice = 0;
                }

                _micOutput = new WaveOutEvent
                {
                    DeviceNumber = outputDevice,
                    Volume = 1.0f,
                    // 设置较小的缓冲区减少输出延迟
                    DesiredLatency = 50,
                    NumberOfBuffers = 3
                };
                _micOutput.Init(_micVolumeProvider);

                // 设置数据捕获回调
                _micCapture.DataAvailable += (s, e) =>
                {
                    lock (_micLock)
                    {
                        if (_micBuffer != null)
                        {
                            _micBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        }
                    }
                };

                // 开始捕获和播放
                _micCapture.StartRecording();
                _micOutput.Play();
                _micPassthroughActive = true;
            }
            catch
            {
                StopMicPassthrough();
            }
        }
    }

    private void StopMicPassthrough()
    {
        lock (_micLock)
        {
            _micPassthroughActive = false;

            if (_micCapture != null)
            {
                try { _micCapture.StopRecording(); } catch { }
                try { _micCapture.Dispose(); } catch { }
                _micCapture = null;
            }

            if (_micOutput != null)
            {
                try { _micOutput.Stop(); } catch { }
                try { _micOutput.Dispose(); } catch { }
                _micOutput = null;
            }

            _micBuffer = null;
            _micVolumeProvider = null;
        }
    }

    private int FindPhysicalMicDevice()
    {
        try
        {
            // 遍历所有 WaveIn 设备，找到第一个非虚拟设备
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                var productName = capabilities.ProductName ?? "";

                // 跳过虚拟设备（如 VB-Cable）
                if (!productName.Contains("CABLE", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        catch { }

        return -1;
    }

    private void PressPtt()
    {
        if (!_pttEnabled || _pttCurrentlyDown) return;
        _pttCurrentlyDown = true;

        InputSimulator.KeyDownWithModifiers(_pttKey, _pttModifiers, useScanCode: true);

        PttStateChanged?.Invoke();
    }

    private void ReleasePtt()
    {
        if (!_pttCurrentlyDown) return;
        _pttCurrentlyDown = false;

        InputSimulator.KeyUpWithModifiers(_pttKey, _pttModifiers, useScanCode: true);

        PttStateChanged?.Invoke();
    }

    public void PressPttManual()
    {
        if (_pttCurrentlyDown) return;
        _pttCurrentlyDown = true;

        InputSimulator.KeyDownWithModifiers(_pttKey, _pttModifiers, useScanCode: true);

        PttStateChanged?.Invoke();
    }

    public void ReleasePttManual()
    {
        if (!_pttCurrentlyDown) return;
        _pttCurrentlyDown = false;

        InputSimulator.KeyUpWithModifiers(_pttKey, _pttModifiers, useScanCode: true);

        PttStateChanged?.Invoke();
    }

    private static WaveStream CreateReader(Stream stream, string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => new Mp3FileReader(stream),
            ".wav" => new WaveFileReader(stream),
            ".aiff" or ".aif" => new AiffFileReader(stream),
            _ => new WaveFileReader(stream)
        };
    }

    private static void ApplyTrim(WaveStream reader, double trimStartSec)
    {
        if (trimStartSec > 0)
        {
            try { reader.CurrentTime = TimeSpan.FromSeconds(trimStartSec); } catch { }
        }
    }

    private static int ValidateDevice(int deviceNumber)
    {
        if (deviceNumber < 0) return -1;
        try
        {
            if (deviceNumber < WaveOut.DeviceCount) return deviceNumber;
        }
        catch { }
        return -1;
    }

    public void Dispose()
    {
        StopAll();
    }
}

internal class PlaybackSession : IDisposable
{
    public WaveOutEvent? WaveGame;
    public WaveOutEvent? WaveLocal;
    public WaveStream? ReaderGame;
    public WaveStream? ReaderLocal;
    public MemoryStream? StreamGame;
    public MemoryStream? StreamLocal;
    public int CompletedCount;
    public int TotalCount;
    public System.Threading.Timer? StopTimer;

    public void Stop()
    {
        StopTimer?.Dispose();
        StopTimer = null;
        try { WaveGame?.Stop(); } catch { }
        try { WaveLocal?.Stop(); } catch { }
    }

    public void Dispose()
    {
        StopTimer?.Dispose();
        StopTimer = null;
        try { WaveGame?.Dispose(); } catch { }
        try { WaveLocal?.Dispose(); } catch { }
        try { ReaderGame?.Dispose(); } catch { }
        try { ReaderLocal?.Dispose(); } catch { }
        try { StreamGame?.Dispose(); } catch { }
        try { StreamLocal?.Dispose(); } catch { }
    }
}
