# Copilot Instructions for Super Lazy Achievement Manager (SLAM)

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

## Developer Workflows
- **Build**: Always run a build to test for any error:
  ```powershell
  dotnet build -c Release
  ```
## Release Protocol (GitHub Actions)
The project uses GitHub Actions for automated building and releasing.

### 1. Verification Build (Manual)
To verify the code compiles without creating a public release:
- Go to the GitHub Repo -> **Actions** tab.
- Select **Build and Release** workflow.
- Click **Run workflow** (Blue button).
- Result: Green checkmark badge on README (no public release created).

### 2. Official Release (Publish)
To publish a new version to users:
1. Update version number (e.g., `8.6.11`) in:
   - `SAM.Reborn/SAM.Picker.csproj` (`<AssemblyVersion>`)
   - `CHANGELOG.md` (Add new section)
   - `VERSION_HISTORY.md` (if maintained)
2. Commit and push the changes:
   ```powershell
   git add .
   git commit -m "chore: bump version to 8.6.11"
   git push
   ```
3. Create and push a tag to trigger the release:
   ```powershell
   git tag v8.6.11
   git push origin v8.6.11
   ```
- **Result**: GitHub automates the build, creates a "Release" page, and attaches `SLAM.exe`.

--
- **CI**: AppVeyor config in `.appveyor.yml` (uses Visual Studio 2019, restores NuGet, builds solution, caches packages).

## Conventions & Practices

### Behavior
- **No Questions**: Do not ask clarifying questions. Proceed with the best possible implementation based on the request.
- **No Comments**: Do not add comments to the code.

### Code Style
- **Curly Braces**: Opening curly braces must be on the same line as the statement (K&R style).
- **Single-Statement If**: Do not use braces for single-statement `if` blocks.
- **No Extra Newlines**: Do not use consecutive blank lines. Keep code compact.
- **Struct Formatting**: Do not keep multiple fields on the same line. Break lines to avoid multiple closing semicolons in one line.

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
- `.appveyor.yml`: CI build config.
- `INFO.md`: Do NOT read this file.

## External Integration
- **Steam**: Requires Steam client running for functionality.
- **No direct Steam API keys**: All access is via local client interop.

---
For further details, see `README.md` and project files. When in doubt, follow existing interop and resource patterns.