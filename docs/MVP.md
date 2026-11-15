# Key Assign MVP: the layout editor

The first usable version is an **editor**: it opens a layout, shows it on an
ANSI or ISO keyboard, lets you change what each key types, and saves it as a
text file. It does **not** build or install a layout yet (that is M5), so
nothing in the MVP touches the system's keyboard configuration (R7.4).

Requirement IDs are in [REQUIREMENTS.md](REQUIREMENTS.md).

## In scope

### 1. Graphical editor (R1.1, R2.1, R2.4, R2.5)

- Draws the keyboard as **ANSI** (one-row Enter, no key left of Z) or **ISO**
  (two-row Enter, extra key between Left Shift and Z). The user picks the shape
  by hand; the layout in use only suggests one.
- Each key shows its outputs; the display can switch between the four levels.
- Keys are identified by scan code, so a layout opens on either shape. Keys the
  current shape lacks, such as the ISO extra key on ANSI, are listed and
  flagged, never dropped.

### 2. Key dialog (R1.1, R3.1)

Clicking a key opens a dialog with one field per level:

| Field | Level |
| --- | --- |
| Normal | base |
| Shift | Shift |
| AltGr | Right Alt |
| AltGr+Shift | Right Alt and Shift |

Each field accepts a value in any of three ways:

1. **Typed** on the keyboard (the character the user's current layout produces).
2. **Pasted** from the clipboard. One character, or one grapheme cluster, is
   taken; anything longer is rejected with a message, since a Windows layout
   can only type up to four UTF-16 code units per key and level, and the MVP
   keeps to one character.
3. **Unicode code** typed as `U+00E1`, `00E1` or `E1`, with the character name
   and a preview shown next to it so a wrong code is obvious.

Each field can also be empty (the key types nothing at that level) or marked as
a **dead key**, which opens the dead key editor below.

**Caps Lock.** Caps Lock does not act the same on every key, so it is set **key
by key**. Each key has one setting, *Caps Lock inverts Shift*:

- **On**, as for letters: with Caps Lock, `[A]` types `A` and Shift+`[A]` types
  `a`. Caps Lock swaps which of the Normal and Shift outputs each press gives.
- **Off**, as for digits and punctuation: `[5]` types `5` and Shift+`[5]` types
  `%` whether Caps Lock is on or not.

A second tick box, *also on the AltGr levels*, applies the same swap to AltGr
and AltGr+Shift (for example, so `[AltGr]+[A]` and `[AltGr]+[Shift]+[A]` swap
with Caps Lock). It is off by default, and is the one switch a layout needs
beyond the plain letter rule.

The setting is data on each key in the text file (the `caps` column in the
sketch below), so it round-trips. When a layout is read from Windows, it is
filled in by trying each key with Caps Lock on, so letters come out *on* and
digits *off* with no manual work; for a new layout, a key defaults to *on* when
its Normal and Shift outputs are a lowercase and uppercase pair, and *off*
otherwise. In the editor the dialog shows the result: with the box ticked, a
small preview shows what the key types with Caps Lock on.

### 3. Dead keys (R3.1, R3.3, R3.4, R3.8)

Marking a level as a dead key (a standalone accent such as `´`) opens a second
keyboard, the **dead key editor**:

- It draws the same physical keyboard. Each key shows the *result* of
  `accent + that key's character`, at Normal and Shift levels, and clicking it
  edits that result with the same three input methods.
- Beside it, a **Space** entry (the accent typed alone; defaults to the accent
  itself) and a list of **other base characters**, for results whose base is
  not on any key, so a dead key can map `a` to `á` and also `ǘ`'s base `ü`. The
  list is how a layer (a whole alphabet reached through one dead key, R3.4) is
  edited: add, remove, or fill from a range.
- A character not listed types the accent and then itself (R3.3).
- Closing the editor warns on an empty table and on two dead keys with the same
  accent (R3.8).

Not in the MVP: dead keys that chain into other dead keys (R3.5) and sustained
layers (R3.6). The model leaves room for both.

### 4. Text files (R1.2, R1.3, R1.4)

- Save and open a layout as **UTF-8 text with comments**, readable and
  diff-friendly. The same file opens in the graphical editor and back with no
  loss (R1.3); there is a test per sample layout for this.
- Reading reports errors with **line and column** and never applies a file
  with errors (R1.4). The graphical editor stays on its previous layout.
- The format is Key Assign's own, with `.klc` left to M8 (decision D1; this
  document assumes the recommended answer).

A sketch, to be settled in `docs/FORMAT.md`:

```
# A layout: scan codes name the keys, levels are columns.
layout "Spanish (custom)"
base   "Spanish (Spain)"

# caps: "-" ignored by Caps Lock, "shift" inverts Shift, "all" also AltGr levels
# scan  normal  shift  altgr   altgr+shift  caps
key 02  1       !      |       ¡            -
key 10  q       Q      @       Ω            shift
key 18  o       O      ø       Ø            all
key 1A  dead(´) ¨      [       {            -
key 56  <       >      U+00A6  .            -         # ISO-only key

dead ´ {
    a = á    A = Á
    e = é    E = É
    space = ´
}
```

### 5. Reading a Windows layout (R4.1)

The editor can **start from a layout installed on the machine**, the one in use
by default, without reverse engineering and without changing anything.

The plan, to be confirmed by spike S2:

- **Primary: ask Windows.** Load the layout's handle (`LoadKeyboardLayout`)
  and, for each scan code and each level, call `MapVirtualKeyEx` and
  `ToUnicodeEx` with a synthetic key state and the "don't change state" flag.
  The documented API reports characters, dead keys (a negative result), and
  Caps Lock behaviour (by trying the same key with Caps Lock set). A dead key's
  table is read by sending each candidate base character after it. Nothing is
  parsed from the DLL, and it works for any layout Windows can load.
- **Fallback: read the DLL's tables.** Layout DLLs export `KbdLayerDescriptor`,
  and their data structures are documented in the Windows Driver Kit's `kbd.h`.
  This is table parsing, not reverse engineering, and it recovers ligatures
  and exact Caps Lock flags that probing can miss. It is only built if the
  primary route proves lossy.

If neither works for some layout, that layout is not offered and the editor
says so; files and new layouts are unaffected. Layouts needing more than the
MVP model (ligatures, chained dead keys) open with those entries kept as
read-only and listed, never silently dropped.

## Out of scope for the MVP

| Item | Where it goes |
| --- | --- |
| Building the layout DLL and installing it (R4.2–R4.8) | M5 |
| Right Alt as AltGr in layouts lacking it (R3.2) | M5, since it only matters on install |
| Packs and their prebuilt sets (R5.1–R5.3) | M6 |
| Chained dead keys, sustained layers (R3.5–R3.7) | M6, M7 |
| JIS, ABNT2, KS and size variants (R2.2, R2.3) | M8 |
| `.klc` import and export, external-editor reload (R1.5, R1.6) | M8 |
| `Scancode Map` remaps and keyboard settings (R6) | M8 |
| Undo and redo, diff against the starting layout (R1.8) | Soon after, see below |
| Typing test area (R1.7) | Soon after, see below |

## Done means

1. Open the layout in use on this machine, see it on ANSI and on ISO, and
   switch between them.
2. Change one key at all four levels, using typing, pasting and a Unicode code.
3. Set Caps Lock per key: `[A]` inverts Shift and `[5]` does not, and the
   typing preview shows both.
4. Make a key a dead key, edit its table on the second keyboard, and see the
   result back on the first.
5. Save as text, edit the file by hand, open it again, and get the same layout.
   Save a file with an error and see its line and column.
6. Tests: round trip for each sample layout (a US, a Spanish with `´` dead key,
   and a Cyrillic layer on AltGr), parser errors, the Unicode-code input, and
   the reader against a fake of the Windows API (R7.4).

## Soon after, cheap to add

- **Typing test (R1.7).** A text box inside the app that feeds each key's scan
  code through the model, so the layout can be tried without installing. This
  is the one MVP-adjacent item worth doing early: it turns "edited" into
  "checked".
- **Undo and redo (R1.8).** Easy if the model is immutable, so keep it so.

## Order of work

1. **Model and text format** (M1): `Layout`, `Key`, `Level`, `DeadKey`; parser,
   writer, `docs/FORMAT.md`, round-trip tests.
2. **Spike S2**: confirm the probing reader on several layouts (US, Spanish,
   German, Russian, a layout with dead keys). Read-only, so it can run here.
3. **Keyboard view** (M3): ANSI and ISO drawing from one table of key positions.
4. **Key dialog and dead key editor** (M4, without undo and the test area).
5. **Open and save** wired to the model, with error reporting.

## What this changes in the roadmap

`ROADMAP.md` calls M1–M5 the MVP, which includes building and installing. This
MVP is smaller: M1, M3 and the editing half of M4, with no M5 and no spikes
S1, S3–S5. If the owner agrees, `ROADMAP.md` and `REQUIREMENTS.md` should be
changed to match: call this release the MVP and renumber the install step as
v0.2, or keep the roadmap and call this M4-alpha.
