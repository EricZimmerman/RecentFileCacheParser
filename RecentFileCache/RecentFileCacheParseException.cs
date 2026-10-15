using System;

namespace RecentFileCache;

/// <summary>
///     Thrown when data is not a usable RecentFileCache.bcf file (too small or bad signature).
///     Problems found after the header are reported in <see cref="RecentFileCacheFile.ParseErrors" /> instead.
/// </summary>
public class RecentFileCacheParseException : Exception
{
    public RecentFileCacheParseException(string message, int offset = -1, string field = null, Exception inner = null)
        : base(message, inner)
    {
        Offset = offset;
        Field = field;
    }

    /// <summary>
    ///     Byte offset of the failing read, or -1 when unknown.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    ///     Name of the field or structure being read when parsing failed, if known.
    /// </summary>
    public string Field { get; }
}
