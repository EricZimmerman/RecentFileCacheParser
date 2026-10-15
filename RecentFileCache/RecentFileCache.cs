using System.IO;

namespace RecentFileCache;

public static class RecentFileCache
{
    /// <summary>
    ///     Reads and parses a RecentFileCache.bcf file.
    /// </summary>
    /// <exception cref="RecentFileCacheParseException">The file is too small or has a bad signature</exception>
    public static RecentFileCacheFile LoadFile(string rfcFile)
    {
        return new RecentFileCacheFile(File.ReadAllBytes(rfcFile), rfcFile);
    }
}
