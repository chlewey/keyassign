# Key Assign requirements

Key Assign edits Windows keyboard layouts: what each key types at each level,
dead keys and layers, and the physical keyboard they are drawn on. It builds the
result into a real Windows layout and installs it in place of the one in use.
`docs/ROADMAP.md` orders this work into milestones.

Priorities: **Must** is needed for the MVP, **Should** for v1.0, and **Could**
is desirable but can wait. IDs are stable; don't renumber them.

## Terms

- **Physical layout:** the shape of the keyboard: which keys exist and where,
  such as ANSI or ISO. It changes how the keyboard is drawn, not what it types.
- **Logical layout:** what each key types. This is what Windows calls a keyboard
  layout, a DLL such as `kbdsp.dll` (Spanish).
- **Level:** a modifier combination for a key: base, Shift, AltGr, Shift+AltGr,
  and Caps Lock's effect on them.
- **Dead key:** a key that types nothing itself and changes the next keystroke
  only. For example, `´` then `a` types `á`.
- **Layer:** a whole set of outputs reached through a dead key. For example,
  AltGr+K could be a Cyrillic layer, where AltGr+K, then `A`, types `А`.
- **Sustain:** a key that turns a one-shot layer into a locked mode, which stays
  on until you exit it.
- **Pack:** a reusable set of dead keys and layers, such as Cyrillic or IPA, that
  can be added to any logical layout.

## Editing

| ID | Priority | Requirement |
| --- | --- | --- |
| R1.1 | Must | **Graphical editing.** Show the keyboard on screen. Selecting a key shows and edits its output at every level. |
| R1.2 | Must | **Text-file editing.** Every layout can be saved and edited as a plain UTF-8 text file, readable and diff-friendly, with comments. |
| R1.3 | Must | Both modes edit the same model. Converting between graphical and text is lossless, so a file saved from either opens unchanged in the other. |
| R1.4 | Must | Reading a text file reports errors with their line and column, and never applies a file that has errors. |
| R1.5 | Should | Edit the text file in an external editor, with Key Assign reloading it when it changes. |
| R1.6 | Should | Import and export MSKLC's `.klc` format, so existing layouts can be brought in and taken out. |
| R1.7 | Must | Typing test area: try the layout being edited before installing it. |
| R1.8 | Should | Undo and redo, and a diff of changes against the layout it started from. |

## Physical layouts

| ID | Priority | Requirement |
| --- | --- | --- |
| R2.1 | Must | Draw the keyboard in the main physical layouts. **ANSI:** one-row Enter, no key between Left Shift and Z. **ISO:** two-row Enter, with an extra key between Left Shift and Z. |
| R2.2 | Should | Also JIS (Japan), ABNT2 (Brazil, extra key by Right Shift) and KS (Korea), each with its extra keys. |
| R2.3 | Should | Size variants: full size, tenkeyless, and laptop without a numeric keypad. |
| R2.4 | Must | Choose the physical layout by hand, with a suggestion based on the logical layout in use, since Windows can't reliably detect the physical shape. |
| R2.5 | Must | Keys are identified by scan code, so a logical layout made on one physical layout works on another; keys the other shape lacks are flagged. |
| R2.6 | Could | Custom physical layouts, defined in the same text format. |

## Dead keys, AltGr and layers

| ID | Priority | Requirement |
| --- | --- | --- |
| R3.1 | Must | Make any key at any level a dead key, notably at the AltGr level (Right Alt). |
| R3.2 | Must | Make Right Alt act as AltGr in layouts that don't have it, such as US English. |
| R3.3 | Must | Edit each dead key's table: which base character gives which result. Space after the dead key types the accent itself; an unlisted character types both. |
| R3.4 | Must | **Layers:** a dead key whose table maps a whole alphabet, as in the Cyrillic example: AltGr+K, then `A`, `B` types `АB`, with only the first letter in Cyrillic. |
| R3.5 | Should | **Chained dead keys:** a dead key followed by another, such as `´` then `¨` then `u` for `ǘ`. |
| R3.6 | Should | **Sustained layers:** a dead key followed by the sustain key locks its layer. AltGr+K, Sustain, then `A`, `B` types `АБ`, and every key stays Cyrillic until you exit. |
| R3.7 | Should | Exit a sustained layer with the same dead key, Esc, or a configurable key, and show an on-screen indicator while one is on. |
| R3.8 | Must | Warn about conflicts: a character that a layout can no longer type, a dead key with an empty table, or two dead keys with the same accent. |

## Installing on the layout in use

| ID | Priority | Requirement |
| --- | --- | --- |
| R4.1 | Must | **Start from the layout in use.** Open the current logical layout as the starting point, read from its installed DLL. |
| R4.2 | Must | Build the edited layout into a Windows keyboard layout DLL, for x64, x86 (32-bit apps) and ARM64. |
| R4.3 | Must | **Apply transparently.** Windows' own layout DLLs are system-protected and replaced by updates, so they aren't modified. Instead, install the new layout, put it in the user's input list where the old one was, select it, and remove the old one from the list. To the user, the layout in use just changed. |
| R4.4 | Must | **Update transparently.** A layout made by Key Assign is replaced by uninstalling the old version and installing the new one. Since Windows keeps a layout DLL in use loaded, each version gets its own file and the old one is removed once nothing uses it. |
| R4.5 | Must | Ask for administrator rights only for the install step; editing never needs them. |
| R4.6 | Must | **Reversible.** Snapshot the input list and registry before installing, and offer to restore the original layout or remove everything Key Assign installed. |
| R4.7 | Must | Never leave the user without a working layout. Check the build before installing and keep the previous layout installed until the new one is confirmed working. |
| R4.8 | Should | Say clearly when a change needs signing out or restarting, and for which apps. |

## Packs

| ID | Priority | Requirement |
| --- | --- | --- |
| R5.1 | Must | Packs use the same text format as layouts and can be added to, or removed from, any layout. |
| R5.2 | Must | When a pack is added, choose which keys trigger its dead keys, AltGr+K by default for Cyrillic, for example, and report clashes with existing keys. |
| R5.3 | Should | **Prebuilt packs:** Cyrillic (phonetic), Greek, extended Latin diacritics for European languages, IPA, mathematical symbols and arrows, typography (quotes, dashes, spaces), and currency symbols. |
| R5.4 | Could | More packs, such as Esperanto, Vietnamese, Hebrew and Arabic transliteration, superscripts and subscripts, and box drawing. |
| R5.5 | Could | Share packs as single files, and install them by opening the file. |

## Other keyboard changes

From the skeleton roadmap. These don't need a layout DLL.

| ID | Priority | Requirement |
| --- | --- | --- |
| R6.1 | Should | Key-to-key remaps and disabled keys through the `Scancode Map` registry value (`KeyAssign.Core.Keys.ScancodeMap`). |
| R6.2 | Could | Keyboard settings: repeat delay and rate, Sticky Keys and Filter Keys, and Caps Lock behavior. |
| R6.3 | Could | Order the input list and set the layout-switching shortcut. |

## Non-functional

| ID | Priority | Requirement |
| --- | --- | --- |
| R7.1 | Must | Windows 11, x64 and ARM64. Windows 10 is a bonus, not a target. |
| R7.2 | Must | No telemetry; everything stays on the machine. |
| R7.3 | Should | English and Spanish UI and documentation in v1.0. |
| R7.4 | Must | Tests never touch the real keyboard configuration. Registry, input-list and installation code sits behind interfaces with fakes. |
| R7.5 | Should | Accessible: full keyboard navigation in the editor, and screen-reader names for keys. |

## Questions to settle by spikes

These decide how some requirements are built, or whether they can be. Each gets
a short spike in `docs/spikes/` before the milestone that depends on it.

- **S1. Building the DLL** (R4.2). Generate the layout DLL directly (it's mostly
  data tables), or rely on Microsoft's MSKLC tools, which need checking for
  redistribution terms and ARM64 support? Does Windows require the DLL to be
  signed?
- **S2. Reading the layout in use** (R4.1). Load the active layout's DLL and read
  its tables, including dead keys and ligatures, without changing anything.
- **S3. Installing and swapping** (R4.3, R4.4). Register a layout, swap the
  user's input list, and replace a version without a restart. What still needs
  signing out?
- **S4. Sustained layers** (R3.6). A layout DLL's dead keys are one-shot. A lock
  might use a locking modifier (as Japanese layouts lock Kana), a background
  keyboard hook, or a text input processor (TSF). Which one works across apps,
  including elevated windows and the sign-in screen?
- **S5. Chained dead keys** (R3.5). Confirm that dead keys whose result is
  another dead key work in current Windows 11 apps, both classic and modern.
