# Key Assign

*[Read in English](README.md)*

Una aplicación para Windows 11 que cambia la configuración del teclado y asigna
teclas: reasigna una tecla a otra, desactiva teclas que se pulsan por error y
administra las distribuciones y opciones del teclado desde un solo lugar, con
todos los cambios reversibles.

**Estado:** esqueleto. La aplicación abre una ventana provisional y todavía no
cambia nada en el equipo. El plan está en [docs/ROADMAP.md](docs/ROADMAP.md) (en
inglés).

## Compilar

Requiere Windows 10 u 11 y el SDK de .NET 8.

```
dotnet build
dotnet test
dotnet run --project src/KeyAssign.App
```

## Estructura

- `src/KeyAssign.App/`: la aplicación de escritorio WPF.
- `src/KeyAssign.Core/`: la lógica del teclado sin interfaz, como el formato del
  valor de registro `Scancode Map`.
- `tests/KeyAssign.Core.Tests/`: pruebas xUnit de Core.
- `docs/`: hoja de ruta y notas de diseño.

## Licencia

[MIT](LICENSE). Un proyecto de [Interlecto](https://ilecto.app).
