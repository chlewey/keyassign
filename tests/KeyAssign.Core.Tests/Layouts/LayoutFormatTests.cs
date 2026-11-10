using KeyAssign.Core.Keys;
using KeyAssign.Core.Layouts;
using KeyAssign.Core.Layouts.Format;

namespace KeyAssign.Core.Tests.Layouts;

public class LayoutFormatTests
{
    private static string Sample(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", name));

    private static Layout ParseOk(string text)
    {
        var result = LayoutFormat.Parse(text);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Layout!;
    }

    private static IReadOnlyList<ParseError> ParseFail(string text)
    {
        var result = LayoutFormat.Parse(text);
        Assert.False(result.Succeeded);
        Assert.Null(result.Layout);
        Assert.NotEmpty(result.Errors);
        return result.Errors;
    }

    // ---- samples and round trips (R1.3) ----

    [Theory]
    [InlineData("us.kal")]
    [InlineData("es.kal")]
    [InlineData("cyrillic.kal")]
    public void Samples_round_trip_without_loss(string file)
    {
        var layout = ParseOk(Sample(file));

        var written = LayoutFormat.Write(layout);
        var again = ParseOk(written);

        Assert.Equal(layout, again);
        Assert.Equal(written, LayoutFormat.Write(again));
    }

    [Fact]
    public void Us_sample_has_letters_inverting_shift_and_digits_ignoring_it()
    {
        var us = ParseOk(Sample("us.kal"));

        Assert.Equal(CapsBehavior.InvertsShift, us.FindKey(new ScanCode(0x1E))!.Caps);
        Assert.Equal(CapsBehavior.Ignored, us.FindKey(new ScanCode(0x06))!.Caps);
        Assert.Equal("%", us.FindKey(new ScanCode(0x06))!.Shift.Text);
        Assert.Equal("#", us.FindKey(new ScanCode(0x04))!.Shift.Text);
        Assert.Equal("\"", us.FindKey(new ScanCode(0x28))!.Shift.Text);
        Assert.Equal(" ", us.FindKey(new ScanCode(0x39))!.Normal.Text);
    }

    [Fact]
    public void Spanish_sample_has_dead_keys_and_an_iso_key()
    {
        var es = ParseOk(Sample("es.kal"));

        Assert.Equal(LayoutKind.Layout, es.Kind);
        Assert.Equal("Spanish (Spain)", es.BaseLayout);
        Assert.Equal(KeyOutput.Dead('´'), es.FindKey(new ScanCode(0x28))!.Normal);
        Assert.Equal(KeyOutput.Dead('¨'), es.FindKey(new ScanCode(0x28))!.Shift);
        Assert.Equal("<", es.FindKey(new ScanCode(0x56))!.Normal.Text);
        Assert.Equal('á', es.FindDeadKey('´')!.Lookup('a'));
        Assert.Equal('´', es.FindDeadKey('´')!.Lookup(' '));
        Assert.Equal('Ü', es.FindDeadKey('¨')!.Lookup('U'));
        Assert.Equal(4, es.DeadKeys.Count);
    }

    [Fact]
    public void Cyrillic_pack_is_a_layer_on_AltGr_K()
    {
        var pack = ParseOk(Sample("cyrillic.kal"));

        Assert.Equal(LayoutKind.Pack, pack.Kind);
        var trigger = pack.FindKey(new ScanCode(0x25))!;
        Assert.Equal(KeyOutput.Dead('⌨'), trigger.AltGr);

        var layer = pack.FindDeadKey('⌨')!;
        Assert.Equal('а', layer.Lookup('a'));
        Assert.Equal('А', layer.Lookup('A'));
        Assert.Equal('б', layer.Lookup('b'));
        Assert.Null(layer.Lookup('q'));
    }

    [Fact]
    public void Awkward_characters_round_trip()
    {
        var specials = new[]
        {
            "#", "\"", "{", "}", "=", "(", ")", "+", "U", "u+0041", "\t", " ", " ", "́", "‍",
            "ffi", "\U0001D538", "é", "\\", "-", "none",
        };
        var keys = specials.Select((s, i) =>
            new KeyBinding(
                new ScanCode((ushort)(0x02 + i)),
                KeyOutput.Chars(s.Length <= 4 ? s : "x"),
                KeyOutput.None,
                KeyOutput.Dead('́'),
                KeyOutput.Dead('('),
                CapsBehavior.InvertsShiftAndAltGr)).ToList();
        keys.Add(new KeyBinding(new ScanCode(0xE05B), KeyOutput.None, KeyOutput.None, KeyOutput.Dead(')'), KeyOutput.Dead('#'), CapsBehavior.Ignored));
        var dead = new[]
        {
            new DeadKey('#', [new DeadEntry('=', '{'), new DeadEntry('}', '"'), new DeadEntry(' ', '#'), new DeadEntry('́', 'x')]),
            new DeadKey('(', []),
        };
        var layout = new Layout("A \"quoted\" \\ name", LayoutKind.Layout, "Base \"x\"", keys, dead);

        var again = ParseOk(LayoutFormat.Write(layout));

        Assert.Equal(layout, again);
    }

    [Fact]
    public void Writer_aligns_columns_and_ends_with_a_newline()
    {
        var written = LayoutFormat.Write(ParseOk(Sample("us.kal")));

        Assert.EndsWith("\n", written);
        Assert.DoesNotContain("\r", written);
        Assert.All(written.Split('\n'), line => Assert.Equal(line.TrimEnd(), line));

        // Every column starts at the same place on every key line.
        var keyLines = written.Split('\n').Where(l => l.StartsWith("key ")).ToArray();
        Assert.True(keyLines.Length > 40);
        var starts = keyLines.Select(ColumnStarts).ToArray();
        Assert.All(starts, s => Assert.Equal(starts[0], s));
    }

    private static int[] ColumnStarts(string line)
    {
        var starts = new List<int>();
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != ' ' && (i == 0 || line[i - 1] == ' '))
            {
                starts.Add(i);
            }
        }

        return starts.ToArray();
    }

    [Fact]
    public void CRLF_files_read_the_same_as_LF_files()
    {
        var lf = Sample("es.kal").Replace("\r\n", "\n");

        Assert.Equal(ParseOk(lf), ParseOk(lf.Replace("\n", "\r\n")));
    }

    [Fact]
    public void A_byte_order_mark_is_ignored()
    {
        Assert.Equal("x", ParseOk("﻿layout \"x\"").Name);
    }

    [Fact]
    public void Comments_and_blank_lines_are_ignored_anywhere()
    {
        var layout = ParseOk("# top\n\nlayout \"x\" # trailing\nkey 1E a A none none # more\n dead ´ { # open\n a = á # in\n }\n");

        Assert.Single(layout.Keys);
        Assert.Equal('á', layout.FindDeadKey('´')!.Lookup('a'));
    }

    [Fact]
    public void Explicit_caps_columns_are_read()
    {
        var layout = ParseOk("layout \"x\"\nkey 1E a A none none -\nkey 1F s S none none shift\nkey 20 d D none none all\n");

        Assert.Equal(CapsBehavior.Ignored, layout.FindKey(new ScanCode(0x1E))!.Caps);
        Assert.Equal(CapsBehavior.InvertsShift, layout.FindKey(new ScanCode(0x1F))!.Caps);
        Assert.Equal(CapsBehavior.InvertsShiftAndAltGr, layout.FindKey(new ScanCode(0x20))!.Caps);
    }

    [Fact]
    public void Codes_and_bare_characters_mean_the_same_thing()
    {
        var layout = ParseOk("layout \"x\"\nkey 1E U+0061 u+0041 U+00E6 U+0066+0066+0069 -\n");
        var key = layout.Keys.Single();

        Assert.Equal("a", key.Normal.Text);
        Assert.Equal("A", key.Shift.Text);
        Assert.Equal("æ", key.AltGr.Text);
        Assert.Equal("ffi", key.AltGrShift.Text);
    }

    [Fact]
    public void Extended_scan_codes_are_read()
    {
        var layout = ParseOk("layout \"x\"\nkey E01D none none none none\n");

        Assert.NotNull(layout.FindKey(ScanCode.RightControl));
    }

    [Fact]
    public void A_pack_may_hold_only_dead_keys()
    {
        var pack = ParseOk("pack \"p\"\ndead ´ { a = á }\n");

        Assert.Equal(LayoutKind.Pack, pack.Kind);
        Assert.Empty(pack.Keys);
        Assert.Single(pack.DeadKeys);
    }

    [Fact]
    public void A_dead_block_can_sit_on_one_line_or_many()
    {
        var one = ParseOk("layout \"x\"\ndead ´ { a = á A = Á }\n");
        var many = ParseOk("layout \"x\"\ndead ´ {\n  a = á\n  A = Á\n}\n");

        Assert.Equal(one, many);
    }

    // ---- errors with line and column (R1.4) ----

    [Fact]
    public void An_empty_file_needs_a_header()
    {
        var error = Assert.Single(ParseFail(""));

        Assert.Equal(new ParseError(1, 1, error.Message), error);
        Assert.Contains("layout", error.Message);
    }

    [Fact]
    public void A_missing_header_is_reported_once_at_the_first_statement()
    {
        var errors = ParseFail("# c\n\n  key 1E a A none none\nkey 1F s S none none\n");

        var error = Assert.Single(errors);
        Assert.Equal(3, error.Line);
        Assert.Equal(3, error.Column);
    }

    [Fact]
    public void An_unknown_statement_is_reported_at_its_position()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\n  keyy 1E a A none none\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal(3, error.Column);
        Assert.Contains("keyy", error.Message);
    }

    [Fact]
    public void A_key_with_too_few_columns_is_reported_at_the_end_of_the_line()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\nkey 1E a A\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal("key 1E a A".Length + 1, error.Column);
    }

    [Fact]
    public void A_key_with_extra_text_is_reported_at_the_extra_token()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\nkey 1E a A none none shift extra\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal("key 1E a A none none shift ".Length + 1, error.Column);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("ZZ")]
    [InlineData("00")]
    [InlineData("E000")]
    [InlineData("E200")]
    [InlineData("1E00")]
    public void A_bad_scan_code_is_reported_at_its_column(string code)
    {
        var error = Assert.Single(ParseFail($"layout \"x\"\nkey {code} a A none none\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal(5, error.Column);
    }

    [Fact]
    public void A_scan_code_bound_twice_is_reported_at_the_second_use()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\nkey 1E a A none none\nkey 1E b B none none\n"));

        Assert.Equal(3, error.Line);
        Assert.Equal(5, error.Column);
    }

    [Fact]
    public void A_bad_output_is_reported_at_its_column()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\nkey 1E a AB none none\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal("key 1E a ".Length + 1, error.Column);
        Assert.Contains("AB", error.Message);
    }

    [Theory]
    [InlineData("U+12", "U+12")]
    [InlineData("U+110000", "U+110000")]
    [InlineData("U+D800", "U+D800")]
    [InlineData("U+0061+0062+0063+0064+0065", "five")]
    public void A_bad_unicode_code_is_rejected(string code, string _)
    {
        ParseFail($"layout \"x\"\nkey 1E {code} A none none\n");
    }

    [Theory]
    [InlineData("{", "U+007B")]
    [InlineData("}", "U+007D")]
    [InlineData("=", "U+003D")]
    public void Reserved_characters_say_how_to_write_them(string bad, string hint)
    {
        var errors = ParseFail($"layout \"x\"\nkey 1E {bad} A none none\n");

        Assert.Contains(errors, e => e.Line == 2 && e.Column == 8 && e.Message.Contains(hint));
    }

    [Fact]
    public void A_hash_starts_a_comment_so_it_must_be_spelled_as_a_code()
    {
        var errors = ParseFail("layout \"x\"\nkey 1E # A none none\n");

        Assert.Equal(2, errors[0].Line);
    }

    [Fact]
    public void A_bad_caps_setting_is_reported()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\nkey 1E a A none none maybe\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal("key 1E a A none none ".Length + 1, error.Column);
    }

    [Fact]
    public void A_dead_accent_must_be_one_character()
    {
        var errors = ParseFail("layout \"x\"\nkey 1E dead(ab) A none none\n");

        Assert.Equal(2, errors[0].Line);
        Assert.Equal("key 1E dead(".Length + 1, errors[0].Column);
    }

    [Fact]
    public void An_unclosed_dead_block_is_reported_at_its_start()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\n\n  dead ´ {\n a = á\n"));

        Assert.Equal(3, error.Line);
        Assert.Equal(3, error.Column);
    }

    [Fact]
    public void A_dead_entry_without_equals_is_reported()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\ndead ´ {\n  a á\n}\n"));

        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void A_dead_block_needs_an_opening_brace()
    {
        var errors = ParseFail("layout \"x\"\ndead ´\n");

        Assert.Equal(2, errors[0].Line);
    }

    [Fact]
    public void A_base_listed_twice_in_a_dead_table_is_reported_at_the_second()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\ndead ´ {\n  a = á\n  a = à\n}\n"));

        Assert.Equal(4, error.Line);
        Assert.Equal(3, error.Column);
    }

    [Fact]
    public void Two_tables_for_one_accent_are_reported_at_the_second()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\ndead ´ { }\ndead ´ { }\n"));

        Assert.Equal(3, error.Line);
        Assert.Equal(6, error.Column);
    }

    [Fact]
    public void Chained_dead_keys_are_not_supported_yet()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\ndead ´ {\n  ¨ = dead(¨)\n}\n"));

        Assert.Equal(3, error.Line);
        Assert.Equal(7, error.Column);
        Assert.Contains("chaining", error.Message);
    }

    [Fact]
    public void A_dead_table_holds_single_utf16_characters()
    {
        var errors = ParseFail("layout \"x\"\ndead ´ {\n  a = U+0061+0062\n  U+1D538 = A\n}\n");

        Assert.Equal([3, 4], errors.Select(e => e.Line).ToArray());
    }

    [Fact]
    public void Text_after_the_closing_brace_is_reported()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\ndead ´ { a = á } extra\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal("dead ´ { a = á } ".Length + 1, error.Column);
    }

    [Fact]
    public void A_header_needs_a_quoted_name()
    {
        Assert.Equal(1, ParseFail("layout x\n")[0].Line);
        Assert.Equal(8, ParseFail("layout x\n")[0].Column);
        Assert.Equal(1, ParseFail("layout\n")[0].Line);
        Assert.Equal(1, ParseFail("layout \"\"\n")[0].Line);
    }

    [Fact]
    public void An_unterminated_string_is_reported_at_its_opening_quote()
    {
        var error = Assert.Single(ParseFail("layout \"x\nkey 1E a A none none\n").Where(e => e.Line == 1));

        Assert.Equal(8, error.Column);
    }

    [Fact]
    public void Two_headers_are_reported()
    {
        var error = Assert.Single(ParseFail("layout \"x\"\nlayout \"y\"\n"));

        Assert.Equal(2, error.Line);
        Assert.Equal(1, error.Column);
    }

    [Fact]
    public void Several_errors_are_all_reported_in_order()
    {
        var errors = ParseFail("layout \"x\"\nkey ZZ a A none none\nbogus\nkey 1E a A none none\nkey 1E b B none none\n");

        Assert.Equal([2, 3, 5], errors.Select(e => e.Line).ToArray());
    }

    [Fact]
    public void Errors_format_as_line_colon_column()
    {
        Assert.Equal("3:7: oops", new ParseError(3, 7, "oops").ToString());
    }

    [Fact]
    public void Parse_never_returns_a_layout_when_there_are_errors()
    {
        var result = LayoutFormat.Parse("layout \"x\"\nkey 1E a A none none\nbogus\n");

        Assert.False(result.Succeeded);
        Assert.Null(result.Layout);
    }

    [Fact]
    public void Parse_and_write_reject_null()
    {
        Assert.Throws<ArgumentNullException>(() => LayoutFormat.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => LayoutFormat.Write(null!));
    }
}
