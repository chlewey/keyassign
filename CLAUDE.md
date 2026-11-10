# CLAUDE.md

Key Assign: a Windows 11 app to change keyboard settings and assign keys
(remaps, disabled keys, layouts, keyboard settings), with every change
reversible. See `README.md` (English), `LEEME.md` (Spanish) and
`docs/ROADMAP.md`, whose open decisions D1–D4 are the owner's to make.

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
