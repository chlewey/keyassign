using System.Text;

namespace KeyAssign.Core.Layouts.Format;

/// <summary>The layout text format: read a file into a <see cref="Layout"/> and write one back.</summary>
public static class LayoutFormat
{
    private static readonly string[] ColumnLabels = ["#", "scan", "normal", "shift", "altgr", "altgr+shift", "caps"];

    public static ParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LayoutParser().Run(text);
    }

    /// <summary>
    /// Writes the layout in a fixed form: one key per line with aligned columns and one
    /// dead key entry per line, so a change to one key is a one-line diff. Comments in
    /// an earlier file are not kept.
    /// </summary>
    public static string Write(Layout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var text = new StringBuilder();
        text.Append("# Key Assign ").Append(layout.Kind == LayoutKind.Pack ? "pack" : "layout").Append(" file; see docs/FORMAT.md.\n");
        text.Append(layout.Kind == LayoutKind.Pack ? "pack " : "layout ").Append(Quote(layout.Name)).Append('\n');
        if (layout.BaseLayout is not null)
        {
            text.Append("base ").Append(Quote(layout.BaseLayout)).Append('\n');
        }

        WriteKeys(text, layout);

        foreach (var dead in layout.DeadKeys)
        {
            text.Append('\n');
            text.Append("dead ").Append(ValueSyntax.FormatChar(dead.Accent)).Append(" {\n");
            foreach (var entry in dead.Entries)
            {
                text.Append("    ").Append(FormatEntryChar(entry.Base)).Append(" = ").Append(FormatEntryChar(entry.Result)).Append('\n');
            }

            text.Append("}\n");
        }

        return text.ToString();
    }

    private static void WriteKeys(StringBuilder text, Layout layout)
    {
        if (layout.Keys.Count == 0)
        {
            return;
        }

        var rows = new List<string[]> { ColumnLabels };
        foreach (var key in layout.Keys)
        {
            rows.Add(
            [
                "key",
                key.Code.Value <= 0xFF ? $"{key.Code.Value:X2}" : $"{key.Code.Value:X4}",
                FormatOutput(key.Normal),
                FormatOutput(key.Shift),
                FormatOutput(key.AltGr),
                FormatOutput(key.AltGrShift),
                key.Caps switch
                {
                    CapsBehavior.InvertsShift => "shift",
                    CapsBehavior.InvertsShiftAndAltGr => "all",
                    _ => "-",
                },
            ]);
        }

        var widths = new int[ColumnLabels.Length];
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                widths[c] = Math.Max(widths[c], row[c].Length);
            }
        }

        text.Append('\n');
        foreach (var row in rows)
        {
            var line = new StringBuilder();
            for (var c = 0; c < row.Length; c++)
            {
                line.Append(row[c].PadRight(widths[c])).Append("  ");
            }

            text.Append(line.ToString().TrimEnd()).Append('\n');
        }
    }

    private static string FormatOutput(KeyOutput output) => output.Kind switch
    {
        OutputKind.None => "none",
        OutputKind.Dead => "dead(" + ValueSyntax.FormatText(output.Text) + ")",
        _ => ValueSyntax.FormatText(output.Text),
    };

    private static string FormatEntryChar(char c) => c == ' ' ? "space" : ValueSyntax.FormatChar(c);

    private static string Quote(string name) => "\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
