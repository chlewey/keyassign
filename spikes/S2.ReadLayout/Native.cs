using System.Runtime.InteropServices;

namespace S2.ReadLayout;

/// <summary>The few user32 calls the spike needs. None of them changes the system's input settings.</summary>
internal static class Native
{
    public const uint KLF_NOTELLSHELL = 0x80;
    public const uint MAPVK_VSC_TO_VK_EX = 3;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadKeyboardLayoutW(string klid, uint flags);

    [DllImport("user32.dll")]
    public static extern bool UnloadKeyboardLayout(IntPtr hkl);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ToUnicodeEx(uint vk, uint scanCode, byte[] keyState,
        [Out] char[] buffer, int bufferSize, uint flags, IntPtr hkl);

    [DllImport("user32.dll")]
    public static extern int GetKeyboardLayoutList(int size, [Out] IntPtr[]? list);
}
