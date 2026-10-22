using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Exceptionless;
using RecentFileCache;
using Serilog;
using Serilog.Core;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;

namespace RecentFileCacheParser;

internal class Program
{
    private static readonly string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly string Header =
        $"RecentFileCacheParser version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
        "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        "\r\nhttps://github.com/EricZimmerman/RecentFileCacheParser";


    private static readonly string Footer =
        @"Examples: RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --csv ""c:\temp""" +
        "\r\n\t " +
        @"   RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --json ""D:\jsonOutput"" --pretty" +
        "\r\n\t " +
        "\r\n\t" +
        "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes";

    private static RootCommand _rootCommand;

    private static readonly DateTimeOffset ts = DateTimeOffset.UtcNow;

    public static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }


    // JSON time stamps: ISO 8601 in UTC (2024-01-02T03:04:05.6780000Z), as JLECmd writes them, instead of ServiceStack's \/Date(ms)\/
    private const string JsonDateFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    private static void ConfigureJsonDates()
    {
        JsConfig.DateHandler = DateHandler.ISO8601;
        JsConfig<DateTimeOffset>.SerializeFn = JsonDate;
        JsConfig<DateTimeOffset?>.SerializeFn = d => d.HasValue ? JsonDate(d.Value) : null;
        JsConfig<DateTime>.SerializeFn = JsonDate;
        JsConfig<DateTime?>.SerializeFn = d => d.HasValue ? JsonDate(d.Value) : null;
    }

    private static string JsonDate(DateTimeOffset d)
    {
        return d.UtcDateTime.ToString(JsonDateFormat, CultureInfo.InvariantCulture);
    }

    private static string JsonDate(DateTime d)
    {
        //timestamps without a kind are already UTC; ToUniversalTime would treat them as local time
        if (d.Kind == DateTimeKind.Unspecified)
        {
            d = DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }

        return d.ToUniversalTime().ToString(JsonDateFormat, CultureInfo.InvariantCulture);
    }

    private static async Task<int> Main(string[] args)
    {
        ConfigureJsonDates();

        ExceptionlessClient.Default.Startup("Wdlq68AwLteBtuqOwNv5rgphcMxzKuHKJQAVK5JN");

        var fOpt = new Option<string>("-f")
        {
            Description = "File to process. Required"
        };

        var csvOpt = new Option<string>(
            "--csv")
        {
            Description = "Directory to save CSV formatted results to. Be sure to include the full path in double quotes"
        };

        var csvfOpt = new Option<string>(
            "--csvf")
        {
            Description = "File name to save CSV formatted results to. When present, overrides default name"
        };

        var jsonOpt = new Option<string>(
            "--json")
        {
            Description = "Directory to save json representation to. Use --pretty for a more human readable layout"
        };

        var prettyOpt = new Option<bool>("--pretty")
        {
            Description = "When exporting to json, use a more human readable layout",
            DefaultValueFactory = _ => false
        };

        var qOpt = new Option<bool>("-q")
        {
            Description =
                "Only show the filename being processed vs all output. Useful to speed up exporting to JSON and/or CSV",
            DefaultValueFactory = _ => false
        };

        _rootCommand = new RootCommand
        {
            fOpt,
            csvOpt,
            csvfOpt,
            jsonOpt,
            prettyOpt,
            qOpt
        };

        _rootCommand.Description = Header + "\r\n\r\n" + Footer;


        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt), result.GetValue(csvOpt),
            result.GetValue(csvfOpt), result.GetValue(jsonOpt), result.GetValue(prettyOpt), result.GetValue(qOpt)));

        var exitCode = await _rootCommand.Parse(args).InvokeAsync();

        Log.CloseAndFlush();

        return exitCode;
    }

    private class DateTimeOffsetFormatter : IFormatProvider, ICustomFormatter
    {
        private readonly IFormatProvider _innerFormatProvider;

        public DateTimeOffsetFormatter(IFormatProvider innerFormatProvider)
        {
            _innerFormatProvider = innerFormatProvider;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg is DateTimeOffset)
            {
                var size = (DateTimeOffset)arg;
                return size.ToString(_dateTimeFormat);
            }

            var formattable = arg as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(format, _innerFormatProvider);
            }

            return arg.ToString();
        }

        public object GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : _innerFormatProvider.GetFormat(formatType);
        }
    }

    /// <returns>0 on success. 1 when arguments are invalid, the file can't be parsed or an export fails</returns>
    public static int DoWork(string f, string csv, string csvf, string json, bool pretty, bool q)
    {
        var levelSwitch = new LoggingLevelSwitch();
        var formatter =
            new DateTimeOffsetFormatter(CultureInfo.CurrentCulture);

        var template = "{Message:lj}{NewLine}{Exception}";

        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template, formatProvider: formatter)
            .MinimumLevel.ControlledBy(levelSwitch);

        Log.Logger = conf.CreateLogger();

        if (f.IsNullOrEmpty())
        {
            var helpAction = new CustomHelpAction(new HelpAction());
            helpAction.Invoke(_rootCommand.Parse(""));

            Console.WriteLine();
            return 1;
        }

        if (!File.Exists(f))
        {
            Log.Warning("File {F} not found. Exiting", f);
            Console.WriteLine();
            return 1;
        }

        Log.Information("{Header}", Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}", string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
        Console.WriteLine();

        if (IsAdministrator() == false)
        {
            Log.Warning("Warning: Administrator privileges not found!");
            Console.WriteLine();
        }

        RecentFileCacheFile rfc;

        try
        {
            if (q == false)
            {
                Log.Warning("Processing {F}", f);
                Console.WriteLine();
            }

            var sw = new Stopwatch();
            sw.Start();

            rfc = RecentFileCache.RecentFileCache.LoadFile(f);

            if (q == false)
            {
                Log.Information("Source file: {SourceFile}", rfc.SourceFile);
                Log.Information("  Source created:  {Date}", rfc.SourceCreated);
                Log.Information("  Source modified: {Date}", rfc.SourceModified);
                Log.Information("  Source accessed: {Date}", rfc.SourceAccessed);
                Console.WriteLine();

                Log.Information("Checksum: 0x{Checksum:X8} ({Status})", rfc.Checksum,
                    rfc.ChecksumValid ? "valid" : "INVALID");
                Console.WriteLine();

                Log.Information("File names");
                foreach (var rfcFileName in rfc.FileNames)
                {
                    Log.Information("{FileName}", rfcFileName);
                }

                Console.WriteLine();
            }

            if (rfc.ParsingError)
            {
                Log.Warning("Problems found while parsing:");
                foreach (var parseError in rfc.ParseErrors)
                {
                    Log.Warning("  {ParseError}", parseError);
                }

                Console.WriteLine();
            }

            sw.Stop();

            if (q)
            {
                Console.WriteLine();
            }

            Log.Information("---------- Processed {SourceFile} in {TotalSeconds:N8} seconds ----------",
                rfc.SourceFile, sw.Elapsed.TotalSeconds);

            if (q == false)
            {
                Console.WriteLine();
            }
        }
        catch (RecentFileCacheParseException pe)
        {
            Log.Error("Error processing file {F}: {Message} (offset 0x{Offset:X}, field {Field})", f, pe.Message,
                pe.Offset, pe.Field);
            return 1;
        }
        catch (UnauthorizedAccessException ua)
        {
            Log.Error(ua, "Unable to access {F}. Are you running as an administrator? Error: {Message}", f,
                ua.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing file {F} Please send it to saericzimmerman@gmail.com. Error: {Message}", f,
                ex.Message);
            return 1;
        }

        //a file that did not parse completely (as in PECmd): what could be read is still exported
        var exitCode = rfc.ParsingError ? 1 : 0;

        if (csv?.Length > 0 && !SaveCsv(rfc, csv, csvf))
        {
            exitCode = 1;
        }

        if (json?.Length > 0 && !SaveJson(rfc, pretty, json))
        {
            exitCode = 1;
        }

        return exitCode;
    }

    private static bool SaveCsv(RecentFileCacheFile rfc, string outDir, string csvf)
    {
        var outName = $"{ts:yyyyMMddHHmmss}_RecentFileCacheParser_Output.csv";

        if (csvf.IsNullOrEmpty() == false)
        {
            outName = Path.GetFileName(csvf);
        }

        var outFile = Path.Combine(outDir, outName);

        try
        {
            if (Directory.Exists(outDir) == false)
            {
                Log.Information("{Csv} does not exist. Creating...", outDir);
                Directory.CreateDirectory(outDir);
            }

            Log.Information("CSV output will be saved to {Path}", Path.GetFullPath(outFile));

            using var streamWriter = new StreamWriter(outFile);
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            var map = csvWriter.Context.AutoMap<CsvOut>();
            map.Map(t => t.SourceAccessed).Convert(t => t.Value.SourceAccessed.ToString(_dateTimeFormat));
            map.Map(t => t.SourceCreated).Convert(t => t.Value.SourceCreated.ToString(_dateTimeFormat));
            map.Map(t => t.SourceModified).Convert(t => t.Value.SourceModified.ToString(_dateTimeFormat));

            csvWriter.WriteHeader<CsvOut>();
            csvWriter.NextRecord();
            csvWriter.WriteRecords(GetCsvFormat(rfc));

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to save CSV to {OutFile}. Export canceled. Error: {Message}", outFile, ex.Message);
            return false;
        }
    }

    private static List<CsvOut> GetCsvFormat(RecentFileCacheFile rcf)
    {
        var csOut = new List<CsvOut>();

        foreach (var rcfFileName in rcf.FileNames)
        {
            var cs = new CsvOut
            {
                SourceFile = rcf.SourceFile,
                SourceCreated = rcf.SourceCreated,
                SourceModified = rcf.SourceModified,
                SourceAccessed = rcf.SourceAccessed,
                Filename = rcfFileName
            };

            csOut.Add(cs);
        }


        return csOut;
    }

    private static bool SaveJson(RecentFileCacheFile rfc, bool pretty, string outDir)
    {
        try
        {
            if (Directory.Exists(outDir) == false)
            {
                Log.Information("{Json} does not exist. Creating...", outDir);
                Directory.CreateDirectory(outDir);
            }

            Log.Information("Saving json output to {Json}", outDir);

            var outName =
                $"{ts:yyyyMMddHHmmss}_{Path.GetFileName(rfc.SourceFile)}.json";
            var outFile = Path.Combine(outDir, outName);

            File.WriteAllText(outFile, pretty ? rfc.Dump() : rfc.ToJson());

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting json for {SourceFile}. Error: {Message}", rfc.SourceFile, ex.Message);
            return false;
        }
    }

    private class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public CustomHelpAction(HelpAction action)
        {
            _defaultHelp = action;
        }

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);

            Log.Warning("-f is required. Exiting");

            return result;
        }
    }
}

public sealed class CsvOut
{
    public string SourceFile { get; set; }
    public DateTimeOffset SourceCreated { get; set; }
    public DateTimeOffset SourceModified { get; set; }
    public DateTimeOffset SourceAccessed { get; set; }

    public string Filename { get; set; }
}
