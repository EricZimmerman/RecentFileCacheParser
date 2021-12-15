using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Exceptionless;
using NLog;
using NLog.Config;
using NLog.Targets;
using RecentFileCache;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;

namespace RecentFileCacheParser
{
    internal class Program
    {
        private static Logger _logger;
        private static readonly string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        
        private static string Header =
            $"RecentFileCacheParser version {Assembly.GetExecutingAssembly().GetName().Version}" +
            "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
            "\r\nhttps://github.com/EricZimmerman/RecentFileCacheParser";


        private static string Footer = @"Examples: RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --csv ""c:\temp""" +
                     "\r\n\t " +
                     @"   RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --json ""D:\jsonOutput"" --jsonpretty" +
                     "\r\n\t " +
                     "\r\n\t" +
                     "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes";

        private static RootCommand _rootCommand;
        
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

            SetupNLog();

            _logger = LogManager.GetCurrentClassLogger();

            _rootCommand = new RootCommand
            {
                new Option<string>(
                    "-f",
                    "File to process. Required"),

             
                new Option<string>(
                    "--csv",
                    "Directory to save CSV formatted results to. Be sure to include the full path in double quotes"),

                new Option<string>(
                    "--csvf",
                    "File name to save CSV formatted results to. When present, overrides default name"),

                new Option<string>(
                    "--json",
                    "Directory to save json representation to. Use --pretty for a more human readable layout"),
                new Option<bool>(
                    "--pretty",
                    getDefaultValue:()=>false,
                    "When exporting to json, use a more human readable layout"),
                new Option<bool>(
                    "-q",
                    getDefaultValue:()=>false,
                    "Only show the filename being processed vs all output. Useful to speed up exporting to json and/or csv"),

            };

            _rootCommand.Description = Header + "\r\n\r\n" + Footer;

            _rootCommand.Handler = CommandHandler.Create(DoWork);

            await _rootCommand.InvokeAsync(args);
        }

        public static void DoWork(string f, string csv, string csvf, string json, bool pretty, bool q)
        {
            if (f.IsNullOrEmpty())
            {
                var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
                var hc = new HelpContext(helpBld, _rootCommand, Console.Out);

                helpBld.Write(hc);

                _logger.Warn("-f is required. Exiting");
                return;
            }

            if (f.IsNullOrEmpty() == false &&
                !File.Exists(f))
            {
                _logger.Warn($"File '{f}' not found. Exiting");
                return;
            }
            _logger.Info(Header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

            if (IsAdministrator() == false)
            {
                _logger.Fatal($"Warning: Administrator privileges not found!\r\n");
            }

            try
            {
                if (q == false)
                {
                    _logger.Warn($"Processing '{f}'");
                    _logger.Info("");
                }

                var sw = new Stopwatch();
                sw.Start();

                var rfc = RecentFileCache.RecentFileCache.LoadFile(f);

                if (q == false)
                {
                    _logger.Error($"Source file: {rfc.SourceFile}");
                    _logger.Info($"  Source created:  {rfc.SourceCreated.ToString(_dateTimeFormat)} ");
                    _logger.Info($"  Source modified: {rfc.SourceModified.ToString(_dateTimeFormat)}");
                    _logger.Info($"  Source accessed: {rfc.SourceAccessed.ToString(_dateTimeFormat)}");
                    _logger.Info("");

                    _logger.Warn("File names");
                    foreach (var rfcFileName in rfc.FileNames)
                    {
                        _logger.Info($"{rfcFileName}");
                    }

                    _logger.Info("");
                }

                sw.Stop();

                if (q)
                {
                    _logger.Info("");
                }

                _logger.Info(
                    $"---------- Processed '{rfc.SourceFile}' in {sw.Elapsed.TotalSeconds:N8} seconds ----------");

                if (q == false)
                {
                    _logger.Info("\r\n");
                }

                try
                {
                    StreamWriter sw1 = null;

                    if (csv?.Length > 0)
                    {
                        if (Directory.Exists(csv) == false)
                        {
                            _logger.Warn(
                                $"'{csv} does not exist. Creating...'");
                            Directory.CreateDirectory(csv);
                        }

                        var outName =
                            $"{DateTimeOffset.Now:yyyyMMddHHmmss}_RecentFileCacheParser_Output.csv";

                        if (csvf.IsNullOrEmpty() == false)
                        {
                            outName = Path.GetFileName(csvf);
                        }

                        var outFile = Path.Combine(csv, outName);
                   
                        _logger.Warn(
                            $"CSV output will be saved to '{Path.GetFullPath(outFile)}'");

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
                            _logger.Error(
                                $"Unable to open '{outFile}' for writing. Export canceled. Error: {ex.Message}");
                        }
                    }

                    if (json?.Length > 0)
                    {
                        if (Directory.Exists(json) == false)
                        {
                            _logger.Warn(
                                $"'{json} does not exist. Creating...'");
                            Directory.CreateDirectory(json);
                        }

                        _logger.Warn($"Saving json output to '{json}'");

                        SaveJson(rfc, pretty,
                            json);
                    }

                    //Close CSV stuff
                    sw1?.Flush();
                    sw1?.Close();
                }
                catch (Exception e)
                {
                    _logger.Error(
                        $"Error exporting data! Error: {e.Message}");
                }
            }
            catch (UnauthorizedAccessException ua)
            {
                _logger.Error(
                    $"Unable to access '{f}'. Are you running as an administrator? Error: {ua.Message}");
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Error processing file '{f}' Please send it to saericzimmerman@gmail.com. Error: {ex.Message}");
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
                    $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(rfc.SourceFile)}.json";
                var outFile = Path.Combine(outDir, outName);

                DumpToJson(rfc, pretty, outFile);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error exporting json for '{rfc.SourceFile}'. Error: {ex.Message}");
            }
        }

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static void SetupNLog()
        {
            if (File.Exists( Path.Combine(BaseDirectory,"Nlog.config")))
            {
                return;
            }
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
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
}