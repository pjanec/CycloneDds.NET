# Changelog

All notable changes to CycloneDDS.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 0.1.24

### Added
- Initial public release of CycloneDDS.NET
- Zero-allocation write path with custom marshaller
- Zero-copy read path using `ref struct` views
- Code-first schema DSL with attributes (`[DdsTopic]`, `[DdsKey]`, `[DdsStruct]`)
- Automatic IDL generation from C# types
- IDL import tool for converting existing IDL to C# DSL
- Async/await support with `WaitDataAsync`
- Client-side filtering with compiled predicates
- Sender tracking (Computer, PID, custom app ID)
- NuGet package with bundled native binaries and build tools
- Automatic C# code generation during build
- Support for keyed topics with O(1) instance lookup
- Full interoperability with other DDS implementations

### Changed

### Deprecated

### Removed

### Fixed

### Security

---

## Release Notes

### Version Numbering

This project uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) for automatic semantic versioning based on git tags and commit history.

### How to Read This Changelog

- **Added** - New features
- **Changed** - Changes in existing functionality
- **Deprecated** - Soon-to-be removed features
- **Removed** - Removed features
- **Fixed** - Bug fixes
- **Security** - Vulnerability fixes

[Unreleased]: https://github.com/pjanec/CycloneDds.NET/compare/HEAD...HEAD
