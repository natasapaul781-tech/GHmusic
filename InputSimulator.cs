using System.Runtime.InteropServices;

namespace Soundboard;

public static class InputSimulator
{
    private const uint CUSTOM_EXTRA_INFO = 0xDEADBEEF;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MAPVK_VK_TO_VSC = 0;

    public static void KeyDown(Keys key, bool useScanCode = true)
    {
        SendKeyInput(key, false, useScanCode);
    }

    public static void KeyUp(Keys key, bool useScanCode = true)
    {
        SendKeyInput(key, true, useScanCode);
    }

    public static void KeyPress(Keys key, bool useScanCode = true)
    {
        KeyDown(key, useScanCode);
        KeyUp(key, useScanCode);
    }

    public static void KeyDownWithModifiers(Keys key, Keys modifiers, bool useScanCode = true)
    {
        PressModifiersDown(modifiers, useScanCode);
        KeyDown(key, useScanCode);
    }

    public static void KeyUpWithModifiers(Keys key, Keys modifiers, bool useScanCode = true)
    {
        KeyUp(key, useScanCode);
        ReleaseModifiersDown(modifiers, useScanCode);
    }

    private static void PressModifiersDown(Keys modifiers, bool useScanCode)
    {
        if (modifiers.HasFlag(Keys.Control))
            KeyDown(Keys.ControlKey, useScanCode);
        if (modifiers.HasFlag(Keys.Alt))
            KeyDown(Keys.Menu, useScanCode);
        if (modifiers.HasFlag(Keys.Shift))
            KeyDown(Keys.ShiftKey, useScanCode);
    }

    private static void ReleaseModifiersDown(Keys modifiers, bool useScanCode)
    {
        if (modifiers.HasFlag(Keys.Shift))
            KeyUp(Keys.ShiftKey, useScanCode);
        if (modifiers.HasFlag(Keys.Alt))
            KeyUp(Keys.Menu, useScanCode);
        if (modifiers.HasFlag(Keys.Control))
            KeyUp(Keys.ControlKey, useScanCode);
    }

    private static void SendKeyInput(Keys key, bool isKeyUp, bool useScanCode)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT
            {
                dwExtraInfo = (UIntPtr)CUSTOM_EXTRA_INFO,
            },
        };

        if (useScanCode)
        {
            var scanCode = MapVirtualKey((uint)key, MAPVK_VK_TO_VSC);
            input.ki.wScan = (ushort)scanCode;
            input.ki.dwFlags = KEYEVENTF_SCANCODE;
            if (IsExtendedKey(key))
                input.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
        }
        else
        {
            input.ki.wVk = (ushort)key;
        }

        if (isKeyUp)
            input.ki.dwFlags |= KEYEVENTF_KEYUP;

        var done = false;
        var thread = new System.Threading.Thread(() =>
        {
            try { SendInput(1, ref input, Marshal.SizeOf<INPUT>()); }
            catch { }
            done = true;
        });
        thread.IsBackground = true;
        thread.Start();
        thread.Join(200);
        if (!done)
        {
            try { thread.Interrupt(); } catch { }
        }
    }

    private static bool IsExtendedKey(Keys key)
    {
        return key is Keys.ControlKey or Keys.Menu or Keys.Insert or Keys.Delete
            or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
            or Keys.Left or Keys.Right or Keys.Up or Keys.Down
            or Keys.PrintScreen or Keys.Divide or Keys.NumLock
            or Keys.LWin or Keys.RWin or Keys.Apps;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
