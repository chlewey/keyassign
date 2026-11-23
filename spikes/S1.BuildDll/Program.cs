using KeyAssign.Core.Keys;
using KeyAssign.Core.Layouts;
using KeyAssign.Core.Layouts.Format;
using S1.BuildDll;

var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
var outDir = Path.Combine(Path.GetTempPath(), "keyassign-s1");
Directory.CreateDirectory(outDir);

if (args.Length == 2 && args[0] == "dump")
{
    Console.Write(Canon.Of(Reader.Read(args[1])));
    return;
}

if (args.Length == 1 && args[0] == "reemit")
{
    // Step 1: write Windows' own kbdus tables back out as a new DLL and read that.
    var original = Reader.Read(Path.Combine(system, "kbdus.dll"));
    var path = Path.Combine(outDir, "kbdreemit.dll");
    File.WriteAllBytes(path, DllWriter.Build(original));
    var again = Reader.Read(path);
    var same = Canon.Of(original) == Canon.Of(again);
    Console.WriteLine($"re-emitted kbdus.dll ({new FileInfo(path).Length} bytes): tables {(same ? "identical" : "DIFFER")}");
}

if (args.Length >= 2 && args[0] == "roundtrip")
{
    // Model -> KBDTABLES -> DLL -> LoadLibrary -> KBDTABLES -> model, for every .kal given.
    var template = Reader.Read(Path.Combine(system, "kbdus.dll"));
    var files = args.Skip(1).SelectMany(a => Directory.Exists(a) ? Directory.GetFiles(a, "*.kal") : new[] { a }).Order().ToList();
    int ok = 0, bad = 0, warned = 0;
    foreach (var file in files)
    {
        var parsed = LayoutFormat.Parse(File.ReadAllText(file));
        if (parsed.Layout is null)
        {
            Console.WriteLine($"{Path.GetFileName(file)}: does not parse");
            bad++;
            continue;
        }

        var layout = parsed.Layout;
        var built = Builder.Build(layout, template);
        var path = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".dll");
        File.WriteAllBytes(path, DllWriter.Build(built.Tables));
        var back = Decoder.Decode(Reader.Read(path), layout.Name);
        var want = LayoutFormat.Write(Normalize(layout));
        var mainBlock = Builder.MainBlockCodes.Where(c => c != 0x39 || layout.FindKey(new ScanCode(0x39)) is not null).ToHashSet();
        back = new Layout(back.Name, back.Kind, null, back.Keys.Where(k => mainBlock.Contains((byte)k.Code.Value)), back.DeadKeys);
        var got = LayoutFormat.Write(Normalize(back));
        if (built.Warnings.Count > 0)
        {
            warned++;
        }

        if (want == got)
        {
            ok++;
            continue;
        }

        bad++;
        Console.WriteLine($"{Path.GetFileName(file)}: DIFFERS"); foreach (var w in built.Warnings) { Console.WriteLine("   warning: " + w); }
        var a = want.Split((char)10);
        var b = got.Split((char)10);
        foreach (var line in a.Except(b).Take(40))
        {
            Console.WriteLine("   want " + line.TrimEnd());
        }

        foreach (var line in b.Except(a).Take(40))
        {
            Console.WriteLine("   got  " + line.TrimEnd());
        }
    }

    Console.WriteLine($"{files.Count} layouts: {ok} round-trip exactly, {bad} differ, {warned} with warnings");
}

static Layout Normalize(Layout l) => new(l.Name, LayoutKind.Layout, null, l.Keys.OrderBy(k => k.Code.Value),
    l.DeadKeys.OrderBy(d => d.Accent).Select(d => new DeadKey(d.Accent, d.Entries.OrderBy(e => e.Base))));

if (args.Length == 2 && args[0] == "compare-system")
{
    // Decode Windows' own layout DLLs and compare with what S2 read through ToUnicodeEx.
    using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Keyboard Layouts")!;
    int same = 0, diff = 0, skipped = 0;
    var kinds = new Dictionary<string, int>();
    foreach (var klid in root.GetSubKeyNames().Order())
    {
        using var key = root.OpenSubKey(klid)!;
        var dll = Path.Combine(system, key.GetValue("Layout File") as string ?? "");
        var probe = Path.Combine(args[1], klid + ".kal");
        if (!File.Exists(dll) || !File.Exists(probe))
        {
            skipped++;
            continue;
        }

        var want = LayoutFormat.Parse(File.ReadAllText(probe)).Layout!;
        var got = Decoder.Decode(Reader.Read(dll), want.Name);
        var codes = Builder.MainBlockCodes.ToHashSet();
        got = new Layout(got.Name, got.Kind, null, got.Keys.Where(k => codes.Contains((byte)k.Code.Value) && k.Code.Value is not 0x7D), got.DeadKeys);
        // Caps Lock cannot be seen on a key whose Normal and Shift outputs are the same.
        KeyBinding Seen(KeyBinding k)
        {
            if (k.Normal == k.Shift && k.AltGr == k.AltGrShift)
            {
                return k with { Caps = CapsBehavior.Ignored };
            }

            return k.AltGr == k.AltGrShift && k.Caps == CapsBehavior.InvertsShiftAndAltGr ? k with { Caps = CapsBehavior.InvertsShift } : k;
        }
        got = new Layout(got.Name, got.Kind, null, got.Keys.Select(Seen), got.DeadKeys);
        want = new Layout(want.Name, want.Kind, null, want.Keys.Where(k => k.Code.Value is not 0x7D).Select(Seen), want.DeadKeys);
        var a = LayoutFormat.Write(Normalize(want)).Split((char)10);
        var b = LayoutFormat.Write(Normalize(got)).Split((char)10);
        var extra = a.Except(b).Concat(b.Except(a)).Where(l => !l.StartsWith("#") && l.Trim().Length > 0).ToList();
        if (extra.Count == 0)
        {
            same++;
            continue;
        }

        diff++;
        Console.WriteLine($"{klid} {want.Name}: {extra.Count} differing lines");
        foreach (var l in a.Except(b).Take(2))
        {
            Console.WriteLine("   probe " + l.TrimEnd());
        }

        foreach (var l in b.Except(a).Take(2))
        {
            Console.WriteLine("   table " + l.TrimEnd());
        }
    }

    Console.WriteLine($"compared {same + diff} layouts: {same} identical, {diff} differ, {skipped} skipped");
}
