# CLAUDE.md

Key Assign: a Windows 11 keyboard layout editor, graphical and by text file,
with AltGr dead keys, layers (one-shot and sustained), prebuilt packs, and
transparent install over the layout in use. See `README.md` (English),
`LEEME.md` (Spanish), `docs/REQUIREMENTS.md` (requirement IDs R1.1–R7.5, spikes
S1–S5) and `docs/ROADMAP.md` (milestones M0–M9; its open decisions D1–D4 are the
owner's to make). Cite requirement IDs in commits and PRs.

- **Never modify Windows' own layout DLLs** in `System32`. Key Assign installs
  its own layout and swaps it into the input list (R4.3).
- Spikes that install layouts run on a throwaway VM, never on the main machine.

Rules shared by all Interlecto projects (branches, deployment, visual identity, how to continue from another machine or account) are in the hub's [CLAUDE.md](https://github.com/Interlecto/ilecto/blob/master/CLAUDE.md). Where they differ, this file wins.

## Stack

- C# / .NET 8 (`net8.0-windows`), WPF. Same stack as Extension Icons
  (`chlewey/extension-icons`), the other Interlecto Windows app; reuse its
  patterns, such as snapshots before any registry write.
- xUnit tests with coverlet.

## Layout

- `src/KeyAssign.App/` — WPF app. Placeholder window only.
- `src/KeyAssign.Core/` — logic without UI. `Keys/ScancodeMap.cs` encodes and
  decodes the `Scancode Map` registry value; it doesn't touch the registry.
- `tests/KeyAssign.Core.Tests/` — tests for Core.
- `site/` — project page for `keyassign.ilecto.app`: static, bilingual (ES at
  `/`, EN at `/en/`), no build step, system fonts and nothing loaded from other
  sites (R7.2). Same structure as Contarte's. Deploy from `site/` with
  `npx wrangler deploy` (worker `keyassign-site`), only when asked. Keep both
  languages and its progress section in step with `docs/ROADMAP.md`. The app
  icon is `site/public/favicon.svg`: four keys, second row shifted a quarter
  key right.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/KeyAssign.App
```

## Conventions

- Default branch is `master`. Roadmap work goes on `develop/<topic>` branches
  that merge to `master` by pull request. Commit and push only when asked.
- Keep `README.md` and `LEEME.md` in step.
- **Never change the real keyboard configuration from tests or experiments.**
  `Scancode Map` is machine-wide and can lock a user out. Put registry, hook and
  system-parameter access behind interfaces with fakes, and test against those.
  For a live check, use a throwaway VM or snapshot first and know how to delete
  the value.
- `Nullable` and `ImplicitUsings` are enabled in all projects.
