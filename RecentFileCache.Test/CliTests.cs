using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace RecentFileCache.Test;

/// <summary>
///     Runs the built RecentFileCacheParser.dll (copied next to the tests by the project reference) as a separate process
/// </summary>
[TestFixture]
public class CliTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "rfc-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_dir, true);
    }

    private static (int ExitCode, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "RecentFileCacheParser.dll"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Close();

        process.WaitForExit(60_000).Should().BeTrue("RecentFileCacheParser should finish");

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    [Test]
    public void ParsesSampleAndShowsChecksum()
    {
        var (exitCode, output) = Run("-f", RecentFileCacheTests.Sample("RecentFileCache.bcf"));

        exitCode.Should().Be(0);
        output.Should().Contain(@"c:\windows\bcuninstall.exe");
        output.Should().Contain("Checksum: 0xF4637B64 (valid)");
    }

    [Test]
    public void MissingFileOptionExitsWithOne()
    {
        var (exitCode, output) = Run();

        exitCode.Should().Be(1);
        output.Should().Contain("-f is required");
    }

    [Test]
    public void MissingFileExitsWithOne()
    {
        Run("-f", Path.Combine(_dir, "nope.bcf")).ExitCode.Should().Be(1);
    }

    [Test]
    public void FileThatIsNotARecentFileCacheExitsWithZero()
    {
        var (exitCode, output) = Run("-f", RecentFileCacheTests.Sample("NotARecentFileCache.bcf"));

        exitCode.Should().Be(0, "a bad file is not the tool's fault");
        output.Should().Contain("Invalid signature");
    }

    [Test]
    public void DamagedFileIsParsedWithWarningsAndExitsWithZero()
    {
        var damaged = Path.Combine(_dir, "damaged.bcf");
        var data = File.ReadAllBytes(RecentFileCacheTests.Sample("RecentFileCache 2.bcf"));
        File.WriteAllBytes(damaged, data.Take(data.Length - 10).ToArray());

        var (exitCode, output) = Run("-f", damaged, "--csv", Path.Combine(_dir, "csv"), "--csvf", "out.csv");

        exitCode.Should().Be(0, "a damaged file is listed with its problems, but is not the tool's fault");
        File.ReadAllText(Path.Combine(_dir, "csv", "out.csv")).Should().Contain(@"c:\windows\psexesvc.exe", "what could be read is still exported");
        output.Should().Contain("(INVALID)");
        output.Should().Contain("Problems found while parsing");
        output.Should().Contain(@"c:\windows\psexesvc.exe");
    }

    [Test]
    public void WritesCsv()
    {
        var sample = RecentFileCacheTests.Sample("RecentFileCache 2.bcf");

        var (exitCode, _) = Run("-f", sample, "--csv", Path.Combine(_dir, "csv"), "--csvf", "out.csv", "-q");

        exitCode.Should().Be(0);
        var lines = File.ReadAllLines(Path.Combine(_dir, "csv", "out.csv"));
        lines.Should().HaveCount(3);
        lines[0].Should().Be("SourceFile,SourceCreated,SourceModified,SourceAccessed,Filename");
        lines[1].Should().StartWith(Path.GetFullPath(sample) + ",").And.EndWith(@",c:\windows\psexesvc.exe");
        lines[2].Should().EndWith(@",c:\windows\system32\tasklist.exe");
        // Times use the yyyy-MM-dd HH:mm:ss format
        lines[1].Split(',')[1].Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WritesJson(bool pretty)
    {
        var args = new[] { "-f", RecentFileCacheTests.Sample("RecentFileCache 2.bcf"), "--json", _dir, "-q" };
        if (pretty)
        {
            args = args.Append("--pretty").ToArray();
        }

        Run(args).ExitCode.Should().Be(0);

        var json = File.ReadAllText(Directory.GetFiles(_dir, "*_RecentFileCache 2.bcf.json").Single());
        json.Should().Contain("psexesvc.exe").And.Contain("ChecksumValid");
    }

    [Test]
    public void FailedExportExitsWithOne()
    {
        // A file where the CSV directory should be
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "");

        Run("-f", RecentFileCacheTests.Sample("RecentFileCache.bcf"), "--csv", blocker, "-q").ExitCode.Should().Be(1);
    }

    [Test]
    public void FileThatCantBeReadExitsWithOne()
    {
        if (OperatingSystem.IsWindows() == false)
        {
            Assert.Ignore("Locking a file for everyone else is Windows only");
        }

        var locked = Path.Combine(_dir, "locked.bcf");
        File.Copy(RecentFileCacheTests.Sample("RecentFileCache.bcf"), locked);

        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Run("-f", locked).ExitCode.Should().Be(1);
        }
    }
}
