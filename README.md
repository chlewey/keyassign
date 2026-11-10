# Key Assign

*[Leer en español](LEEME.md)*

A Windows 11 keyboard layout editor. Change what each key types, graphically
or in a text file; add dead keys and whole layers on AltGr, such as Cyrillic or
Greek, from prebuilt packs; and install the result in place of the layout you
use, with every change reversible.

**Status:** skeleton. The app opens a placeholder window and changes nothing on
your machine yet. See [docs/ROADMAP.md](docs/ROADMAP.md) for the plan and
[docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) for the requirements. Project page:
<https://keyassign.ilecto.app>.

## Build

Requires Windows 10 or 11 and the .NET 8 SDK.

```
dotnet build
dotnet test
dotnet run --project src/KeyAssign.App
```

## Layout

- `src/KeyAssign.App/` — the WPF desktop app.
- `src/KeyAssign.Core/` — keyboard logic with no UI, such as the `Scancode Map`
  registry format.
- `tests/KeyAssign.Core.Tests/` — xUnit tests for Core.
- `docs/` — roadmap and design notes.
- `site/` — the project page, a static Cloudflare Workers site (see
  [site/README.md](site/README.md)).

## License

[MIT](LICENSE). A project by [Interlecto](https://ilecto.app).
