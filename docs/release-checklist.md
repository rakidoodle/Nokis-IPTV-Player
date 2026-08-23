# Release checklist

Version 1.0 release acceptance uses a clean, restored Release build rather than reusing Debug output.

1. Confirm the worktree contains no credentials, databases, logs, or unexpected files.
2. Run `dotnet clean MyIPTV.sln -c Release`.
3. Run `dotnet restore MyIPTV.sln`.
4. Run `dotnet build MyIPTV.sln -c Release --no-restore` and require zero warnings.
5. Run `dotnet test MyIPTV.sln -c Release --no-build` and require all tests to pass.
6. Run the NuGet transitive vulnerability audit.
7. Publish the self-contained `win-x64` profile and create its checksum.
8. Launch the published executable and visually verify startup before distributing it.

Build version metadata is centralized in `Directory.Build.props`; packaging reads the same `1.0.0` version.
