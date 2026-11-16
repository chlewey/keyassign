using KeyAssign.Core.Keys;
using KeyAssign.Core.Layouts;

namespace S2.ReadLayout;

/// <summary>What probing a layout found, beyond the layout itself.</summary>
internal sealed class ProbeReport
{
    public int LigatureKeys;
    public int TruncatedOutputs;
    public int ChainedDead;
    public int ForeignDeadResults;
    public int UnrepresentableCaps;
    public List<string> Notes { get; } = new();
}

/// <summary>
/// Reads a layout by asking Windows what each key types, which needs no knowledge of the
/// DLL's tables. Dead key tables come from pressing the dead key and then every key that
/// types a single character.
/// </summary>
internal sealed class Prober
{
    private static readonly ushort[] ScanCodes = BuildScanCodes();

    private readonly IntPtr _hkl;

    public Prober(IntPtr hkl) => _hkl = hkl;

    public ProbeReport Report { get; } = new();

    private static ushort[] BuildScanCodes()
    {
        var codes = new List<ushort>();
        codes.AddRange(Enumerable.Range(0x02, 0x0D - 0x02 + 1).Select(i => (ushort)i));
        codes.AddRange(Enumerable.Range(0x10, 0x1B - 0x10 + 1).Select(i => (ushort)i));
        codes.AddRange(Enumerable.Range(0x1E, 0x29 - 0x1E + 1).Select(i => (ushort)i));
        codes.AddRange(Enumerable.Range(0x2B, 0x35 - 0x2B + 1).Select(i => (ushort)i));
        codes.AddRange(new ushort[] { 0x39, 0x56, 0x73, 0x7D });
        return codes.ToArray();
    }

    // One cell of the grid: what a key types at a level, with or without Caps Lock.
    private readonly record struct Probe(string Text, bool Dead);

    private static byte[] State(Level level, bool caps)
    {
        var s = new byte[256];
        if (level is Level.Shift or Level.AltGrShift)
        {
            s[0x10] = 0x80;
            s[0xA0] = 0x80;
        }

        if (level is Level.AltGr or Level.AltGrShift)
        {
            s[0x11] = 0x80;
            s[0xA2] = 0x80;
            s[0x12] = 0x80;
            s[0xA5] = 0x80;
        }

        if (caps)
        {
            s[0x14] = 0x01;
        }

        return s;
    }

    private Probe Press(ushort sc, Level level, bool caps, out int count)
    {
        var vk = Native.MapVirtualKeyExW(sc, Native.MAPVK_VSC_TO_VK_EX, _hkl);
        count = 0;
        if (vk == 0)
        {
            return new Probe("", false);
        }

        var buf = new char[16];
        var n = Native.ToUnicodeEx(vk, sc, State(level, caps), buf, buf.Length, 0, _hkl);
        count = n;
        if (n < 0)
        {
            // A dead key leaves its accent pending. The caller flushes after use.
            return new Probe(buf[0].ToString(), true);
        }

        return new Probe(new string(buf, 0, n), false);
    }

    private void Flush()
    {
        var buf = new char[16];
        // A space with no modifiers consumes any pending dead key. Twice covers a chain.
        for (var i = 0; i < 2; i++)
        {
            Native.ToUnicodeEx(0x20, 0x39, new byte[256], buf, buf.Length, 0, _hkl);
        }
    }

    private Probe Cell(ushort sc, Level level, bool caps = false)
    {
        Flush();
        var p = Press(sc, level, caps, out _);
        Flush();
        return p;
    }

    private KeyOutput ToOutput(Probe p)
    {
        if (p.Text.Length == 0)
        {
            return KeyOutput.None;
        }

        if (p.Dead)
        {
            return KeyOutput.Dead(p.Text[0]);
        }

        var text = p.Text;
        if (text.Length > 1)
        {
            Report.LigatureKeys++;
        }

        if (text.Length > KeyOutput.MaxLength)
        {
            Report.TruncatedOutputs++;
            text = text[..KeyOutput.MaxLength];
        }

        // Control characters (Ctrl+letter results) are not text a layout assigns.
        return text.Any(char.IsControl) ? KeyOutput.None : KeyOutput.Chars(text);
    }

    public Layout Read(string name)
    {
        var keys = new List<KeyBinding>();
        var typeable = new List<(ushort Sc, Level Level, char Char)>();
        var accents = new Dictionary<char, (ushort Sc, Level Level)>();
        var levels = new[] { Level.Normal, Level.Shift, Level.AltGr, Level.AltGrShift };

        foreach (var sc in ScanCodes)
        {
            var cells = levels.Select(l => Cell(sc, l)).ToArray();
            var outs = cells.Select(ToOutput).ToArray();
            if (outs.All(o => o.Kind == OutputKind.None))
            {
                continue;
            }

            for (var i = 0; i < 4; i++)
            {
                if (cells[i].Dead)
                {
                    accents.TryAdd(cells[i].Text[0], (sc, levels[i]));
                }
                else if (cells[i].Text.Length == 1 && !char.IsControl(cells[i].Text[0]))
                {
                    typeable.Add((sc, levels[i], cells[i].Text[0]));
                }
            }

            var caps = ReadCaps(sc, outs);
            keys.Add(new KeyBinding(new ScanCode(sc), outs[0], outs[1], outs[2], outs[3], caps));
        }

        var dead = accents.Select(a => ReadDeadTable(a.Key, a.Value, typeable)).ToList();
        return new Layout(name, LayoutKind.Layout, null, keys, dead);
    }

    private CapsBehavior ReadCaps(ushort sc, KeyOutput[] outs)
    {
        var normalCaps = ToOutput(Cell(sc, Level.Normal, true));
        var altGrCaps = ToOutput(Cell(sc, Level.AltGr, true));
        var inverts = normalCaps == outs[1] && outs[0] != outs[1];
        var altInverts = altGrCaps == outs[3] && outs[2] != outs[3];
        var normalSame = normalCaps == outs[0];
        var altSame = altGrCaps == outs[2];

        if (inverts && altInverts)
        {
            return CapsBehavior.InvertsShiftAndAltGr;
        }

        if (inverts && altSame)
        {
            return CapsBehavior.InvertsShift;
        }

        if (normalSame && altSame)
        {
            return CapsBehavior.Ignored;
        }

        // Caps Lock gives a value the model cannot say (AltGr only, or a third output).
        Report.UnrepresentableCaps++;
        return inverts ? CapsBehavior.InvertsShift : CapsBehavior.Ignored;
    }

    private DeadKey ReadDeadTable(char accent, (ushort Sc, Level Level) via,
        List<(ushort Sc, Level Level, char Char)> typeable)
    {
        var entries = new Dictionary<char, char>();
        foreach (var (sc, level, baseChar) in typeable)
        {
            if (entries.ContainsKey(baseChar))
            {
                continue;
            }

            Flush();
            Press(via.Sc, via.Level, false, out _);
            var second = Press(sc, level, false, out var n);
            Flush();

            if (second.Dead)
            {
                Report.ChainedDead++;
                Report.Notes.Add($"chained: dead '{accent}' then dead '{second.Text}'");
                continue;
            }

            if (n == 1 && second.Text[0] != baseChar)
            {
                entries[baseChar] = second.Text[0];
            }
            else if (n == 2 && second.Text[0] == accent && second.Text[1] == baseChar)
            {
                // No composition: Windows types the accent, then the character.
            }
            else if (n >= 1 && second.Text != baseChar.ToString())
            {
                Report.ForeignDeadResults++;
                Report.Notes.Add($"dead '{accent}' + '{baseChar}' gave {second.Text.Length} chars");
            }
        }

        return new DeadKey(accent, entries.Select(e => new DeadEntry(e.Key, e.Value)).OrderBy(e => e.Base));
    }
}
