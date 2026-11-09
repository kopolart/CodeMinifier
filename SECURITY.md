# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | Yes       |

Only the latest release receives security fixes. Upgrade to the newest version before reporting an issue.

## Reporting a vulnerability

**Do not open a public issue for a vulnerability.**

Use GitHub's private vulnerability reporting:

1. Open the repository's **Security** tab.
2. Select **Report a vulnerability**.
3. Describe the problem and include a proof of concept if possible.

Please include:

- The application version and Windows version.
- Input that triggers the problem (the smallest snippet that reproduces it).
- The observed impact — crash, hang, data loss, corrupted output, or worse.
- Any suggested fix or mitigation.

You will receive an acknowledgement as soon as possible, normally within seven days. Please allow time for a fix before public disclosure.

## Scope

Code Minifier Pro is a local desktop application:

- It does **not** connect to the network.
- It reads only files you explicitly open or drop onto the window.
- It writes only files you explicitly save.
- It has **no** third-party runtime dependencies.

Reports that are in scope include:

- A crash, hang, or excessive memory use caused by untrusted input.
- Output that is silently corrupted in a way that can break a site or application.
- Unsafe file or clipboard handling.
- Supply-chain concerns in the build or release workflow.

Out of scope:

- Issues that require an already-compromised machine.
- Vulnerabilities in .NET Framework or Windows itself (report those to Microsoft).
- Missing features such as input validation for syntactically invalid code.
