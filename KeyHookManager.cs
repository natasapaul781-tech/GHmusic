using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Soundboard;

public enum InputMode { LowLevelHook, SendInput, Hybrid }

public class KeyHookManager : IDisposable
{
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc _hookProc = null!;
    private bool _disposed;
    private bool _initialized;
    private DateTime _lastHookActivity = DateTime.MinValue;
    private readonly object _healthLock = new();
    private IntPtr _rawInputTargetHwnd = IntPtr.Zero;
    private bool _rawInputRegistered;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int LLKHF_INJECTED = 0x10;
    private const int LLKHF_UP = 0x80;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(5);

    private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    private const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RIDEV_REMOVE = 0x00000001;

    public InputMode CurrentMode { get; private set; } = InputMode.LowLevelHook;

#pragma warning disable CS0067
    public event Action<InputMode>? ModeChanged;
#pragma warning restore CS0067
    public event Action<Keys, Keys>? KeyTriggered;

    public void Initialize()
    {
        if (_initialized) return;
        InstallLowLevelHook();
        _initialized = true;
        CurrentMode = InputMode.LowLevelHook;
    }

    public bool IsHookResponsive()
    {
        lock (_healthLock)
        {
            if (_lastHookActivity == DateTime.MinValue)
                return _initialized && DateTime.Now - DateTime.MinValue < HealthTimeout;
            return (DateTime.Now - _lastHookActivity) < HealthTimeout;
        }
    }

    public void Reinstall()
    {
        lock (_healthLock)
        {
            _lastHookActivity = DateTime.MinValue;
        }
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        InstallLowLevelHook();
        _initialized = true;
        CurrentMode = InputMode.LowLevelHook;
    }

    public bool RegisterRawInput(IntPtr hwndTarget)
    {
        if (_rawInputRegistered) return true;
        if (hwndTarget == IntPtr.Zero) return false;

        _rawInputTargetHwnd = hwndTarget;

        var device = new RAWINPUTDEVICE
        {
            usUsagePage = HID_USAGE_PAGE_GENERIC,
            usUsage = HID_USAGE_GENERIC_KEYBOARD,
            dwFlags = RIDEV_INPUTSINK,
            hwndTarget = hwndTarget,
        };

        if (!RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            _rawInputTargetHwnd = IntPtr.Zero;
            return false;
        }

        _rawInputRegistered = true;
        return true;
    }

    public bool UnregisterRawInput()
    {
        if (!_rawInputRegistered || _rawInputTargetHwnd == IntPtr.Zero)
            return false;

        var device = new RAWINPUTDEVICE
        {
            usUsagePage = HID_USAGE_PAGE_GENERIC,
            usUsage = HID_USAGE_GENERIC_KEYBOARD,
            dwFlags = RIDEV_REMOVE,
            hwndTarget = IntPtr.Zero,
        };

        if (!RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            return false;

        _rawInputRegistered = false;
        _rawInputTargetHwnd = IntPtr.Zero;
        return true;
    }

    public static ProcessRawInputResult ProcessRawInput(IntPtr lParam)
    {
        var result = new ProcessRawInputResult();
        var size = 0u;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0) return result;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != size)
                return result;

            var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
            if (raw.header.dwType != RIM_TYPE_KEYBOARD) return result;

            var kb = raw.keyboard;
            result.VirtualKey = (Keys)kb.VKey;
            result.MakeCode = kb.MakeCode;
            result.Flags = kb.Flags;
            result.IsKeyDown = (kb.Flags & RI_KEY_BREAK) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_rawInputRegistered)
            UnregisterRawInput();

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _initialized = false;
    }

    private void InstallLowLevelHook()
    {
        _hookProc = HookCallback;
        var hMod = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName!);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hMod, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            lock (_healthLock)
            {
                _lastHookActivity = DateTime.Now;
            }

            var vkCode = Marshal.ReadInt32(lParam);
            var key = (Keys)vkCode;
            var mods = Keys.None;
            if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) mods |= Keys.Control;
            if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) mods |= Keys.Shift;
            if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0
                || (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0
                || (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0) mods |= Keys.Alt;

            if (key != Keys.Menu && key != Keys.ControlKey && key != Keys.ShiftKey)
            {
                KeyTriggered?.Invoke(key, mods);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(ref RAWINPUTDEVICE pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPE_KEYBOARD = 1;
    private const ushort RI_KEY_BREAK = 0x01;
    private const ushort RI_KEY_E0 = 0x02;
    private const ushort RI_KEY_E1 = 0x04;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWINPUT_KEYBOARD keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT_KEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }
}

public struct ProcessRawInputResult
{
    public Keys VirtualKey;
    public ushort MakeCode;
    public ushort Flags;
    public bool IsKeyDown;
}
