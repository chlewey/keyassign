using System.Runtime.InteropServices;

namespace S2.ReadLayout;

/// <summary>Selects and probes layouts already loaded by Windows.</summary>
internal static class Native
{
    public const uint MAPVK_VSC_TO_VK_EX = 3;

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetKeyboardLayoutNameW([Out] char[] name);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ToUnicodeEx(uint vk, uint scanCode, byte[] keyState,
        [Out] char[] buffer, int bufferSize, uint flags, IntPtr hkl);

    [DllImport("user32.dll")]
    public static extern int GetKeyboardLayoutList(int size, [Out] IntPtr[]? list);
}
