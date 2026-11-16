using System.Text;
using KeyAssign.Core.Layouts;
using KeyAssign.Core.Layouts.Format;
using S2.ReadLayout;

// Spike S2: read installed keyboard layouts by probing, and write them in Key Assign's text format.
//   S2.ReadLayout --list                 the layouts Windows ships (registry, read-only)
//   S2.ReadLayout --current              the layouts in this user's input list
//   S2.ReadLayout <KLID> [<KLID>...]     probe those layouts, e.g. 00000409 0000040A
//   --out <dir>                          where the .kal files go (default: ./out)
Console.OutputEncoding = Encoding.UTF8;
var argList = args.ToList();
var outDir = "out";
var oi = argList.IndexOf("--out");
if (oi >= 0)
{
    outDir = argList[oi + 1];
    argList.RemoveRange(oi, 2);
}

if (argList.Count == 0 || argList[0] == "--list")
{
    foreach (var l in LayoutRegistry.All())
    {
        Console.WriteLine($"{l.Klid}  {l.File,-14} {l.Text}");
    }

    return;
}

var texts = LayoutRegistry.All().ToDictionary(l => l.Klid, l => l.Text, StringComparer.OrdinalIgnoreCase);
var targets = new List<(string Klid, IntPtr Hkl, bool Loaded)>();

if (argList[0] == "--current")
{
    var n = Native.GetKeyboardLayoutList(0, null);
    var list = new IntPtr[n];
    Native.GetKeyboardLayoutList(n, list);
    targets.AddRange(list.Select(h => (LayoutRegistry.KlidOf(h), h, false)));
}
else
{
    foreach (var klid in argList)
    {
        // Loads the layout into this process only; no ACTIVATE, nothing is added to the input list.
        var h = Native.LoadKeyboardLayoutW(klid, Native.KLF_NOTELLSHELL);
        if (h == IntPtr.Zero)
        {
            Console.WriteLine($"{klid}: could not load");
            continue;
        }

        targets.Add((klid, h, true));
    }
}

Directory.CreateDirectory(outDir);
foreach (var (klid, hkl, loaded) in targets)
{
    var name = texts.TryGetValue(klid, out var t) && t.Length > 0 ? t : klid;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var prober = new Prober(hkl);
    var layout = prober.Read(name);
    watch.Stop();

    var text = LayoutFormat.Write(layout);
    File.WriteAllText(Path.Combine(outDir, klid + ".kal"), text, new UTF8Encoding(false));
    var back = LayoutFormat.Parse(text);
    var roundTrip = back.Errors.Count == 0 && back.Layout!.Equals(layout);

    var r = prober.Report;
    var entries = layout.DeadKeys.Sum(d => d.Entries.Count);
    var inverting = layout.Keys.Count(k => k.Caps != CapsBehavior.Ignored);
    Console.WriteLine(
        $"{klid} {name}: {layout.Keys.Count} keys, {layout.DeadKeys.Count} dead ({entries} entries), " +
        $"{inverting} caps-inverting, ligatures {r.LigatureKeys}, truncated {r.TruncatedOutputs}, chained {r.ChainedDead}, " +
        $"odd {r.ForeignDeadResults}, caps-unrep {r.UnrepresentableCaps}, " +
        $"{watch.ElapsedMilliseconds} ms, round-trip {(roundTrip ? "ok" : "FAILED")}");
    foreach (var note in r.Notes.Distinct().Take(5))
    {
        Console.WriteLine("    " + note);
    }

    if (loaded)
    {
        Native.UnloadKeyboardLayout(hkl);
    }
}
