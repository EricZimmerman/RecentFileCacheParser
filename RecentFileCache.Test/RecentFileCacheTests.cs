using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace RecentFileCache.Test
{
    public class RecentFileCacheTests
    {
        [Test]
        public void BaseTests()
        {
            var basePath = @"..\..\Files";

            var f1 = Path.Combine(basePath, "RecentFileCache.bcf");
            var f2 = Path.Combine(basePath, "RecentFileCache 2.bcf");
            var f3 = Path.Combine(basePath, "NotARecentFileCache.bcf");

            var r1 = RecentFileCache.LoadFile(f1);

            r1.Should().NotBe(null);

            r1.FileNames.Should().NotBeEmpty();
            r1.FileNames.Should().Contain(t => t.Contains("wipesvc.exe"));
            r1.FileNames.Should().NotContain(t => t.Contains("TotallyMadeUp.exe"));
            r1.FileNames.Count.Should().Be(6);
            r1.FileNames.Last().Should().Be(@"c:\windows\bcuninstall.exe");

            var r2 = RecentFileCache.LoadFile(f2);

            r2.Should().NotBe(null);
            r2.FileNames.Should().NotBeEmpty();
            r2.FileNames.Should().Contain(t => t.Contains("tasklist.exe"));
            r2.FileNames.Should().NotContain(t => t.Contains("TotallyMadeUp2.exe"));
            r2.FileNames.Count.Should().Be(2);
            r2.FileNames.Last().Should().Be(@"c:\windows\system32\tasklist.exe");

            //test bad file
            Action action = () => RecentFileCache.LoadFile(f3);

            //action.ShouldThrow<Exception>().WithMessage("Invalid signature!");
        }
    }
}