using System;
using System.IO;

namespace RecentFileCache
{
    public static class RecentFileCache
    {
        public static RecentFileCacheFile LoadFile(string rfcFile)
        {
            var raw = File.ReadAllBytes(rfcFile);

            if (raw[0] != 0xFE)
            {
                throw new Exception($"Invalid signature!");
            }

            return new RecentFileCacheFile(raw, rfcFile);
        }
    }
}