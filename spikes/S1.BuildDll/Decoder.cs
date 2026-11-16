using KeyAssign.Core.Keys;
using KeyAssign.Core.Layouts;

namespace S1.BuildDll;

/// <summary>Reads the model back out of KBDTABLES, the way Windows resolves a key: scan code, VK, column, character.</summary>
internal static class Decoder
{
    private static readonly byte[] Scans = Enumerable.Range(0x02, 0x7E).Select(i => (byte)i).ToArray();

    public static Layout Decode(RawTables t, string name)
    {
        var keys = new List<KeyBinding>();
        foreach (var sc in Scans)
        {
            if (sc >= t.ScanToVk.Length)
            {
                break;
            }

            var vk = (byte)(t.ScanToVk[sc] & 0xFF);
            if (vk == 0)
            {
                continue;
            }

            var (table, index) = Find(t, vk);
            if (table is null)
            {
                continue;
            }

            var row = table.Rows[index];
            KeyOutput At(int bits)
            {
                if (bits >= t.ModNumber.Length || t.ModNumber[bits] == RawTables.ShftInvalid)
                {
                    return KeyOutput.None;
                }

                var col = t.ModNumber[bits];
                if (col >= table.Columns)
                {
                    return KeyOutput.None;
                }

                var ch = row.Chars[col];
                if (ch == RawTables.WchDead)
                {
                    var next = index + 1 < table.Rows.Count ? table.Rows[index + 1] : null;
                    return next is { Vk: 0xFF } && next.Chars[col] != RawTables.WchNone
                        ? KeyOutput.Dead((char)next.Chars[col])
                        : KeyOutput.None;
                }

                if (ch == RawTables.WchLgtr)
                {
                    var lig = t.Ligatures.FirstOrDefault(l => l.Vk == vk && l.Column == col);
                    return lig is null ? KeyOutput.None
                        : KeyOutput.Chars(new string(lig.Chars.Where(c => c != 0 && c != RawTables.WchNone).Select(c => (char)c).ToArray()));
                }

                // WCH_NONE, and the control characters of the Ctrl column, are not outputs.
                return ch == RawTables.WchNone || ch < 0x20 && ch != 0 ? KeyOutput.None : ch == 0 ? KeyOutput.None : KeyOutput.Chars(((char)ch).ToString());
            }

            var outs = new[] { At(0), At(1), At(6), At(7) };
            if (outs.All(o => o.Kind == OutputKind.None))
            {
                continue;
            }

            var caps = (row.Attributes & RawTables.CapLok) == 0 ? CapsBehavior.Ignored
                : (row.Attributes & RawTables.CapLokAltGr) != 0 ? CapsBehavior.InvertsShiftAndAltGr
                : CapsBehavior.InvertsShift;
            keys.Add(new KeyBinding(new ScanCode(sc), outs[0], outs[1], outs[2], outs[3], caps));
        }

        var dead = t.Dead.Where(d => (d.Flags & 1) == 0).GroupBy(d => d.Accent)
            .Select(g => new DeadKey(g.Key, g.GroupBy(d => d.Base).Select(e => new DeadEntry(e.Key, e.First().Composed)))).ToList();

        // A dead key with an empty table has no DEADKEY rows; its accent is only on the key.
        foreach (var accent in keys.SelectMany(k => new[] { k.Normal, k.Shift, k.AltGr, k.AltGrShift })
                     .Where(o => o.Kind == OutputKind.Dead).Select(o => o.Accent).Distinct())
        {
            if (dead.All(d => d.Accent != accent))
            {
                dead.Add(new DeadKey(accent, Array.Empty<DeadEntry>()));
            }
        }

        return new Layout(name, LayoutKind.Layout, null, keys, dead);
    }

    private static (VkTable?, int) Find(RawTables t, byte vk)
    {
        foreach (var table in t.VkToWchar)
        {
            var i = table.Rows.FindIndex(r => r.Vk == vk);
            if (i >= 0)
            {
                return (table, i);
            }
        }

        return (null, -1);
    }
}
