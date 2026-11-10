# Key Assign roadmap

Key Assign is a Windows 11 app to change keyboard settings and assign keys:
remap one key to another, disable keys, and manage the keyboard layouts and
input settings Windows already offers, from one place and reversibly.

## Ways Windows can change keys

Each mechanism has a different reach, cost and risk, so the app will likely
combine more than one. Picking which ones the MVP uses is decision D1 below.

| Mechanism | What it does | Scope | Needs | Takes effect |
| --- | --- | --- | --- | --- |
| `Scancode Map` (registry, `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout`) | One key to another, or disabled. No combinations, no per-app rules | Every user, every app, including sign-in | Administrator | After signing out and back in, or a restart |
| Low-level keyboard hook (`WH_KEYBOARD_LL`) | Keys, combinations and shortcuts, optionally per app | Current user, while the app runs | A background process | Immediately |
| Keyboard layouts (`HKCU\Keyboard Layout\Preload`, language settings) | Add, remove and order installed layouts and switching shortcuts | Current user | Nothing special | Immediately or at next sign-in |
| Custom layout DLL (as built by MSKLC) | A whole new layout, including dead keys and AltGr | Installed per machine, chosen per user | Administrator to install | After installing and selecting it |
| Keyboard settings (repeat delay and rate, Sticky/Filter keys, Caps Lock behavior) | System parameters via `SystemParametersInfo` and settings keys | Current user | Nothing special | Immediately |

## Principles

- **Reversible.** Snapshot every setting before the first change, and offer
  "restore" per change and for everything, as Extension Icons does.
- **Never lock the user out.** Warn before disabling keys needed to sign in or
  recover, such as Ctrl, Alt, Delete, Enter and the letters, and keep a way back
  that works with the mouse alone.
- **Testable core.** `KeyAssign.Core` holds the logic behind interfaces
  (registry, hooks, system parameters) with fakes for tests, so tests never touch
  the real keyboard configuration.
- **Bilingual.** English and Spanish UI and docs from v1.0.

## Milestones

### 0. Skeleton (done)

- Solution: WPF app, Core library, xUnit tests.
- `ScancodeMap` encoder and decoder for the registry value, with tests.
- README, LEEME, this roadmap, MIT license.

### 1. Read-only inventory

- Show current `Scancode Map` remaps, installed layouts and keyboard settings.
- Registry and system-parameter abstraction with real and fake implementations.

### 2. Key-to-key remaps (MVP)

- Edit remaps in a table and by "press the key" capture.
- Write `Scancode Map` with an elevation prompt, after a snapshot.
- Restore one remap or all, and explain that changes apply at next sign-in.

### 3. Layouts and settings

- Add, remove and order layouts; set the switching shortcut.
- Repeat delay and rate, Sticky/Filter keys, Caps Lock behavior.

### 4. Shortcuts and per-app rules

- A background hook (tray app, optional start at sign-in) for combinations and
  per-app assignments, which `Scancode Map` can't express.

### 5. Ship v1.0

- Installer, smoke-test checklist, English and Spanish UI, landing page at
  `keyassign.ilecto.app`.

## Open decisions

- **D1. MVP mechanism:** `Scancode Map` only (simple, no background process, but
  needs admin and a sign-out), or a hook from the start (instant and per user,
  but always running)?
- **D2. Relation to PowerToys Keyboard Manager**, which already offers hook-based
  remaps: complement it (registry remaps, layouts, settings), or compete with it?
- **D3. Custom layouts:** generate layout DLLs (as MSKLC does), or only manage
  installed ones?
- **D4. Packaging:** unpackaged with an installer (as planned for Extension
  Icons), or MSIX?
