using System.Text;

namespace S1.BuildDll;

/// <summary>A canonical text of the tables, to compare two DLLs (or a DLL and what was meant to be written).</summary>
internal static class Canon
{
    public static string Of(RawTables t)
    {
        var s = new StringBuilder();
        s.AppendLine("bits " + string.Join(" ", t.VkToBit.Select(b => $"{b.Vk:X2}:{b.Bits}")));
        s.AppendLine("mod " + string.Join(",", t.ModNumber));
        foreach (var table in t.VkToWchar)
        {
            s.AppendLine($"table {table.Columns}");
            foreach (var r in table.Rows)
            {
                s.AppendLine($"  {r.Vk:X2} {r.Attributes:X2} " + string.Join(" ", r.Chars.Select(c => c.ToString("X4"))));
            }
        }

        foreach (var d in t.Dead)
        {
            s.AppendLine($"dead {d.Accent:X4} {d.Base:X4} {d.Composed:X4} {d.Flags:X4}");
        }

        s.AppendLine("names " + string.Join("|", t.KeyNames.Select(n => $"{n.Scan:X2}={n.Name}")));
        s.AppendLine("ext " + string.Join("|", t.KeyNamesExt.Select(n => $"{n.Scan:X2}={n.Name}")));
        s.AppendLine("deadnames " + string.Join("|", t.KeyNamesDead));
        s.AppendLine("vsc " + string.Join(" ", t.ScanToVk.Select(v => v.ToString("X4"))));
        s.AppendLine("e0 " + string.Join(" ", t.ScanToVkE0.Select(v => $"{v.Scan:X2}:{v.Vk:X4}")));
        s.AppendLine("e1 " + string.Join(" ", t.ScanToVkE1.Select(v => $"{v.Scan:X2}:{v.Vk:X4}")));
        s.AppendLine($"flags {t.LocaleFlags:X8} lg {t.LigatureMax} type {t.KeyboardType}/{t.KeyboardSubType}");
        foreach (var l in t.Ligatures)
        {
            s.AppendLine($"lig {l.Vk:X2} {l.Column} " + string.Join(" ", l.Chars.Select(c => c.ToString("X4"))));
        }

        return s.ToString();
    }
}
