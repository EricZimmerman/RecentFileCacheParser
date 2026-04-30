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
        
    private static string Header =
        $"RecentFileCacheParser version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
        "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        "\r\nhttps://github.com/EricZimmerman/RecentFileCacheParser";


    private static string Footer = @"Examples: RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --csv ""c:\temp""" +
                                   "\r\n\t " +
                                   @"   RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --json ""D:\jsonOutput"" --jsonpretty" +
                                   "\r\n\t " +
                                   "\r\n\t" +
                                   "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes";

    private static RootCommand _rootCommand;

    private static DateTimeOffset ts = DateTimeOffset.UtcNow;
        
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
     

    private static async Task Main(string[] args)
    {
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
            Description   = "File name to save CSV formatted results to. When present, overrides default name"
        };
        
        var jsonOpt = new Option<string>(
            "--json")
        {
            Description   = "Directory to save json representation to. Use --pretty for a more human readable layout"
        };
        
        var prettyOpt = new Option<bool>("--pretty")
        {
            Description = "When exporting to json, use a more human readable layout",
            DefaultValueFactory = _ => false
        };
        
        var qOpt = new Option<bool>("-q")
        {
            Description = "Only show the filename being processed vs all output. Useful to speed up exporting to JSON and/or CSV",
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

        
        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt), result.GetValue(csvOpt), result.GetValue(csvfOpt),
            result.GetValue(jsonOpt), result.GetValue(prettyOpt), result.GetValue(qOpt)));

        var foo = _rootCommand.Parse(args).InvokeAsync();
        
        Log.CloseAndFlush();
    }
    
    class DateTimeOffsetFormatter : IFormatProvider, ICustomFormatter
    {
        private readonly IFormatProvider _innerFormatProvider;

        public DateTimeOffsetFormatter(IFormatProvider innerFormatProvider)
        {
            _innerFormatProvider = innerFormatProvider;
        }

        public object GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : _innerFormatProvider.GetFormat(formatType);
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
    }

    public static void DoWork(string f, string csv, string csvf, string json, bool pretty, bool q)
    {
        var levelSwitch = new LoggingLevelSwitch();
        var formatter  =
            new DateTimeOffsetFormatter(CultureInfo.CurrentCulture);

        var template = "{Message:lj}{NewLine}{Exception}";

        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template,formatProvider: formatter)
            .MinimumLevel.ControlledBy(levelSwitch);
      
        Log.Logger = conf.CreateLogger();
        
        if (f.IsNullOrEmpty())
        {
            var aaa = new CustomHelpAction(new HelpAction());
            aaa.Invoke(_rootCommand.Parse("-f is required. Exiting"));

           
            Console.WriteLine();
            return;
        }

        if (f.IsNullOrEmpty() == false &&
            !File.Exists(f))
        {
            Log.Warning("File {F} not found. Exiting",f);
            Console.WriteLine();
            return;
        }
        Log.Information("{Header}",Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}",string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
        Console.WriteLine();

        if (IsAdministrator() == false)
        {
            Log.Warning($"Warning: Administrator privileges not found!");
            Console.WriteLine();
        }

        try
        {
            if (q == false)
            {
                Log.Warning("Processing {F}",f);
                Console.WriteLine();
            }

            var sw = new Stopwatch();
            sw.Start();

            var rfc = RecentFileCache.RecentFileCache.LoadFile(f);

            if (q == false)
            {
                Log.Information("Source file: {SourceFile}",rfc.SourceFile);
                Log.Information("  Source created:  {Date}",rfc.SourceCreated);
                Log.Information("  Source modified: {Date}",rfc.SourceModified);
                Log.Information("  Source accessed: {Date}",rfc.SourceAccessed);
                Console.WriteLine();

                Log.Information("File names");
                foreach (var rfcFileName in rfc.FileNames)
                {
                    Log.Information("{FileName}",rfcFileName);
                }

                Console.WriteLine();
            }

            sw.Stop();

            if (q)
            {
                Console.WriteLine();
            }

            Log.Information("---------- Processed {SourceFile} in {TotalSeconds:N8} seconds ----------",rfc.SourceFile,sw.Elapsed.TotalSeconds);

            if (q == false)
            {
                Console.WriteLine();
            }

            try
            {
                StreamWriter sw1 = null;

                if (csv?.Length > 0)
                {
                    if (Directory.Exists(csv) == false)
                    {
                        Log.Information("{Csv} does not exist. Creating...",csv);
                        Directory.CreateDirectory(csv);
                    }

                    var outName =
                        $"{ts:yyyyMMddHHmmss}_RecentFileCacheParser_Output.csv";

                    if (csvf.IsNullOrEmpty() == false)
                    {
                        outName = Path.GetFileName(csvf);
                    }

                    var outFile = Path.Combine(csv, outName);
                   
                    Log.Information("CSV output will be saved to {Path}",Path.GetFullPath(outFile));

                    try
                    {
                        sw1 = new StreamWriter(outFile);
                        var csvWriter = new CsvWriter(sw1,CultureInfo.InvariantCulture);

                        var foo = csvWriter.Context.AutoMap<CsvOut>();
                        foo.Map(t => t.SourceAccessed)
                            .Convert(t => t.Value.SourceAccessed.ToString(_dateTimeFormat));
                        foo.Map(t => t.SourceCreated).Convert(t => t.Value.SourceCreated.ToString(_dateTimeFormat));
                        foo.Map(t => t.SourceModified)
                            .Convert(t => t.Value.SourceModified.ToString(_dateTimeFormat));

                        csvWriter.WriteHeader(typeof(CsvOut));
                        csvWriter.NextRecord();

                        csvWriter.WriteRecords(GetCsvFormat(rfc));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex,"Unable to open {OutFile} for writing. Export canceled. Error: {Message}",outFile,ex.Message);
                    }
                }

                if (json?.Length > 0)
                {
                    if (Directory.Exists(json) == false)
                    {
                        Log.Information("{Json} does not exist. Creating...",json);
                        Directory.CreateDirectory(json);
                    }

                    Log.Information("Saving json output to {Json}",json);

                    SaveJson(rfc, pretty,
                        json);
                }

                //Close CSV stuff
                sw1?.Flush();
                sw1?.Close();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting data! Error: {Message}",e.Message);
            }
        }
        catch (UnauthorizedAccessException ua)
        {
            Log.Error(ua, "Unable to access {F}. Are you running as an administrator? Error: {Message}",f,ua.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex,"Error processing file {F} Please send it to saericzimmerman@gmail.com. Error: {Message}",f,ex.Message);
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

    private static void DumpToJson(RecentFileCacheFile rfc, bool pretty, string outFile)
    {
        if (pretty)
        {
            File.WriteAllText(outFile, rfc.Dump());
        }
        else
        {
            File.WriteAllText(outFile, rfc.ToJson());
        }
    }

    private static void SaveJson(RecentFileCacheFile rfc, bool pretty, string outDir)
    {
        try
        {
            if (Directory.Exists(outDir) == false)
            {
                Directory.CreateDirectory(outDir);
            }

            var outName =
                $"{ts:yyyyMMddHHmmss}_{Path.GetFileName(rfc.SourceFile)}.json";
            var outFile = Path.Combine(outDir, outName);

            DumpToJson(rfc, pretty, outFile);
        }
        catch (Exception ex)
        {
            Log.Error(ex,"Error exporting json for {rfc.SourceFile}. Error: {Message}",rfc.SourceFile,ex.Message);
        }
    }

    private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    
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

            Log.Warning("{Msg}", string.Join(" ",parseResult.Tokens));

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