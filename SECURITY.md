
# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in **Super Lazy Achievement Manager (SLAM)**, please report it privately by opening a private issue on GitHub. Do not disclose security issues publicly until they have been reviewed and patched.

- I will respond as quickly as possible to your report.
- Once the vulnerability is confirmed, I will work to release a fix promptly and credit the reporter if desired.

## Supported Versions

Only the latest version of SLAM is supported with security updates. Please update to the latest version before reporting issues.

## Best Practices

- **Do not share sensitive Steam information (like SteamID or login tokens) in public issues or pull requests.**
- **Keep dependencies up to date.**
- **Review third-party packages for security risks before use.**
- **Never commit personal config files (like `config.json`) to the repository.**
- **Monitor for vulnerabilities in NuGet packages.**
- **Patch and disclose vulnerabilities responsibly.**

## API & Data Security

- This application interacts directly with the local Steam Client via `SAM.API`.
- User data is never stored remotely or transmitted to third-party services by SLAM itself.
- External links (SteamDB, SteamHunters, etc.) are opened in the default browser and are subject to their own privacy policies.

## Dependency Management

- Use only well-maintained, trusted NuGet packages.
- Regularly audit dependencies for vulnerabilities.

## Protocol Compliance

- Always follow the guidelines in `INSTRUCTIONS.md` to maintain code quality.

Thank you for helping keep SLAM and its users safe!