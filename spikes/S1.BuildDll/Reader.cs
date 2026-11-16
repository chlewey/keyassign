using System.Runtime.InteropServices;

namespace S1.BuildDll;

/// <summary>
/// Loads a keyboard layout DLL into this process (read-only: it is not registered or
/// activated) and walks the KBDTABLES that <c>KbdLayerDescriptor</c> returns.
/// </summary>
internal static class Reader
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string path);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr Descriptor();

    public static RawTables Read(string dllPath)
    {
        var module = LoadLibraryW(dllPath);
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException($"LoadLibrary failed for {dllPath}: error {Marshal.GetLastWin32Error()}");
        }

        try
        {
            var proc = GetProcAddress(module, "KbdLayerDescriptor");
            if (proc == IntPtr.Zero)
            {
                throw new InvalidOperationException("No KbdLayerDescriptor export.");
            }

            var tables = Marshal.GetDelegateForFunctionPointer<Descriptor>(proc)();
            return Walk(tables);
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private static RawTables Walk(IntPtr t)
    {
        var raw = new RawTables();

        // MODIFIERS
        var mods = Marshal.ReadIntPtr(t, 0);
        var vkToBit = Marshal.ReadIntPtr(mods, 0);
        for (var p = vkToBit; Marshal.ReadByte(p) != 0; p += 2)
        {
            raw.VkToBit.Add((Marshal.ReadByte(p), Marshal.ReadByte(p, 1)));
        }

        var maxBits = Marshal.ReadInt16(mods, 8);
        raw.ModNumber = Enumerable.Range(0, maxBits + 1).Select(i => Marshal.ReadByte(mods, 10 + i)).ToArray();

        // VK_TO_WCHAR_TABLE[]
        for (var tp = Marshal.ReadIntPtr(t, 8); Marshal.ReadIntPtr(tp, 0) != IntPtr.Zero; tp += 16)
        {
            var rows = Marshal.ReadIntPtr(tp, 0);
            int columns = Marshal.ReadByte(tp, 8);
            int size = Marshal.ReadByte(tp, 9);
            var table = new VkTable((byte)columns, new List<VkRow>());
            for (var rp = rows; Marshal.ReadByte(rp) != 0; rp += size)
            {
                var chars = new ushort[columns];
                for (var c = 0; c < columns; c++)
                {
                    chars[c] = (ushort)Marshal.ReadInt16(rp, 2 + (2 * c));
                }

                table.Rows.Add(new VkRow(Marshal.ReadByte(rp), Marshal.ReadByte(rp, 1), chars));
            }

            raw.VkToWchar.Add(table);
        }

        // DEADKEY[]
        var dead = Marshal.ReadIntPtr(t, 16);
        if (dead != IntPtr.Zero)
        {
            for (var p = dead; Marshal.ReadInt32(p) != 0; p += 8)
            {
                var both = (uint)Marshal.ReadInt32(p);
                raw.Dead.Add(new DeadRow((char)(both & 0xFFFF), (char)(both >> 16),
                    (char)Marshal.ReadInt16(p, 4), (ushort)Marshal.ReadInt16(p, 6)));
            }
        }

        raw.KeyNames = ReadNames(Marshal.ReadIntPtr(t, 24));
        raw.KeyNamesExt = ReadNames(Marshal.ReadIntPtr(t, 32));

        var deadNames = Marshal.ReadIntPtr(t, 40);
        if (deadNames != IntPtr.Zero)
        {
            for (var p = deadNames; Marshal.ReadIntPtr(p) != IntPtr.Zero; p += 8)
            {
                raw.KeyNamesDead.Add(Marshal.PtrToStringUni(Marshal.ReadIntPtr(p))!);
            }
        }

        // Scan codes to virtual keys
        int maxVsc = Marshal.ReadByte(t, 56);
        var scanToVk = Marshal.ReadIntPtr(t, 48);
        raw.ScanToVk = Enumerable.Range(0, maxVsc).Select(i => (ushort)Marshal.ReadInt16(scanToVk, 2 * i)).ToArray();
        raw.ScanToVkE0 = ReadVscVk(Marshal.ReadIntPtr(t, 64));
        raw.ScanToVkE1 = ReadVscVk(Marshal.ReadIntPtr(t, 72));

        raw.LocaleFlags = (uint)Marshal.ReadInt32(t, 80);
        raw.LigatureMax = Marshal.ReadByte(t, 84);
        int ligSize = Marshal.ReadByte(t, 85);
        var lig = Marshal.ReadIntPtr(t, 88);
        if (lig != IntPtr.Zero && ligSize > 0)
        {
            for (var p = lig; Marshal.ReadByte(p) != 0; p += ligSize)
            {
                var chars = Enumerable.Range(0, raw.LigatureMax).Select(i => (ushort)Marshal.ReadInt16(p, 4 + (2 * i))).ToArray();
                raw.Ligatures.Add(new LigatureRow(Marshal.ReadByte(p), (ushort)Marshal.ReadInt16(p, 2), chars));
            }
        }

        raw.KeyboardType = (uint)Marshal.ReadInt32(t, 96);
        raw.KeyboardSubType = (uint)Marshal.ReadInt32(t, 100);
        return raw;
    }

    private static List<NamedKey> ReadNames(IntPtr p)
    {
        var list = new List<NamedKey>();
        if (p == IntPtr.Zero)
        {
            return list;
        }

        for (; Marshal.ReadIntPtr(p, 8) != IntPtr.Zero; p += 16)
        {
            list.Add(new NamedKey(Marshal.ReadByte(p), Marshal.PtrToStringUni(Marshal.ReadIntPtr(p, 8))!));
        }

        return list;
    }

    private static List<(byte, ushort)> ReadVscVk(IntPtr p)
    {
        var list = new List<(byte, ushort)>();
        if (p == IntPtr.Zero)
        {
            return list;
        }

        for (; Marshal.ReadByte(p) != 0; p += 4)
        {
            list.Add((Marshal.ReadByte(p), (ushort)Marshal.ReadInt16(p, 2)));
        }

        return list;
    }
}
