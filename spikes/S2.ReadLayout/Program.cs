using System.Text;
using KeyAssign.Core.Layouts;
using KeyAssign.Core.Layouts.Format;
using S2.ReadLayout;

// Spike S2: read installed keyboard layouts by probing, and write them in Key Assign's text format.
//   S2.ReadLayout --list                 the layouts Windows ships (registry, read-only)
//   S2.ReadLayout --active               the foreground window's active layout
//   S2.ReadLayout --current              the layouts in this user's input list
//   S2.ReadLayout <KLID> [<KLID>...]     probe those layouts if already loaded
//   --out <dir>                          where the .kal files go (default: ./out)
Console.OutputEncoding = Encoding.UTF8;
var argList = args.ToList();
var outDir = "out";
var oi = argList.IndexOf("--out");
if (oi >= 0)
{
    if (oi + 1 == argList.Count || argList[oi + 1].StartsWith("--"))
    {
        return Usage();
    }

    outDir = argList[oi + 1];
    argList.RemoveRange(oi, 2);
}

if (argList.Count == 0 || argList is ["--list"])
{
    foreach (var l in LayoutRegistry.All())
    {
        Console.WriteLine($"{l.Klid}  {l.File,-14} {l.Text}");
    }

    return 0;
}

var active = argList is ["--active"];
var current = argList is ["--current"];
if (!active && !current && argList.Any(id => id.Length != 8 || !id.All(Uri.IsHexDigit)))
{
    return Usage();
}

var targets = new List<(string Klid, IntPtr Hkl)>();
if (active)
{
    var window = Native.GetForegroundWindow();
    var thread = window == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(window, IntPtr.Zero);
    var hkl = Native.GetKeyboardLayout(thread);
    if (hkl != IntPtr.Zero)
    {
        targets.Add((LayoutRegistry.KlidOf(hkl), hkl));
    }
}
else
{
    var handles = new IntPtr[Native.GetKeyboardLayoutList(0, null)];
    var read = handles.Length == 0 ? 0 : Native.GetKeyboardLayoutList(handles.Length, handles);
    targets.AddRange(handles.Take(read).Select(h => (LayoutRegistry.KlidOf(h), h)));
}

if (targets.Count == 0)
{
    Console.Error.WriteLine("Windows did not report any keyboard layouts for this selection.");
    return 1;
}

var failed = false;
if (!active && !current)
{
    var loaded = targets.ToLookup(target => target.Klid, StringComparer.OrdinalIgnoreCase);
    targets.Clear();
    foreach (var klid in argList.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (!loaded.Contains(klid))
        {
            Console.Error.WriteLine($"{klid}: Windows has not loaded this layout in the session.");
            failed = true;
            continue;
        }

        targets.AddRange(loaded[klid]);
    }
}

var texts = LayoutRegistry.All().ToDictionary(l => l.Klid, l => l.Text, StringComparer.OrdinalIgnoreCase);
foreach (var (klid, hkl) in targets)
{
    var name = texts.TryGetValue(klid, out var t) && t.Length > 0 ? t : klid;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var prober = new Prober(hkl);
    var layout = prober.Read(name);
    watch.Stop();

    var text = LayoutFormat.Write(layout);
    var back = LayoutFormat.Parse(text);
    var roundTrip = back.Errors.Count == 0 && back.Layout!.Equals(layout);
    failed |= !roundTrip;
    if (roundTrip)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, klid + ".kal"), text, new UTF8Encoding(false));
    }

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
}

return failed ? 1 : 0;

static int Usage()
{
    Console.Error.WriteLine("Usage: S2.ReadLayout [--list | --active | --current | KLID [KLID ...]] [--out directory]");
    return 2;
}
