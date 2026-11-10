# Key Assign

*[Read in English](README.md)*

Un editor de distribuciones de teclado para Windows 11. Cambia lo que escribe
cada tecla, en modo gráfico o en un archivo de texto; añade teclas muertas y
capas completas en AltGr, como cirílico o griego, desde paquetes ya hechos; e
instala el resultado en lugar de la distribución que usas, con todos los cambios
reversibles.

**Estado:** esqueleto. La aplicación abre una ventana provisional y todavía no
cambia nada en el equipo. El plan está en [docs/ROADMAP.md](docs/ROADMAP.md) y los
requisitos en [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md), ambos en inglés.

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
