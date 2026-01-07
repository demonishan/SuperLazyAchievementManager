
# Contributing to Super Lazy Achievement Manager (SLAM)

Thank you for your interest in contributing! Please follow these guidelines to help me maintain a high-quality, secure, and modern project.

## Workflow

1. **Fork the repository** and create your branch from `master`.
2. **Follow the Development Protocol** for all code changes:
   - Bump the version in `SAM.Picker.csproj` if releasing a new version.
   - Ensure the solution builds with zero errors (`dotnet build -c Release`).
   - CI/CD: Your Pull Request will be automatically built and verified by GitHub Actions. Ensure the checks pass.
   - Stage, commit, and push with a clear message.
3. **Keep code minimal and concise**:
   - Write clean, readable C# code.
   - Avoid unnecessary comments, whitespace, or variables.
   - Follow standard C# naming conventions.
4. **UI/UX**:
   - Follow the existing WPF/XAML design patterns.
   - Ensure the UI remains responsive and accessible.
5. **Security**:
   - Never commit sensitive data or config files (like `config.json`).
   - Review dependencies for security risks.
   - See [SECURITY.md](SECURITY.md) for more details.
6. **Pull Requests**:
   - Reference related issues if any.
   - Describe your changes clearly and concisely.
   - Ensure your branch is up to date with `master` before submitting.

## Code Style

- Target Framework: .NET Framework 4.8.
- Use strict typing and avoid `dynamic` where possible.
- Consistent formatting (braces on new lines, etc.).

## Communication

- Be respectful and constructive in all discussions.
- Ask questions if unsure about the protocol or implementation.

Thank you for helping make SLAM better!
