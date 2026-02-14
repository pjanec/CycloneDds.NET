# Contributing to CycloneDDS.NET

Thank you for your interest in contributing to CycloneDDS.NET! We welcome contributions from the community.

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Windows (currently the primary development platform)
- PowerShell (for build scripts)

### Building the Project

1. Clone the repository with submodules:
   ```bash
   git clone --recursive https://github.com/pjanec/CycloneDds.NET.git
   cd CycloneDds.NET
   ```

2. Build the native Cyclone DDS libraries (one-time setup):
   ```powershell
   .\build_cyclone.bat
   ```

3. Build and pack the NuGet packages:
   ```powershell
   .\build\pack.ps1
   ```

4. Run tests:
   ```powershell
   dotnet test
   ```

### Project Structure

- `src/` - Core C# source code
  - `CycloneDDS.Core/` - Core DDS functionality
  - `CycloneDDS.Runtime/` - Runtime marshalling and native interop
  - `CycloneDDS.Schema/` - Schema definition attributes
- `tools/` - Build-time code generation tools
- `tests/` - Unit and integration tests
- `cyclonedds/` - Native Cyclone DDS submodule
- `build/` - Build scripts

## Making Changes

### Pull Request Process

1. **Fork the repository** and create your branch from `main`:
   ```bash
   git checkout -b feature/my-new-feature
   ```

2. **Make your changes** following the coding standards:
   - Follow existing code style and conventions
   - Add XML documentation comments for public APIs
   - Include unit tests for new functionality
   - Ensure all tests pass

3. **Commit your changes** with clear, descriptive messages:
   ```bash
   git commit -m "Add feature: description of what you added"
   ```

4. **Push to your fork** and submit a pull request:
   ```bash
   git push origin feature/my-new-feature
   ```

5. **Describe your changes** in the PR description:
   - What problem does this solve?
   - How did you test it?
   - Are there any breaking changes?

### Coding Standards

- Use C# idiomatic patterns and modern language features
- Follow Microsoft's C# Coding Conventions
- Keep performance in mind - this is a high-performance library
- Write clear, self-documenting code with appropriate comments
- Add XML documentation for public APIs

### Testing

- Write unit tests for new functionality
- Ensure existing tests still pass
- For performance-critical code, consider adding benchmarks
- Test with both zero-copy and managed allocation paths where applicable

## Reporting Issues

When reporting issues, please include:
- .NET SDK version
- Operating system and version
- Steps to reproduce the issue
- Expected vs actual behavior
- Any relevant error messages or stack traces

## Code of Conduct

- Be respectful and inclusive
- Provide constructive feedback
- Focus on what is best for the project and community

## Questions?

If you have questions about contributing, feel free to:
- Open an issue for discussion
- Refer to the documentation in the `docs/` directory
- Check existing issues and PRs

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
