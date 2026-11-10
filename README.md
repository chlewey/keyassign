# Key Assign

*[Leer en español](LEEME.md)*

A Windows 11 app to change keyboard settings and assign keys: remap a key to
another, disable keys you hit by accident, and manage layouts and keyboard
settings from one place, with every change reversible.

**Status:** skeleton. The app opens a placeholder window and changes nothing on
your machine yet. See [docs/ROADMAP.md](docs/ROADMAP.md) for the plan.

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

## License

[MIT](LICENSE). A project by [Interlecto](https://ilecto.app).
