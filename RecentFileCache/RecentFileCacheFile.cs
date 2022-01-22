using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RecentFileCache;

public class RecentFileCacheFile
{
    public RecentFileCacheFile(byte[] rawBytes, string sourceFile)
    {
        SourceFile = Path.GetFullPath(sourceFile);

        const uint header = 0xFFEEFFFE;

        if (BitConverter.ToUInt32(rawBytes, 0) != header)
        {
            throw new Exception($"Invalid header! Should be '0xFEFFEEFF'");
        }

        FileNames = new List<string>();

        var fi = new FileInfo(sourceFile);
        SourceCreated = new DateTimeOffset(fi.CreationTimeUtc);
        SourceModified = new DateTimeOffset(fi.LastWriteTimeUtc);
        SourceAccessed = new DateTimeOffset(fi.LastAccessTimeUtc);


        var index = 0x14; //start of data

        while (index < rawBytes.Length)
        {
            var size = BitConverter.ToInt32(rawBytes, index);
            index += 4;
            var fname = Encoding.Unicode.GetString(rawBytes, index, size * 2);

            index += size * 2 + 2; //size * 2 for unicode, plus null char

            FileNames.Add(fname);
        }
    }


    public string SourceFile { get; }

    public DateTimeOffset SourceCreated { get; }
    public DateTimeOffset SourceModified { get; }
    public DateTimeOffset SourceAccessed { get; }

    public List<string> FileNames { get; }

    public override string ToString()
    {
        return $"FileNames found: {FileNames.Count:N0}";
    }
}