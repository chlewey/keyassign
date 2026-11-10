# Layout text format

The plain-text form of a layout or a pack (R1.2, R5.1). It is UTF-8, one
statement per line, with `#` comments, and it is the same model the graphical
editor edits (R1.3). The reader is `LayoutFormat.Parse` and the writer
`LayoutFormat.Write` in `KeyAssign.Core.Layouts.Format`. Samples that the tests
read are in `tests/KeyAssign.Core.Tests/Samples/`.

The format is Key Assign's own; `.klc` import and export come in M8. Decision D1
in the roadmap is still the owner's. The extension `.kal` used by the samples is
a suggestion.

## Example

```
# A comment runs from # to the end of the line.
layout "Spanish (custom)"
base "Spanish (Spain)"

# key  scan  normal   shift    altgr  altgr+shift  caps
key 02  1       !        |        none   -
key 10  q       Q        @        none   shift
key 28  dead(´) dead(¨)  U+007B   none   -

dead ´ {
    a = á    A = Á
    space = ´
}
```

## Statements

| Statement | Meaning |
| --- | --- |
| `layout "Name"` or `pack "Name"` | Required, once, first. A layout is a whole keyboard; a pack is a set of dead keys to add to a layout (adding is M6). |
| `base "Name"` | Optional. The installed layout this one was made from. |
| `key SCAN NORMAL SHIFT ALTGR ALTGR+SHIFT [CAPS]` | What one key types at each level. |
| `dead ACCENT { ... }` | The table of one dead key. |

Names are in double quotes; inside, write `\"` and `\\`.

### `key`

- **SCAN** is the scan code in hex: two digits (`1E`), or `E0` or `E1` and two
  digits (`E01D`). The same code is the same physical key on ANSI and ISO
  keyboards (R2.5). A code can be bound once.
- Each of the four **outputs** is one of:
  - one character, bare: `a`, `ñ`, `€`, `e` followed by a combining accent;
  - a Unicode code: `U+00E1`, or several joined with `+` (`U+0066+0066+0069`)
    for a ligature of up to four UTF-16 code units;
  - `none`, for nothing typed;
  - `dead(X)`, where X is a character or code: the key is a dead key and
    `X` is its accent, which names its `dead` table.
- **CAPS** is what Caps Lock does to this key, set per key:
  `-` ignores it (digits), `shift` swaps the Normal and Shift outputs (letters),
  `all` also swaps AltGr and AltGr+Shift. When the column is left out, a key
  whose Normal output is the lowercase of its Shift output gets `shift` and
  every other key gets `-`. The writer always writes the column.

### `dead`

```
dead ACCENT {
    BASE = RESULT
    ...
}
```

`BASE = RESULT` pairs can be several to a line, and `{` and `}` can sit on the
same line as entries. Each side is one UTF-16 character, as in a Windows layout
(a character outside the BMP can't be in a table). `space` stands for the space
bar: `space = ´` types the accent alone when it is followed by Space. A
character that isn't listed types the accent and then itself (R3.3).

A table that maps a whole alphabet is a **layer** (R3.4): see
`Samples/cyrillic.kal`, where AltGr+K then `a` types `а`.

A result can't be a dead key yet; chained dead keys are R3.5 (M6).

## Characters with a meaning in the file

Whitespace, `#`, `"`, `{`, `}` and `=` can't be written bare. Write them as
`U+0020`, `U+0023`, `U+0022`, `U+007B`, `U+007D` and `U+003D`. The writer also
spells out control characters, lone combining marks and invisible characters
this way, so a file stays readable in any editor.

## Errors (R1.4)

Every problem is reported as `line:column: message`, with both counted from 1
and the column counted in UTF-16 characters. A statement with an error is
skipped and reading goes on, so one pass reports many errors. A file with any
error is not applied: `ParseResult.Layout` is null.

## What the writer does

It writes a fixed form: keys in the order of the model with aligned columns, one
dead key entry per line, so a change to one key is a one-line diff. It does not
keep comments from an earlier file; a file edited by hand and saved from the
graphical editor loses them. Keeping comments is a possible later improvement.
