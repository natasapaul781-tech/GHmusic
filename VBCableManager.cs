using System.Diagnostics;
using System.IO;
using System.Net.Http;
using NAudio.Wave;

namespace HGmusic;

public class VBCableInstallProgress
{
    public int Step { get; set; }
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string Details { get; set; } = "";
    public bool IsCancelled { get; set; }
}

public class VBCableManager
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(120),
        DefaultRequestHeaders = { UserAgent = { new System.Net.Http.Headers.ProductInfoHeaderValue("HGmusic", "1.0") } }
    };

    private static string LogPath => Path.Combine(Path.GetTempPath(), "vbcable_debug.log");
    private bool _cancelled;

    public event Action<VBCableInstallProgress>? ProgressChanged;

    public void Cancel() => _cancelled = true;

    public static bool IsVBCableInstalled()
    {
        try
        {
            LogMessage("Checking if VBCable is installed...");
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                LogMessage($"  Device {i}: {caps.ProductName}");
                if (caps.ProductName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase) ||
                    caps.ProductName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                    caps.ProductName.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase))
                {
                    LogMessage("  VBCable found!");
                    return true;
                }
            }
            LogMessage("  VBCable not found.");
        }
        catch (Exception ex)
        {
            LogMessage($"Error checking VBCable: {ex}");
        }
        return false;
    }

    public static string? GetVBCableDeviceName()
    {
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            if (caps.ProductName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                return caps.ProductName;
        }
        return null;
    }

    public static string[] GetVBCableDeviceNames()
    {
        var list = new List<string>();
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            if (caps.ProductName.Contains("CABLE", StringComparison.OrdinalIgnoreCase))
                list.Add(caps.ProductName);
        }
        return list.ToArray();
    }

    private void ReportProgress(int step, string status, int progress, string details = "")
    {
        if (_cancelled) return;
        ProgressChanged?.Invoke(new VBCableInstallProgress
        {
            Step = step,
            Status = status,
            Progress = progress,
            Details = details
        });
    }

    private static void LogMessage(string msg)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    public async Task<(bool Success, string Message, bool Downloaded, bool Installed)> EnsureInstalledAsync()
    {
        LogMessage("=== EnsureInstalledAsync starting ===");
        _cancelled = false;

        try
        {
            if (IsVBCableInstalled())
            {
                LogMessage("VBCable already installed.");
                return (true, "VB-Audio Virtual Cable 已安装", false, true);
            }

            var url = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip";
            LogMessage($"Download URL: {url}");

            ReportProgress(0, "正在下载 VB-Audio Virtual Cable 安装包...", 0, "正在连接下载服务器...");

            var tempDir = Path.Combine(Path.GetTempPath(), $"VBCableSetup_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, "VBCable_Driver_Pack45.zip");
            LogMessage($"Temp dir: {tempDir}");
            LogMessage($"Zip path: {zipPath}");

            bool downloaded = false;
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                LogMessage($"Download status: {response.StatusCode}");

                await using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await using var stream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[8192];
                int read;
                long total = response.Content.Headers.ContentLength ?? 0;
                long downloadedBytes = 0;
                LogMessage($"Total size: {total} bytes");

                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    if (_cancelled)
                    {
                        LogMessage("Download cancelled by user.");
                        return (false, "用户取消了下载", downloaded, false);
                    }

                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    downloadedBytes += read;

                    int progress = total > 0 ? (int)(downloadedBytes * 100 / total) : 0;
                    string details = total > 0
                        ? $"{downloadedBytes / 1024.0 / 1024.0:F1} MB / {total / 1024.0 / 1024.0:F1} MB"
                        : $"{downloadedBytes / 1024.0 / 1024.0:F1} MB";
                    ReportProgress(0, $"正在下载安装包... {progress}%", progress, details);
                    LogMessage($"Downloaded: {downloadedBytes}/{total} ({progress}%)");
                }
                LogMessage($"Download complete: {downloadedBytes} bytes");
                downloaded = true;
            }
            catch (Exception ex)
            {
                LogMessage($"Download failed: {ex}");
                return (false, $"下载失败: {ex.Message}\n\n请尝试手动下载：https://vb-audio.com/Cable/", false, false);
            }

            ReportProgress(1, "正在解压安装包...", 100, "正在解压文件...");
            LogMessage("Extracting...");
            try
            {
                LogMessage($"Extracting to: {tempDir}");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);
                LogMessage("Extract complete.");
                LogFiles(tempDir);
                ReportProgress(1, "解压完成！", 100, "文件解压成功");
            }
            catch (Exception ex)
            {
                LogMessage($"Extract failed: {ex}");
                return (false, $"解压失败: {ex.Message}", downloaded, false);
            }

            var setupExe = FindSetupExe(tempDir);
            if (setupExe == null)
            {
                LogMessage("No setup exe found.");
                return (false, "解压后未找到安装程序，请手动安装。", downloaded, false);
            }
            LogMessage($"Setup exe found: {setupExe}");

            ReportProgress(2, "正在安装 VB-Audio Virtual Cable...", 0, "即将启动安装程序（需要管理员权限）...");

            try
            {
                LogMessage("Starting installer with admin rights...");
                ReportProgress(2, "正在安装驱动...", 30, "请在弹出的窗口中确认安装");

                var startInfo = new ProcessStartInfo
                {
                    FileName = setupExe,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    Verb = "runas",
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    LogMessage("Installer process started.");
                    ReportProgress(2, "正在安装驱动...", 60, "等待安装程序完成...");
                    await process.WaitForExitAsync();
                    LogMessage($"Installer exited with code: {process.ExitCode}");
                    ReportProgress(2, "安装程序已退出", 90, $"退出码: {process.ExitCode}");
                }
                else
                {
                    LogMessage("Process.Start returned null.");
                    return (false, "无法启动安装程序", downloaded, false);
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                LogMessage("User cancelled UAC prompt.");
                ReportProgress(2, "用户取消了安装", 0, "未获得管理员权限");
                return (false, "您取消了管理员权限请求。\n\n请重新点击安装按钮并选择「是」以继续。", downloaded, false);
            }
            catch (Exception ex)
            {
                LogMessage($"Install failed: {ex}");
                return (false, $"安装失败: {ex.Message}", downloaded, false);
            }

            LogMessage("Waiting 5 seconds for driver to install...");
            ReportProgress(3, "正在验证安装...", 0, "正在检测驱动设备...");

            for (int i = 0; i < 5; i++)
            {
                if (_cancelled)
                {
                    LogMessage("Verification cancelled by user.");
                    return (false, "用户取消了验证", downloaded, false);
                }

                if (i > 0)
                {
                    await Task.Delay(1000);
                    ReportProgress(3, $"正在验证安装... {i*20}%", i * 20, $"正在第 {i+1}/5 次检测...");
                }

                if (IsVBCableInstalled())
                {
                    try { Directory.Delete(tempDir, true); LogMessage("Temp dir cleaned up."); } catch { }
                    ReportProgress(3, "安装成功！", 100, "检测到虚拟声卡设备");
                    LogMessage("Installation verified successfully!");
                    return (true, "安装成功", downloaded, true);
                }
            }

            LogMessage("Installation not detected after 5 attempts.");
            var msg = $"安装程序执行完成，但未检测到设备。\n\n可能需要：\n1. 重启电脑后再试\n2. 手动安装：右键 VBCableSetup_x64.exe → 以管理员身份运行\n\n安装包已下载保存在：\n{zipPath}";
            LogMessage(msg);
            ReportProgress(3, "安装未完成", 100, "驱动未检测到，可能需要重启");
            return (false, msg, downloaded, false);
        }
        catch (Exception ex)
        {
            LogMessage($"Fatal error: {ex}");
            return (false, $"发生严重错误: {ex.Message}", false, false);
        }
    }

    private static string? FindSetupExe(string dir)
    {
        var exeFiles = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories);
        LogMessage($"Found {exeFiles.Length} exe files:");
        foreach (var f in exeFiles)
            LogMessage($"  - {f}");

        string? setupExe = null;

        if (Environment.Is64BitOperatingSystem)
        {
            setupExe = exeFiles.FirstOrDefault(f => {
                var name = Path.GetFileName(f);
                return (name.Contains("VBCableSetup", StringComparison.OrdinalIgnoreCase) || 
                        name.Contains("VBCABLE_Setup", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("VBCable_Setup", StringComparison.OrdinalIgnoreCase)) && 
                       name.Contains("x64", StringComparison.OrdinalIgnoreCase);
            });
            if (setupExe != null)
            {
                LogMessage($"Selected x64 VBCableSetup: {setupExe}");
                return setupExe;
            }

            setupExe = exeFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("Setup", StringComparison.OrdinalIgnoreCase) &&
                                                     Path.GetFileName(f).Contains("x64", StringComparison.OrdinalIgnoreCase) &&
                                                     (Path.GetFileName(f).Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                                                      Path.GetFileName(f).Contains("Cable", StringComparison.OrdinalIgnoreCase)));
            if (setupExe != null)
            {
                LogMessage($"Selected x64 VB/Cable Setup: {setupExe}");
                return setupExe;
            }
        }

        setupExe = exeFiles.FirstOrDefault(f => {
            var name = Path.GetFileName(f);
            return name.Contains("VBCableSetup", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("VBCABLE_Setup", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("VBCable_Setup", StringComparison.OrdinalIgnoreCase);
        });
        if (setupExe != null)
        {
            LogMessage($"Selected VBCableSetup: {setupExe}");
            return setupExe;
        }

        setupExe = exeFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("Setup", StringComparison.OrdinalIgnoreCase) &&
                                                 (Path.GetFileName(f).Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                                                  Path.GetFileName(f).Contains("Cable", StringComparison.OrdinalIgnoreCase)));
        if (setupExe != null)
        {
            LogMessage($"Selected VB/Cable Setup: {setupExe}");
            return setupExe;
        }

        if (exeFiles.Length > 0)
        {
            var sorted = exeFiles.OrderByDescending(f => Path.GetFileName(f).Length).ToArray();
            foreach (var f in sorted)
            {
                if (!Path.GetFileName(f).Contains("ControlPanel", StringComparison.OrdinalIgnoreCase))
                {
                    LogMessage($"Selected (first non-ControlPanel): {f}");
                    return f;
                }
            }
            LogMessage($"Falling back to first exe: {sorted[0]}");
            return sorted[0];
        }

        return null;
    }

    private static void LogFiles(string dir)
    {
        LogMessage($"Files in {dir}:");
        try
        {
            foreach (var file in Directory.GetFiles(dir))
                LogMessage($"  {Path.GetFileName(file)}");
            foreach (var subDir in Directory.GetDirectories(dir))
                LogFiles(subDir);
        }
        catch (Exception ex)
        {
            LogMessage($"Error listing files: {ex}");
        }
    }

    public static string GetDownloadUrl()
    {
        return "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip";
    }

    public static string GetLogPath()
    {
        return LogPath;
    }
}
