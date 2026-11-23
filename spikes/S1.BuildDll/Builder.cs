using KeyAssign.Core.Keys;
using KeyAssign.Core.Layouts;

namespace S1.BuildDll;

/// <summary>
/// Turns a <see cref="Layout"/> into KBDTABLES data. Everything the model does not own
/// (Tab, Enter, the keypad, function keys, key names) comes from a template, here Windows'
/// own kbdus.dll read at run time; the real product would embed those as constants.
/// </summary>
internal static class Builder
{
    private const int Columns = 5;      // Normal, Shift, Ctrl, AltGr, AltGr+Shift
    private static readonly byte[] CharScanCodes = MainBlock();

    private static byte[] MainBlock()
    {
        var codes = new List<byte>();
        codes.AddRange(Enumerable.Range(0x02, 0x0D - 0x02 + 1).Select(i => (byte)i));
        codes.AddRange(Enumerable.Range(0x10, 0x1B - 0x10 + 1).Select(i => (byte)i));
        codes.AddRange(Enumerable.Range(0x1E, 0x29 - 0x1E + 1).Select(i => (byte)i));
        codes.AddRange(Enumerable.Range(0x2B, 0x35 - 0x2B + 1).Select(i => (byte)i));
        codes.AddRange(new byte[] { 0x39, 0x56, 0x73, 0x7D });
        return codes.ToArray();
    }

    public static IReadOnlyList<byte> MainBlockCodes => CharScanCodes;

    public sealed record Result(RawTables Tables, List<string> Warnings);

    public static Result Build(Layout layout, RawTables template)
    {
        var warnings = new List<string>();
        var t = new RawTables
        {
            VkToBit = new() { (RawTables.VkShift, 1), (RawTables.VkControl, 2), (RawTables.VkMenu, 4) },

            // bits: 0 none, 1 shift, 2 ctrl, 3 shift+ctrl, 4 alt, 5 shift+alt, 6 ctrl+alt (AltGr), 7 shift+ctrl+alt
            ModNumber = new byte[] { 0, 1, 2, 2, RawTables.ShftInvalid, RawTables.ShftInvalid, 3, 4 },
            KeyNames = template.KeyNames,
            KeyNamesExt = template.KeyNamesExt,
            ScanToVkE0 = template.ScanToVkE0,
            ScanToVkE1 = template.ScanToVkE1,
            KeyboardType = 4,
            KeyboardSubType = 0,
        };

        var vks = AssignVirtualKeys(layout, template, warnings);
        t.ScanToVk = (ushort[])template.ScanToVk.Clone();
        foreach (var (sc, vk) in vks)
        {
            if (sc.Value >= t.ScanToVk.Length)
            {
                warnings.Add($"scan code {sc} is beyond the table");
                continue;
            }

            t.ScanToVk[sc.Value] = vk;
        }

        // A main-block scan code the layout leaves out types nothing: it must not keep the
        // template's VK, which another key of the layout may now own.
        foreach (var sc in CharScanCodes.Where(c => c != 0x39 && c < t.ScanToVk.Length && !vks.Keys.Any(k => k.Value == c)))
        {
            t.ScanToVk[sc] = 0;
        }

        // Rows kept from the template: every virtual key that no main-block scan code owns.
        var ownedByChars = CharScanCodes.Where(sc => sc != 0x39 || layout.FindKey(new ScanCode(0x39)) is not null)
            .Select(sc => (byte)(template.ScanToVk[sc] & 0xFF)).ToHashSet();
        var rows = new List<VkRow>();
        foreach (var row in template.VkToWchar.SelectMany(x => x.Rows))
        {
            if (ownedByChars.Contains(row.Vk) || vks.Values.Contains(row.Vk))
            {
                continue;
            }

            // The template has Normal, Shift, Ctrl (and Shift+Ctrl): our columns 0 to 2.
            var chars = new ushort[Columns];
            Array.Fill(chars, RawTables.WchNone);
            for (var i = 0; i < Math.Min(3, row.Chars.Length); i++)
            {
                chars[i] = row.Chars[i];
            }

            rows.Add(new VkRow(row.Vk, row.Attributes, chars));
        }

        var ligatures = new List<LigatureRow>();
        var usesAltGr = false;
        foreach (var key in layout.Keys)
        {
            if (!vks.TryGetValue(key.Code, out var vk))
            {
                continue;
            }

            var outputs = new[] { key.Normal, key.Shift, key.AltGr, key.AltGrShift };
            var chars = new ushort[Columns];
            var deadAccents = new ushort[Columns];
            Array.Fill(chars, RawTables.WchNone);
            var col = new[] { 0, 1, 3, 4 };
            for (var i = 0; i < 4; i++)
            {
                var o = outputs[i];
                switch (o.Kind)
                {
                    case OutputKind.None:
                        break;
                    case OutputKind.Dead:
                        chars[col[i]] = RawTables.WchDead;
                        deadAccents[col[i]] = o.Text[0];
                        break;
                    default:
                        if (o.Text.Length == 1)
                        {
                            chars[col[i]] = o.Text[0];
                        }
                        else
                        {
                            chars[col[i]] = RawTables.WchLgtr;
                            ligatures.Add(new LigatureRow(vk, (ushort)col[i], o.Text.Select(c => (ushort)c).ToArray()));
                        }

                        break;
                }

                usesAltGr |= i >= 2 && o.Kind != OutputKind.None;
            }

            chars[2] = ControlChar(vk);
            byte attr = key.Caps switch
            {
                CapsBehavior.InvertsShift => RawTables.CapLok,
                CapsBehavior.InvertsShiftAndAltGr => (byte)(RawTables.CapLok | RawTables.CapLokAltGr),
                _ => 0,
            };
            rows.Add(new VkRow(vk, attr, chars));
            if (deadAccents.Any(a => a != 0))
            {
                // A dead character is followed by a VK__none_ row that carries the accents.
                var accents = deadAccents.Select(a => a == 0 ? RawTables.WchNone : a).ToArray();
                rows.Add(new VkRow(0xFF, 0, accents));
            }
        }

        t.VkToWchar = new() { new VkTable(Columns, rows) };

        foreach (var dead in layout.DeadKeys)
        {
            foreach (var e in dead.Entries)
            {
                t.Dead.Add(new DeadRow(e.Base, dead.Accent, e.Result, 0));
            }

            t.KeyNamesDead.Add($"{dead.Accent}DEAD KEY U+{(int)dead.Accent:X4}");
        }

        t.Ligatures = ligatures;
        t.LigatureMax = ligatures.Count == 0 ? (byte)0 : (byte)Math.Max(2, ligatures.Max(l => l.Chars.Length));
        t.Ligatures = ligatures.Select(l => l with { Chars = Pad(l.Chars, t.LigatureMax) }).ToList();
        t.LocaleFlags = (RawTables.KbdVersion1 << 16) | (usesAltGr ? RawTables.KllfAltGr : 0);
        return new Result(t, warnings);
    }

    private static ushort[] Pad(ushort[] chars, int n)
    {
        var padded = new ushort[n];
        chars.CopyTo(padded, 0);
        return padded;
    }

    private static ushort ControlChar(byte vk) => vk switch
    {
        >= 0x41 and <= 0x5A => (ushort)(vk - 0x40),
        0xDB => 0x1B,
        0xDC or 0xE2 => 0x1C,
        0xDD => 0x1D,
        0x20 => 0x20,
        _ => RawTables.WchNone,
    };

    // Windows keys its character tables by virtual key, so each key needs its own. A Latin
    // letter gets its letter's VK (so Ctrl+A and shortcuts follow the letter, as on AZERTY);
    // other keys keep the US position's VK, or a free OEM VK if a letter took theirs.
    private static Dictionary<ScanCode, byte> AssignVirtualKeys(Layout layout, RawTables template, List<string> warnings)
    {
        var result = new Dictionary<ScanCode, byte>();
        var taken = new HashSet<byte>();

        foreach (var key in layout.Keys)
        {
            if (key.Normal.Kind == OutputKind.Text && key.Normal.Text.Length == 1)
            {
                var c = char.ToUpperInvariant(key.Normal.Text[0]);
                if (c is >= 'A' and <= 'Z' && key.Normal.Text[0] is >= 'a' and <= 'z' && taken.Add((byte)c))
                {
                    result[key.Code] = (byte)c;
                }
            }
        }

        // Keys that keep their positional VK first, so a displaced key never steals one.
        var pending = new List<KeyBinding>();
        foreach (var key in layout.Keys.Where(k => !result.ContainsKey(k.Code)))
        {
            var positional = key.Code.Value < template.ScanToVk.Length ? (byte)(template.ScanToVk[key.Code.Value] & 0xFF) : (byte)0;
            if (positional != 0 && taken.Add(positional))
            {
                result[key.Code] = positional;
            }
            else
            {
                pending.Add(key);
            }
        }

        // The others take any unused VK: OEM keys first, then digits, then unused letters.
        var pool = new Queue<byte>(new byte[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE2 }
            .Concat(Enumerable.Range('0', 10).Select(c => (byte)c))
            .Concat(Enumerable.Range('A', 26).Select(c => (byte)c))
            .Where(v => !taken.Contains(v)));
        foreach (var key in pending)
        {
            if (pool.Count == 0)
            {
                warnings.Add($"key {key.Code} has no free virtual key and was left out");
                continue;
            }

            var v = pool.Dequeue();
            taken.Add(v);
            result[key.Code] = v;
            warnings.Add($"key {key.Code} got VK {v:X2}: its positional VK was taken");
        }

        return result;
    }
}
