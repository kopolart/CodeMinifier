# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-11-08

### Added

- CSS minifier: comment removal with `/*! ... */` preservation, whitespace collapsing, trailing-semicolon removal, zero-length unit stripping, leading-zero removal, hex color shortening and lowercasing, `calc()`/`min()`/`max()`/`clamp()`/`env()`/`var()` minification, and protection for strings, `url(...)`, custom properties, and selector text.
- JavaScript minifier: comment removal with `/*! ... */` preservation, whitespace collapsing, automatic-semicolon-insertion safety, and verbatim handling of strings, template literals, and regex literals.
- Tabbed Windows Forms interface with one tab per language.
- File open, save, copy, clear, and drag & drop support.
- Live statistics: line counts, UTF-8 byte sizes, and percentage saved.
- Custom editor control without the default 64 KB text limit, with a warning for files over 50 MB.
- UTF-8 output without a byte-order mark.
- GitHub Actions continuous integration and tag-driven release packaging.

[Unreleased]: https://github.com/kopolart/CodeMinifier/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/kopolart/CodeMinifier/releases/tag/v1.0.0
