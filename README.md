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
              RecentFileCacheParser.exe -f "C:\Temp\RecentFileCache.bcf" --json "D:\jsonOutput" --jsonpretty
    
              Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes

## Documentation

Parses RecentFileCacheParser.bcf files.

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). Use the [Get-ZimmermanTools](https://f001.backblazeb2.com/file/EricZimmermanTools/Get-ZimmermanTools.zip) PowerShell script to automate the download and updating of the EZ Tools suite. Additionally, you can automate each of these tools using [KAPE](https://www.kroll.com/en/services/cyber-risk/incident-response-litigation-support/kroll-artifact-parser-extractor-kape)!

# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
