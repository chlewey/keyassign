# Key Assign roadmap

Key Assign is a Windows 11 keyboard layout editor. It edits what each key types,
graphically or as a text file, with dead keys, AltGr layers and prebuilt packs,
and installs the result in place of the layout in use. The requirements, with
their IDs, are in [REQUIREMENTS.md](REQUIREMENTS.md).

## Approach

- **One model, two editors.** `KeyAssign.Core` holds the layout model. The
  graphical editor and the text format are two views of it (R1.3).
- **Own layouts, never system files.** Windows' layout DLLs stay untouched. Key
  Assign builds its own DLL and swaps it into the user's input list (R4.3).
- **Reversible.** Snapshot before every install and restore on request (R4.6),
  as Extension Icons does with its icon overrides.
- **Spikes first.** Building, reading and installing layout DLLs, and sustained
  layers, are settled by spikes S1–S5 before the milestones that depend on them.

### Ways Windows can change keys

| Mechanism | What it does | Scope | Needs | Takes effect | Used for |
| --- | --- | --- | --- | --- | --- |
| Layout DLL (as built by MSKLC) | Everything a key types at each level, dead keys, AltGr | Installed per machine, chosen per user | Administrator to install | Right after selecting it, in most apps | The core of Key Assign (R3, R4) |
| Keyboard hook (`WH_KEYBOARD_LL`) | Any key behavior, while the app runs | Current user | A background process | Immediately | Possibly sustained layers (S4) |
| Text input processor (TSF) | Composes text, as input methods do | Current user | Registering a component | Immediately | Possibly sustained layers (S4) |
| `Scancode Map` (registry) | One key to another, or disabled | Every user, including sign-in | Administrator | After signing out | Simple remaps (R6.1) |
| Input list and settings | Order layouts, repeat rate, Sticky Keys | Current user | Nothing special | Immediately | Swapping layouts (R4.3), settings (R6.2) |

## Milestones

### M0. Skeleton (done)

Solution with a WPF app, Core library and xUnit tests; `ScancodeMap` encoder and
decoder; README, LEEME and MIT license.

### M1. Layout model and text format

R1.2, R1.3, R1.4, R3.1, R3.3, R3.4, R5.1.

- Layout model in Core: keys by scan code, levels, dead keys with their tables,
  layers.
- The text format: specification in `docs/FORMAT.md`, a parser with line and
  column errors, and a writer that round-trips it.
- Tests with sample layouts, including the Cyrillic layer example.

### M2. Spikes

S1–S5, each written up in `docs/spikes/` with a working proof of concept on a
throwaway VM, never on the main machine. Their findings may change M4–M7.

### M3. Read and view

R1.1 (read-only), R2.1, R2.4, R2.5, R4.1.

- Load the layout in use from its DLL.
- Draw it on an ANSI or ISO keyboard, showing every level.

### M4. Graphical editor

R1.1, R1.7, R1.8, R3.1–R3.4, R3.8.

- Edit keys, levels and dead keys on screen, with a typing test area.
- Undo and redo, and a diff against the starting layout.
- Make Right Alt act as AltGr (R3.2).

### M5. Build and install (MVP)

R4.2–R4.8.

- Build the layout DLL for x64, x86 and ARM64.
- Install it, swap it into the input list, and update it transparently.
- Snapshot, restore and uninstall everything.

The MVP is M1–M5: edit the layout in use graphically or as text, with AltGr dead
keys and one-shot layers, and install it transparently.

### M6. Packs and chained dead keys

R3.5, R5.2, R5.3.

- The prebuilt packs, with key choice and clash reports.
- Chained dead keys, if S5 confirms they work.

### M7. Sustained layers

R3.6, R3.7, using the mechanism S4 chose, with the on-screen indicator.

### M8. More physical layouts and other changes

R1.5, R1.6, R2.2, R2.3, R6.1–R6.3: JIS, ABNT2 and KS; size variants; external
editor reload; `.klc` import and export; `Scancode Map` remaps and keyboard
settings.

### M9. v1.0

R7.3, R7.5: installer, smoke-test checklist, English and Spanish UI,
accessibility pass, and a landing page at `keyassign.ilecto.app`.

## Open decisions

- **D1. Text format:** Key Assign's own format (readable, with comments, as
  R1.2 asks), with `.klc` only as import and export, or `.klc` as the main
  format? `.klc` is UTF-16 and can't express layers, sustain or packs.
- **D2. Relation to MSKLC and PowerToys Keyboard Manager:** Key Assign covers
  MSKLC's ground (MSKLC hasn't been updated in years; S1 checks its ARM64 and
  chained dead key support) and differs from PowerToys, which remaps keys
  through a hook rather than editing layouts. Should the README position it
  against them?
- **D3. Packaging:** unpackaged with an installer, as planned for Extension
  Icons, or MSIX? Installing layout DLLs needs administrator rights either way.
- **D4. Default sustain key:** which key locks a layer by default, such as
  AltGr+Space or pressing the dead key twice?
