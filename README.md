# RecentFileCacheParser

## Command Line Interface

    RecentFileCacheParser version 1.0.0.0
    
    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/RecentFileCacheParser
    
            f               File to process. Required
            q               Only show the filename being processed vs all output. Useful to speed up exporting to json and/or csv
    
            csv             Directory to save CSV formatted results to. Be sure to include the full path in double quotes
            csvf            File name to save CSV formatted results to. When present, overrides default name
            json            Directory to save json representation to. Use --pretty for a more human readable layout
            pretty          When exporting to json, use a more human readable layout
    
    
    Examples: RecentFileCacheParser.exe -f "C:\Temp\RecentFileCache.bcf" --csv "c:\temp"
              RecentFileCacheParser.exe -f "C:\Temp\RecentFileCache.bcf" --json "D:\jsonOutput" --pretty
    
              Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes

## Documentation

Parses RecentFileCache.bcf files (Windows 7 and Server 2008 R2, `C:\Windows\AppCompat\Programs\RecentFileCache.bcf`), which list executables that ran recently.

The header holds a CRC32 of the rest of the file. RecentFileCacheParser checks it and reports a mismatch, which means the file was modified or damaged. Damaged entries are reported and every entry that can be read is still listed.

Exit code 0 means success. Exit code 1 means invalid arguments, a file that is not a RecentFileCache.bcf file, or a failed export.

The .NET 10 build runs on Linux and macOS.

## RecentFileCache NuGet package

The parser is also available as the [RecentFileCache](https://www.nuget.org/packages/RecentFileCache) NuGet package:

```csharp
var rfc = RecentFileCache.RecentFileCache.LoadFile(@"C:\Temp\RecentFileCache.bcf");
foreach (var name in rfc.FileNames) Console.WriteLine(name);
// rfc.ChecksumValid, rfc.ParseErrors
```

`LoadFile` throws `RecentFileCacheParseException` when the data is too small or has the wrong signature; other problems are listed in `ParseErrors`.

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). 
# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
