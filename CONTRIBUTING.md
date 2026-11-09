# Contributing to Code Minifier Pro

Thanks for taking the time to improve the project. This guide covers the workflow, the conventions used in the codebase, and what a good pull request looks like.

## Before you start

- Search [existing issues](../../issues) to avoid duplicating work.
- For anything larger than a small fix, open an issue first so the approach can be agreed on.
- Security vulnerabilities must not be reported in a public issue — follow [SECURITY.md](SECURITY.md).

## Development setup

1. Install **Visual Studio 2022** with the *.NET desktop development* workload (this includes the .NET Framework 4.8 targeting pack and MSBuild).
2. Clone the repository:
   ```powershell
   git clone https://github.com/kopolart/CodeMinifier.git
   cd CodeMinifier
   ```
3. Open `CodeMinifier.slnx` in Visual Studio 2022 (17.13+) **or** build from a Developer Command Prompt:
   ```powershell
   msbuild CodeMinifier\CodeMinifier.csproj /p:Configuration=Release
   ```
4. Run `CodeMinifier\bin\Release\CodeMinifier.exe`.

## Project conventions

- **C# 7.3, .NET Framework 4.8, BCL only.** Do not add NuGet packages or third-party libraries.
- **Stay offline.** The application must not make network calls, phone home, or add telemetry.
- **One minifier per language.** Implement `IMinifier` in `CodeMinifier/Minification/` and register a tab in `CodeMinifier/UI/MainForm.cs`.
- **Minifiers are single-pass scanners.** Avoid regex-only rewriting of code content; protected constructs (strings, URLs, template literals, regex literals, custom properties) must be scanned explicitly.
- **Prefer correctness over compression.** If a rule is ambiguous, keep the safe form and document the trade-off in the class header.
- **Match the existing style.** Four-space indentation, Allman braces, `_camelCase` private fields, and XML documentation on public types and members.
- **No designer files.** Build UI in code.
- Keep changes focused. Do not reformat unrelated code in the same pull request.

## Adding a language

1. Implement `IMinifier` in `CodeMinifier/Minification/` with a unique `Name`.
2. Register a tab in `MainForm` with the accent color, labels, file filter, and default extension.
3. Add the source file to `CodeMinifier/CodeMinifier.csproj`.
4. Verify the minifier against samples that contain strings, comments, license comments, and the ambiguous constructs of that language.
5. Update `README.md` (features and rules) and `CHANGELOG.md`.

## Testing

There is no automated test project yet, so pull requests must include the manual checks that were performed. At minimum, verify:

- [ ] The solution builds in **Release** with no new warnings.
- [ ] CSS: ordinary rules, `@media`, `@font-face`, `calc()`, custom properties, data URIs, strings containing `{}` or `//`.
- [ ] CSS: `a :hover` and `a:hover` remain different, and `#ABCDEF` selectors are not altered.
- [ ] JavaScript: semicolon-less code, `return` on its own line, regex literals after `if (...)`, division, template literals with `${ ... }`.
- [ ] Large input (a few MB) still completes and reports correct statistics.
- [ ] Open, drag & drop, save (UTF-8 without BOM), and copy all work.
- [ ] The application makes no network requests.

## Pull requests

- Use a descriptive title, for example `fix(css): preserve units for zero percentages`.
- Keep commits small and explain *why* the change is needed.
- Complete the pull request template, including how the change was tested.
- Add a `CHANGELOG.md` entry under `Unreleased` for user-visible changes.
- Update documentation when behavior changes.

A maintainer will review the change. Review comments are about the code, not the contributor.

## Versioning

The project follows [Semantic Versioning](https://semver.org/). Releases are created by pushing a tag such as `v1.1.0`; the release workflow builds and publishes the ZIP automatically. Version numbers in `Properties/AssemblyInfo.cs` should be updated in the same pull request that prepares a release.
