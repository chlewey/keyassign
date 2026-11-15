using KeyAssign.Core.Keys;
using KeyAssign.Core.Layouts;

namespace KeyAssign.Core.Tests.Layouts;

public class LayoutModelTests
{
    private static KeyBinding Key(ushort code, string normal, string shift) =>
        new(new ScanCode(code), KeyOutput.Chars(normal), KeyOutput.Chars(shift), KeyOutput.None, KeyOutput.None,
            KeyBinding.DefaultCaps(KeyOutput.Chars(normal), KeyOutput.Chars(shift)));

    [Fact]
    public void KeyOutput_rejects_more_than_four_code_units()
    {
        Assert.Throws<ArgumentException>(() => KeyOutput.Chars("abcde"));
        Assert.Equal("abcd", KeyOutput.Chars("abcd").Text);
    }

    [Fact]
    public void KeyOutput_rejects_empty_text_and_lone_surrogates()
    {
        Assert.Throws<ArgumentException>(() => KeyOutput.Chars(""));
        Assert.Throws<ArgumentException>(() => KeyOutput.Chars("\uD83D"));
        Assert.Throws<ArgumentException>(() => KeyOutput.Dead('\uDC00'));
    }

    [Fact]
    public void KeyOutput_has_value_equality_and_dead_accent()
    {
        Assert.Equal(KeyOutput.Chars("a"), KeyOutput.Chars("a"));
        Assert.NotEqual(KeyOutput.Chars("a"), KeyOutput.Dead('a'));
        Assert.Equal('´', KeyOutput.Dead('´').Accent);
        Assert.Throws<InvalidOperationException>(() => KeyOutput.Chars("a").Accent);
    }

    [Theory]
    [InlineData("a", "A", CapsBehavior.InvertsShift)]
    [InlineData("ñ", "Ñ", CapsBehavior.InvertsShift)]
    [InlineData("5", "%", CapsBehavior.Ignored)]
    [InlineData("1", "1", CapsBehavior.Ignored)]
    public void DefaultCaps_inverts_shift_for_letters_only(string normal, string shift, CapsBehavior expected) =>
        Assert.Equal(expected, KeyBinding.DefaultCaps(KeyOutput.Chars(normal), KeyOutput.Chars(shift)));

    [Fact]
    public void DefaultCaps_ignores_keys_without_text()
    {
        Assert.Equal(CapsBehavior.Ignored, KeyBinding.DefaultCaps(KeyOutput.None, KeyOutput.None));
        Assert.Equal(CapsBehavior.Ignored, KeyBinding.DefaultCaps(KeyOutput.Dead('´'), KeyOutput.Dead('¨')));
    }

    [Fact]
    public void KeyBinding_indexer_and_With_use_the_level()
    {
        var key = Key(0x1E, "a", "A");
        var changed = key.With(Level.AltGr, KeyOutput.Chars("æ")).With(Level.AltGrShift, KeyOutput.Chars("Æ"));

        Assert.Equal("a", key[Level.Normal].Text);
        Assert.Equal("æ", changed[Level.AltGr].Text);
        Assert.Equal("Æ", changed[Level.AltGrShift].Text);
        Assert.Equal(KeyOutput.None, key[Level.AltGr]);
    }

    [Fact]
    public void Layout_rejects_a_scan_code_bound_twice()
    {
        var keys = new[] { Key(0x1E, "a", "A"), Key(0x1E, "b", "B") };
        Assert.Throws<ArgumentException>(() => new Layout("x", LayoutKind.Layout, null, keys, []));
    }

    [Fact]
    public void Layout_rejects_a_key_without_scan_code()
    {
        Assert.Throws<ArgumentException>(() =>
            new Layout("x", LayoutKind.Layout, null, [Key(0x0000, "a", "A")], []));
    }

    [Fact]
    public void Layout_rejects_two_tables_for_one_accent()
    {
        var dead = new[] { new DeadKey('´', []), new DeadKey('´', []) };
        Assert.Throws<ArgumentException>(() => new Layout("x", LayoutKind.Layout, null, [], dead));
    }

    [Fact]
    public void DeadKey_rejects_a_base_listed_twice_and_looks_up_results()
    {
        Assert.Throws<ArgumentException>(() =>
            new DeadKey('´', [new DeadEntry('a', 'á'), new DeadEntry('a', 'à')]));

        var dead = new DeadKey('´', [new DeadEntry('a', 'á'), new DeadEntry(' ', '´')]);
        Assert.Equal('á', dead.Lookup('a'));
        Assert.Equal('´', dead.Lookup(' '));
        Assert.Null(dead.Lookup('x'));
    }

    [Fact]
    public void Layouts_with_the_same_content_are_equal()
    {
        Layout Make() => new("x", LayoutKind.Layout, "base", [Key(0x1E, "a", "A")], [new DeadKey('´', [new DeadEntry('a', 'á')])]);

        Assert.Equal(Make(), Make());
        Assert.NotEqual(Make(), new Layout("y", LayoutKind.Layout, "base", [], []));
    }

    [Fact]
    public void Layout_finds_keys_and_dead_keys()
    {
        var layout = new Layout("x", LayoutKind.Layout, null, [Key(0x1E, "a", "A")], [new DeadKey('´', [])]);

        Assert.NotNull(layout.FindKey(new ScanCode(0x1E)));
        Assert.Null(layout.FindKey(new ScanCode(0x1F)));
        Assert.NotNull(layout.FindDeadKey('´'));
        Assert.Null(layout.FindDeadKey('`'));
    }
}
