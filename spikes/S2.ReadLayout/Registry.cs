using Microsoft.Win32;

namespace S2.ReadLayout;

internal sealed record InstalledLayout(string Klid, string Text, string File);

/// <summary>Read-only look at HKLM\...\Keyboard Layouts, the list of layouts Windows ships.</summary>
internal static class LayoutRegistry
{
    private const string Path = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

    public static IReadOnlyList<InstalledLayout> All()
    {
        using var root = Registry.LocalMachine.OpenSubKey(Path)!;
        return root.GetSubKeyNames().Order().Select(klid =>
        {
            using var key = root.OpenSubKey(klid)!;
            return new InstalledLayout(klid, key.GetValue("Layout Text") as string ?? "", key.GetValue("Layout File") as string ?? "");
        }).ToList();
    }

    /// <summary>The KLID behind an HKL from the user's input list; high word 0xFxxx is a "Layout Id".</summary>
    public static string KlidOf(IntPtr hkl)
    {
        var value = (uint)hkl.ToInt64();
        var lang = value & 0xFFFF;
        var hi = value >> 16;
        if ((hi & 0xF000) != 0xF000)
        {
            return hi == lang ? $"0000{lang:X4}" : $"{hi:X4}{lang:X4}";
        }

        var id = (hi & 0x0FFF).ToString("X4");
        using var root = Registry.LocalMachine.OpenSubKey(Path)!;
        foreach (var klid in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(klid)!;
            if (string.Equals(key.GetValue("Layout Id") as string, id, StringComparison.OrdinalIgnoreCase)
                && klid.EndsWith(lang.ToString("X4"), StringComparison.OrdinalIgnoreCase))
            {
                return klid;
            }
        }

        return $"0000{lang:X4}";
    }
}
