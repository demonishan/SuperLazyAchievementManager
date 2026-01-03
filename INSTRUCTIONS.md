# Instructions for Super Lazy Achievement Manager (SLAM)

## Project Overview
- **SLAM** is a modern C#/WPF desktop app for managing Steam achievements, based on the original Steam Achievement Manager.
- The solution contains two main projects:
  - `SAM.API`: Low-level Steam API interop and wrappers (unsafe code, P/Invoke, structs for Steam interfaces).
  - `SAM.Reborn`: WPF GUI, user interaction, and business logic. References `SAM.API`.

## Architecture & Patterns
- **Interop Layer**: `SAM.API/Interfaces.cs` defines structs mapping to Steam interfaces. Many fields are `IntPtr` for function pointers.
- **Suppressions**: `SAM.API/GlobalSuppressions.cs` disables many code analysis warnings due to necessary unsafe/interop patterns.
- **Resource Management**: Embedded resources (icons, images) are linked in `.csproj` and loaded at runtime.
- **Fody/Costura**: Used for IL weaving and embedding dependencies (see `FodyWeavers.xml`).
- **Build Output**: Main executable is `SLAM.exe` in `SAM.Reborn/bin/Release/net48/` and is copied to `Dist/` after build.

## Developer Workflows
- **Build**: Always run a build to test for any error. Use the VS Code task "Build and Copy EXE to Dist" or run:
  ```powershell
  dotnet build -c Release
  ```
  This also stops any running `SLAM` process before building.
- **Run**: Execute `Dist/SLAM.exe` (Steam must be running and user logged in).
- **Dependencies**: .NET Framework 4.8, Costura.Fody (for single-file output).
- **CI**: AppVeyor config in `.appveyor.yml` (uses Visual Studio 2019, restores NuGet, builds solution, caches packages).

## Conventions & Practices

### Behavior
- **No Questions**: Do not ask clarifying questions. Proceed with the best possible implementation based on the request.
- **No Comments**: Do not add comments to the code.

### Code Style
- **Curly Braces**: Opening curly braces must be on the same line as the statement (K&R style).
- **Single-Statement If**: Do not use braces for single-statement `if` blocks.
- **No Extra Newlines**: Do not use consecutive blank lines. Keep code compact.

### Technical Constraints
- **Interop**: Use `[StructLayout(LayoutKind.Sequential, Pack = 1)]` for all Steam interface structs.
- **Unsafe Code**: Permitted in `SAM.API` for pointer operations.
- **Suppressions**: Maintain `GlobalSuppressions.cs` entries; they are required for interop.
- **Platform**: Target x86 only (Steam client requirement).
- **Resource Linking**: Link images/icons via `.csproj` and load as resources.
- **No Tests**: Do not create or look for automated tests.

## Key Files & Directories
- `SAM.API/Interfaces.cs`: Steam API interface definitions.
- `SAM.API/GlobalSuppressions.cs`: Code analysis suppressions for interop.
- `SAM.Reborn/MainWindow.xaml`: Main WPF UI.
- `SAM.Reborn/SAM.Picker.csproj`: GUI project file, resource linking.
- `Dist/`: Final output location for `SLAM.exe`.
- `.appveyor.yml`: CI build config.
- `INFO.md`: Do NOT read this file.

## External Integration
- **Steam**: Requires Steam client running for functionality.
- **No direct Steam API keys**: All access is via local client interop.

---
For further details, see `README.md` and project files. When in doubt, follow existing interop and resource patterns.
