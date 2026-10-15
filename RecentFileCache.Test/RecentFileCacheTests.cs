using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace RecentFileCache.Test;

/// <summary>
///     Assertions on the real samples in Files/
/// </summary>
[TestFixture]
public class RecentFileCacheTests
{
    internal static string Sample(string name) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", name);

    [Test]
    public void ParsesSample()
    {
        var rfc = RecentFileCache.LoadFile(Sample("RecentFileCache.bcf"));

        rfc.FileNames.Should().Equal(
            @"c:\windows\system32\werfault.exe",
            @"c:\program files\jetico\bcwipe\bcwipesvc.exe",
            @"c:\program files\jetico\bcwipe\bcwipetm.exe",
            @"c:\windows\system32\icacls.exe",
            @"c:\windows\system32\systempropertiesprotection.exe",
            @"c:\windows\bcuninstall.exe");
        rfc.Checksum.Should().Be(0xF4637B64);
        rfc.ChecksumValid.Should().BeTrue();
        rfc.ParseErrors.Should().BeEmpty();
        rfc.ParsingError.Should().BeFalse();
        rfc.SourceFile.Should().Be(Path.GetFullPath(Sample("RecentFileCache.bcf")));
        rfc.SourceModified.Should().BeAfter(DateTimeOffset.MinValue);
        rfc.ToString().Should().Be("FileNames found: 6");
    }

    [Test]
    public void ParsesSecondSample()
    {
        var rfc = RecentFileCache.LoadFile(Sample("RecentFileCache 2.bcf"));

        rfc.FileNames.Should().Equal(@"c:\windows\psexesvc.exe", @"c:\windows\system32\tasklist.exe");
        rfc.Checksum.Should().Be(0xE5918E10);
        rfc.ChecksumValid.Should().BeTrue();
        rfc.ParsingError.Should().BeFalse();
    }

    [Test]
    public void FileThatIsNotARecentFileCacheIsRejected()
    {
        // A LNK file
        Action act = () => RecentFileCache.LoadFile(Sample("NotARecentFileCache.bcf"));

        act.Should().Throw<RecentFileCacheParseException>()
            .Where(e => e.Field == "Signature" && e.Offset == 0 && e.Message.Contains("0x0000004C"));
    }

    [Test]
    public void EverySampleChecksumMatches()
    {
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(Sample("x"))!, "*.bcf")
                     .Where(t => !t.Contains("NotARecentFileCache")))
        {
            RecentFileCache.LoadFile(file).ChecksumValid.Should().BeTrue(file);
        }
    }
}
