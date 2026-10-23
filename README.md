# RecentFileCacheParser

Parses `RecentFileCache.bcf` files (Windows 7 and Server 2008 R2, `C:\Windows\AppCompat\Programs\RecentFileCache.bcf`),
which list executables that ran recently. The header holds a CRC32 of the rest of the file, which is checked: a mismatch
means the file was modified or damaged. Results are shown on the console and can be exported to CSV and JSON.

| | |
|---|---|
| **Version** | 2026.10.1 |
| **Download** | [.NET 10](https://download.ericzimmermanstools.com/net/RecentFileCacheParser.zip) (recommended) &middot; [.NET Framework 4.7.2](https://download.ericzimmermanstools.com/RecentFileCacheParser.zip) |
| **All tools** | [ericzimmerman.github.io](https://ericzimmerman.github.io) or [Get-ZimmermanTools](https://download.ericzimmermanstools.com/Get-ZimmermanTools.zip), which downloads them all and keeps them current |
| **Changes** | [ChangeLog.txt](https://download.ericzimmermanstools.com/ChangeLog.txt) |
| **Issues** | [EricZimmerman/RecentFileCacheParser](https://github.com/EricZimmerman/RecentFileCacheParser/issues) or [EricZimmerman/Issues](https://github.com/EricZimmerman/Issues/issues) |

## Requirements

- **.NET 10 build**: the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Run `RecentFileCacheParser.exe`
  on Windows, or `dotnet RecentFileCacheParser.dll` on any system with the runtime.
- **.NET Framework build**: .NET Framework 4.7.2 or later (Windows only).

## Command line

```text
Usage:
  RecentFileCacheParser [options]

Options:
  -f <f>          File to process. Required
  --csv <csv>     Directory to save CSV formatted results to. Be sure to include the full path in double quotes
  --csvf <csvf>   File name to save CSV formatted results to. When present, overrides default name
  --json <json>   Directory to save json representation to. Use --pretty for a more human readable layout
  --pretty        When exporting to json, use a more human readable layout
  -q              Only show the filename being processed vs all output. Useful to speed up exporting to JSON and/or CSV
  -?, -h, --help  Show help and usage information
  --version       Show version information
```

Short options (a single letter) start with one dash, long options with two.

## Examples

```text
RecentFileCacheParser.exe -f "C:\Temp\RecentFileCache.bcf" --csv "c:\temp"
RecentFileCacheParser.exe -f "C:\Temp\RecentFileCache.bcf" --json "D:\jsonOutput" --pretty
```

## Output

- **Console**: the source file's time stamps, the checksum and whether it is valid, every file name, and any parse
  problems (`-q` shows only the file name).
- **CSV** (`--csv`): one row per file name, with the columns `SourceFile`, `SourceCreated`, `SourceModified`,
  `SourceAccessed` and `Filename`. Named `<timestamp>_RecentFileCacheParser_Output.csv` unless `--csvf` is given.
- **JSON** (`--json`): the whole parsed file (`<timestamp>_<file name>.json`): the source file and its time stamps, the
  stored and calculated checksums, the file names and the parse problems. Time stamps are in ISO 8601 UTC
  (`2024-01-02T03:04:05.6780000Z`).

Damaged files are parsed as far as possible: a checksum mismatch, a bad entry length, a missing terminator and trailing
bytes are reported, and every entry that can be read is still listed and exported.

## RecentFileCache NuGet package

The parser is also available as the [RecentFileCache](https://www.nuget.org/packages/RecentFileCache) NuGet package:

```csharp
var rfc = RecentFileCache.RecentFileCache.LoadFile(@"C:\Temp\RecentFileCache.bcf");
foreach (var name in rfc.FileNames) Console.WriteLine(name);
// rfc.ChecksumValid, rfc.ParseErrors
```

`LoadFile` throws `RecentFileCacheParseException` when the data is too small or has the wrong signature; other problems
are listed in `ParseErrors`.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. A damaged file, or a file that isn't a RecentFileCache.bcf, is reported but is not a failure |
| `1` | The tool failed: a missing `-f` or input file, a file it can't read (access denied, or locked by another process: reported as "Unable to read"), or a CSV or JSON export that can't be written |

## What's new in 2026.10

- The file's checksum is checked, and shown with whether it is valid.
- Damaged entries are reported and the readable ones kept; empty files no longer crash; parse problems are shown.
- JSON time stamps are ISO 8601 UTC (`2024-01-02T03:04:05.6780000Z`) instead of `\/Date(milliseconds)\/`.
- A file that can't be read (for example locked by another process) is reported as "Unable to read" instead of asking
  for the file to be sent in.
- Exit code 1 when the tool fails (it was always 0). Error reports include source file and line numbers.

The full list is in the [ChangeLog](https://download.ericzimmermanstools.com/ChangeLog.txt).

## Building

```text
dotnet build RecentFileCacheParser.slnx -c Release
```

Builds the CLI for `net10.0` and `net472`, and the `RecentFileCache` library (`netstandard2.0`), which is published as
the [RecentFileCache](https://www.nuget.org/packages/RecentFileCache) NuGet package.

## Special thanks

Open source development funding and support provided by the [SANS Institute](https://www.sans.org/) and
[SANS DFIR](https://www.sans.org/digital-forensics-incident-response/).

## License

[MIT](LICENSE), Copyright (c) Eric R. Zimmerman 2027.
