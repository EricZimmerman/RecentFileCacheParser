using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RecentFileCache;

/// <summary>
///     A parsed RecentFileCache.bcf file.
/// </summary>
/// <remarks>
///     Layout: a 20 byte header (signature 0xFFEEFFFE at 0x00, a CRC32 at 0x10), then entries until the end of the
///     file. Each entry is a 32-bit character count, that many UTF-16LE characters and a NUL character.
/// </remarks>
public class RecentFileCacheFile
{
    public const uint Signature = 0xFFEEFFFE;

    /// <summary>
    ///     Size of the header, which is also where the first entry starts.
    /// </summary>
    public const int HeaderSize = 0x14;

    private const int ChecksumOffset = 0x10;

    /// <summary>
    ///     Parses a RecentFileCache.bcf file. Times of the source file are read from disk when sourceFile exists.
    /// </summary>
    /// <param name="rawBytes">Contents of the file</param>
    /// <param name="sourceFile">Path the contents came from</param>
    /// <exception cref="RecentFileCacheParseException">The data is too small or has a bad signature</exception>
    public RecentFileCacheFile(byte[] rawBytes, string sourceFile)
    {
        if (rawBytes == null)
        {
            throw new ArgumentNullException(nameof(rawBytes));
        }

        SourceFile = Path.GetFullPath(sourceFile);

        if (rawBytes.Length < HeaderSize)
        {
            throw new RecentFileCacheParseException(
                $"File is {rawBytes.Length} bytes, smaller than the {HeaderSize} byte header", 0, "Header");
        }

        if (BitConverter.ToUInt32(rawBytes, 0) != Signature)
        {
            throw new RecentFileCacheParseException(
                $"Invalid signature 0x{BitConverter.ToUInt32(rawBytes, 0):X8}! Should be 0x{Signature:X8}", 0,
                "Signature");
        }

        if (File.Exists(sourceFile))
        {
            var fi = new FileInfo(sourceFile);
            SourceCreated = new DateTimeOffset(fi.CreationTimeUtc);
            SourceModified = new DateTimeOffset(fi.LastWriteTimeUtc);
            SourceAccessed = new DateTimeOffset(fi.LastAccessTimeUtc);
        }

        Checksum = BitConverter.ToUInt32(rawBytes, ChecksumOffset);
        CalculatedChecksum = Crc32.Compute(rawBytes, ChecksumOffset);

        if (!ChecksumValid)
        {
            ParseErrors.Add(
                $"Checksum mismatch: header has 0x{Checksum:X8}, contents give 0x{CalculatedChecksum:X8}. The file may have been modified or damaged");
        }

        ParseEntries(rawBytes);
    }

    public string SourceFile { get; }

    public DateTimeOffset SourceCreated { get; }
    public DateTimeOffset SourceModified { get; }
    public DateTimeOffset SourceAccessed { get; }

    /// <summary>
    ///     CRC32 stored in the header at 0x10.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    ///     CRC32 of the file with the 4 checksum bytes left out.
    /// </summary>
    public uint CalculatedChecksum { get; }

    public bool ChecksumValid => Checksum == CalculatedChecksum;

    /// <summary>
    ///     File names in the order they are stored. An entry whose length runs past the end of the file is left out.
    /// </summary>
    public List<string> FileNames { get; } = new List<string>();

    /// <summary>
    ///     Problems found while parsing: a checksum mismatch, a damaged entry or unexpected data. Parsing continues
    ///     where it can.
    /// </summary>
    public List<string> ParseErrors { get; } = new List<string>();

    /// <summary>
    ///     True when <see cref="ParseErrors" /> is not empty.
    /// </summary>
    public bool ParsingError => ParseErrors.Count > 0;

    private void ParseEntries(byte[] rawBytes)
    {
        var index = HeaderSize;

        while (index < rawBytes.Length)
        {
            var remaining = rawBytes.Length - index;

            if (remaining < 4)
            {
                ParseErrors.Add($"{remaining} trailing byte(s) at 0x{index:X} are too short for an entry");
                return;
            }

            var length = BitConverter.ToInt32(rawBytes, index);

            // A bad length means the rest can't be found, since entries have no other marker
            if (length < 0 || (long)length * 2 > remaining - 4)
            {
                ParseErrors.Add(
                    $"Entry at 0x{index:X} has a length of {length} characters, but only {remaining - 4} bytes remain. Stopping");
                return;
            }

            var name = Encoding.Unicode.GetString(rawBytes, index + 4, length * 2);
            var terminator = index + 4 + length * 2;

            if (terminator + 2 > rawBytes.Length)
            {
                ParseErrors.Add($"Entry at 0x{index:X} ('{name}') is missing its NUL terminator");
                FileNames.Add(name);
                return;
            }

            if (rawBytes[terminator] != 0 || rawBytes[terminator + 1] != 0)
            {
                ParseErrors.Add(
                    $"Entry at 0x{index:X} ('{name}') has 0x{BitConverter.ToUInt16(rawBytes, terminator):X4} instead of a NUL terminator");
            }

            FileNames.Add(name);
            index = terminator + 2;
        }
    }

    public override string ToString()
    {
        return $"FileNames found: {FileNames.Count:N0}";
    }
}
