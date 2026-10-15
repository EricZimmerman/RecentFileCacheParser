using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace RecentFileCache.Test;

/// <summary>
///     In-memory files that exercise malformed input
/// </summary>
[TestFixture]
public class RobustnessTests
{
    // Not a real path, so source times are left at their defaults
    private const string Name = "memory.bcf";

    /// <summary>
    ///     A header (with a correct checksum unless one is given) followed by entries
    /// </summary>
    private static byte[] Build(IEnumerable<byte[]> entries, uint? checksum = null)
    {
        var data = new List<byte>();
        data.AddRange(BitConverter.GetBytes(RecentFileCacheFile.Signature));
        data.AddRange(BitConverter.GetBytes(0x2211u));
        data.AddRange(BitConverter.GetBytes(3u));
        data.AddRange(BitConverter.GetBytes(1u));
        data.AddRange(BitConverter.GetBytes(0u));
        foreach (var entry in entries)
        {
            data.AddRange(entry);
        }

        var bytes = data.ToArray();
        var crc = checksum ?? Crc32.Compute(bytes, 0x10);
        Array.Copy(BitConverter.GetBytes(crc), 0, bytes, 0x10, 4);
        return bytes;
    }

    private static byte[] Entry(string name, int? length = null, ushort terminator = 0)
    {
        return BitConverter.GetBytes(length ?? name.Length)
            .Concat(Encoding.Unicode.GetBytes(name))
            .Concat(BitConverter.GetBytes(terminator))
            .ToArray();
    }

    private static RecentFileCacheFile Parse(byte[] data) => new RecentFileCacheFile(data, Name);

    [Test]
    public void BuiltFileMatchesTheSampleLayout()
    {
        var sample = System.IO.File.ReadAllBytes(RecentFileCacheTests.Sample("RecentFileCache 2.bcf"));

        Build(new[] { Entry(@"c:\windows\psexesvc.exe"), Entry(@"c:\windows\system32\tasklist.exe") }).Should()
            .Equal(sample);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(19)]
    public void DataSmallerThanTheHeaderThrows(int size)
    {
        Action act = () => Parse(new byte[size]);

        act.Should().Throw<RecentFileCacheParseException>().Where(e => e.Field == "Header");
    }

    [Test]
    public void EmptyFileOnDiskThrowsParseException()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            Action act = () => RecentFileCache.LoadFile(path);

            act.Should().Throw<RecentFileCacheParseException>();
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Test]
    public void NullDataThrows()
    {
        Action act = () => new RecentFileCacheFile(null, Name);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void HeaderOnlyHasNoEntries()
    {
        var rfc = Parse(Build(new byte[0][]));

        rfc.FileNames.Should().BeEmpty();
        rfc.ParsingError.Should().BeFalse();
        rfc.SourceModified.Should().Be(default);
    }

    [Test]
    public void ChecksumMismatchIsReportedAndEntriesAreStillParsed()
    {
        var rfc = Parse(Build(new[] { Entry("a.exe") }, 0x12345678));

        rfc.ChecksumValid.Should().BeFalse();
        rfc.Checksum.Should().Be(0x12345678);
        rfc.FileNames.Should().Equal("a.exe");
        rfc.ParseErrors.Should().ContainSingle().Which.Should().Contain("Checksum mismatch");
    }

    [Test]
    public void EmptyNameIsAnEntry()
    {
        var rfc = Parse(Build(new[] { Entry(""), Entry("b.exe") }));

        rfc.FileNames.Should().Equal("", "b.exe");
        rfc.ParsingError.Should().BeFalse();
    }

    [TestCase(-1)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    [TestCase(1000)]
    public void BadLengthStopsParsingWithoutThrowing(int length)
    {
        var rfc = Parse(Build(new[] { Entry("good.exe"), Entry("bad.exe", length), Entry("never.exe") }));

        rfc.FileNames.Should().Equal("good.exe");
        rfc.ParseErrors.Should().ContainSingle().Which.Should().Contain($"length of {length} characters");
    }

    [Test]
    public void EntryCutOffBeforeItsTerminatorIsKept()
    {
        var data = Build(new[] { Entry("good.exe"), Entry("cut.exe") });
        var truncated = data.Take(data.Length - 2).ToArray();

        var rfc = Parse(truncated);

        rfc.FileNames.Should().Equal("good.exe", "cut.exe");
        rfc.ParseErrors.Should().Contain(t => t.Contains("missing its NUL terminator"));
    }

    [Test]
    public void NonNulTerminatorIsReportedAndParsingContinues()
    {
        var rfc = Parse(Build(new[] { Entry("odd.exe", terminator: 0x4141), Entry("next.exe") }));

        rfc.FileNames.Should().Equal("odd.exe", "next.exe");
        rfc.ParseErrors.Should().ContainSingle().Which.Should().Contain("0x4141 instead of a NUL terminator");
    }

    [TestCase(1)]
    [TestCase(3)]
    public void TrailingBytesAreReported(int extra)
    {
        var rfc = Parse(Build(new[] { Entry("a.exe"), new byte[extra] }));

        rfc.FileNames.Should().Equal("a.exe");
        rfc.ParseErrors.Should().ContainSingle().Which.Should().Contain($"{extra} trailing byte(s)");
    }

    [Test]
    public void RandomCorruptionNeverThrowsAfterTheHeader()
    {
        var random = new Random(42);
        var good = Build(Enumerable.Range(0, 20).Select(i => Entry($@"c:\windows\file{i}.exe")));

        for (var i = 0; i < 2000; i++)
        {
            var data = good.Take(random.Next(RecentFileCacheFile.HeaderSize, good.Length + 1)).ToArray();
            for (var flips = random.Next(1, 8); flips > 0; flips--)
            {
                var at = random.Next(RecentFileCacheFile.HeaderSize, Math.Max(RecentFileCacheFile.HeaderSize + 1, data.Length));
                if (at < data.Length)
                {
                    data[at] = (byte)random.Next(256);
                }
            }

            Action act = () => Parse(data);

            act.Should().NotThrow();
        }
    }

    [Test]
    public void Crc32MatchesKnownValue()
    {
        // Standard check value for CRC-32/ISO-HDLC over "123456789"
        var data = Encoding.ASCII.GetBytes("1234" + "\0\0\0\0" + "56789");

        Crc32.Compute(data, 4).Should().Be(0xCBF43926);
    }
}
