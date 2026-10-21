# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Two deliverables in one repo, part of Eric Zimmerman's DFIR tool suite:
- **`RecentFileCache`**: a `netstandard2.0` NuGet library that parses Windows 7 / 2008 R2 `RecentFileCache.bcf` files. Released to NuGet from a GitHub release.
- **`RecentFileCacheParser`**: the CLI (`net472;net10.0`), which references the library as a project (not the package) and displays or exports to CSV/JSON. Built, signed and staged manually; no GitHub release.

Input may be corrupt or hostile: parse everything that is there and report problems rather than giving up.

## Commands

The SDK is pinned in `global.json` (.NET 10). All projects use `LangVersion 10`, so avoid newer C# syntax. Tests use NUnit 5 + FluentAssertions 7 (pinned to `[7.0.0,8.0.0)`, since 8.x requires a commercial license).

```bash
dotnet build -c Release                                    # builds RecentFileCacheParser.slnx (library, CLI net472+net10.0, tests)
dotnet test -c Release                                     # all tests
dotnet test --filter "FullyQualifiedName~RobustnessTests"  # one fixture
dotnet test --filter "Name=ParsesSample"                   # one test
dotnet pack -c Release                                     # library package (README.md and icon.png from the repo root)
dotnet run --project RecentFileCacheParser -f net10.0 -- -f RecentFileCache.Test/Files/RecentFileCache.bcf --csv out
```

## Format and library

- 20 byte header: signature `0xFFEEFFFE` at 0x00, three unknown DWORDs (0x2211, 3, 1 in every sample), and at 0x10 a CRC32 (zlib/IEEE) of the whole file with those 4 bytes left out. Then entries to the end of the file: an Int32 character count, UTF-16LE characters, a NUL character. There is no entry count, so a bad length means the rest can't be found.
- `RecentFileCache.LoadFile(path)` / `new RecentFileCacheFile(bytes, sourceFile)`. Source file times are read from disk only when `sourceFile` exists.
- **Error model** (same as the Prefetch library): `RecentFileCacheParseException` (with `Offset` and `Field`) is thrown only when the data isn't usable (smaller than the header or a bad signature). After that the constructor never throws: a checksum mismatch, a bad length, a missing or non-NUL terminator and trailing bytes go to `ParseErrors` (`ParsingError` is true when it isn't empty), and every readable entry is still in `FileNames`. Check lengths against the remaining bytes before reading or allocating.
- `Crc32` is internal (`InternalsVisibleTo` the tests).
- JSON export serializes `RecentFileCacheFile` with ServiceStack.Text, so new public properties appear in the JSON. Add them after the existing ones.

## CLI

All in `RecentFileCacheParser/Program.cs`. `Main` awaits `InvokeAsync` on a `System.CommandLine` `RootCommand` and returns `DoWork`'s exit code: 0 on success (including a file parsed with warnings), 1 for a missing `-f`, a missing file, an unusable file or a failed export. To add an option: create the `Option<T>`, add it to the `RootCommand` initializer and pass `result.GetValue(...)` into `DoWork`. `CsvOut` defines the CSV columns; keep the existing ones in order and add new ones at the end.

## Tests

`RecentFileCache.Test` (net10.0) references both the library and the CLI:
- `RecentFileCacheTests`: assertions on the real samples in `Files/` (copied to the test output).
- `RobustnessTests`: in-memory files from the `Build`/`Entry` helpers (which reproduce `RecentFileCache 2.bcf` byte for byte), covering every malformed case plus random corruption. Add regression tests here.
- `CliTests`: runs the built `RecentFileCacheParser.dll` with `dotnet` as a separate process, for exit codes and CSV/JSON output.

## Versioning and release

- **Library:** MinVer sets the version from the git tag (bare tags like `2026.10.0`); there is no `<Version>` in its csproj. Untagged commits build as `x.y.z-alpha.0.N`. To release: push to `master`, then `git tag <ver> && git push origin <ver> && gh release create <ver> --verify-tag --generate-notes`.
- **CLI:** `<Version>` in `RecentFileCacheParser/RecentFileCacheParser.csproj`, bumped by hand per release and shown in the banner.
- Versions are `yyyy.mm.x`.
- `.github/workflows/publish.yml` (identical to GuidMapping's) packs, validates and tests on PRs and pushes to `master`, and pushes to NuGet.org when a GitHub Release is published, using trusted publishing (`NuGet/login` with the `NUGET_USER` repo variable).
- Dependabot (`.github/dependabot.yml`) opens monthly PRs for GitHub Actions and NuGet updates.
- Release builds are staged, signed and zipped by `Build-ZimmermanTools.ps1` in the get-zimmermantools repository (cloned next to this one). Its program table says what goes in this tool's download zip (the files taken from the build, folders copied from the repositories, files kept from the current release zip); a new output file, a content folder or a change in how the tool is built (single file, another framework) needs that entry updated.
- pdbs are embedded in the assemblies (`DebugType embedded`, `EmbedUntrackedSources`) and Release builds set `ContinuousIntegrationBuild` (source paths `/_/...`), both in `Directory.Build.props`: stack traces and Exceptionless reports have file and line numbers, and no .pdb file ships. Don't add `DebugType` (or `DebugSymbols`, `IncludeSymbols`, snupkg) to project files.
