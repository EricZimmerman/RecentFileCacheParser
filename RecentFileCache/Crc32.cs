namespace RecentFileCache;

/// <summary>
///     CRC-32 (IEEE 802.3, the zlib/PKZIP one)
/// </summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }

    /// <summary>
    ///     CRC-32 of data, leaving out the 4 bytes at skipOffset
    /// </summary>
    public static uint Compute(byte[] data, int skipOffset)
    {
        var crc = 0xFFFFFFFF;
        for (var i = 0; i < data.Length; i++)
        {
            if (i == skipOffset)
            {
                i += 3;
                continue;
            }

            crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }
}
