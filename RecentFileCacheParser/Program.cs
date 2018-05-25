using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Exceptionless;
using Fclp;
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
        private static FluentCommandLineParser<AppArgs> _fluentCommandLineParser;
        private static readonly string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private static string exportExt = "tsv";

        private static void Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("Wdlq68AwLteBtuqOwNv5rgphcMxzKuHKJQAVK5JN");

            SetupNLog();

            _logger = LogManager.GetCurrentClassLogger();

            _fluentCommandLineParser = new FluentCommandLineParser<AppArgs>
            {
                IsCaseSensitive = false
            };

            _fluentCommandLineParser.Setup(arg => arg.File)
                .As('f')
                .WithDescription("File to process. Required");


            _fluentCommandLineParser.Setup(arg => arg.CsvDirectory)
                .As("csv")
                .WithDescription(
                    "Directory to save CSV formatted results to. Be sure to include the full path in double quotes");


            _fluentCommandLineParser.Setup(arg => arg.JsonDirectory)
                .As("json")
                .WithDescription(
                    "Directory to save json representation to. Use --pretty for a more human readable layout");

            _fluentCommandLineParser.Setup(arg => arg.JsonPretty)
                .As("pretty")
                .WithDescription(
                    "When exporting to json, use a more human readable layout\r\n").SetDefault(false);


            _fluentCommandLineParser.Setup(arg => arg.Quiet)
                .As('q')
                .WithDescription(
                    "Only show the filename being processed vs all output. Useful to speed up exporting to json and/or csv\r\n")
                .SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.CsvSeparator)
                .As("cs")
                .WithDescription(
                    "When true, use comma instead of tab for field separator. Default is true").SetDefault(true);


            var header =
                $"RecentFileCacheParser version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/RecentFileCacheParser";


            var footer = @"Examples: RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --csv ""c:\temp""" +
                         "\r\n\t " +
                         @" RecentFileCacheParser.exe -f ""C:\Temp\RecentFileCache.bcf"" --json ""D:\jsonOutput"" --jsonpretty" +
                         "\r\n\t " +
                         "\r\n\t" +
                         "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

            _fluentCommandLineParser.SetupHelp("?", "help")
                .WithHeader(header)
                .Callback(text => _logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _logger.Error("");
                _logger.Error(result.ErrorText);

                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("-f is required. Exiting");
                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() == false &&
                !File.Exists(_fluentCommandLineParser.Object.File))
            {
                _logger.Warn($"File '{_fluentCommandLineParser.Object.File}' not found. Exiting");
                return;
            }


            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

            try
            {
                if (_fluentCommandLineParser.Object.Quiet == false)
                {
                    _logger.Warn($"Processing '{_fluentCommandLineParser.Object.File}'");
                    _logger.Info("");
                }

                if (_fluentCommandLineParser.Object.CsvSeparator)
                {
                    exportExt = "csv";
                }

                var sw = new Stopwatch();
                sw.Start();

                var rfc = RecentFileCache.RecentFileCache.LoadFile(_fluentCommandLineParser.Object.File);

                if (_fluentCommandLineParser.Object.Quiet == false)
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

                if (_fluentCommandLineParser.Object.Quiet)
                {
                    _logger.Info("");
                }

                _logger.Info(
                    $"---------- Processed '{rfc.SourceFile}' in {sw.Elapsed.TotalSeconds:N8} seconds ----------");

                if (_fluentCommandLineParser.Object.Quiet == false)
                {
                    _logger.Info("\r\n");
                }

                try
                {
                    StreamWriter sw1 = null;

                    if (_fluentCommandLineParser.Object.CsvDirectory?.Length > 0)
                    {
                        if (Directory.Exists(_fluentCommandLineParser.Object.CsvDirectory) == false)
                        {
                            _logger.Warn(
                                $"'{_fluentCommandLineParser.Object.CsvDirectory} does not exist. Creating...'");
                            Directory.CreateDirectory(_fluentCommandLineParser.Object.CsvDirectory);
                        }

                        var outName =
                            $"{DateTimeOffset.Now:yyyyMMddHHmmss}_RecentFileCacheParser_Output.{exportExt}";
                        var outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                        _fluentCommandLineParser.Object.CsvDirectory =
                            Path.GetFullPath(outFile);
                        _logger.Warn(
                            $"CSV output will be saved to '{Path.GetFullPath(outFile)}'");

                        try
                        {
                            sw1 = new StreamWriter(outFile);
                            var csv = new CsvWriter(sw1);
                            if (_fluentCommandLineParser.Object.CsvSeparator == false)
                            {
                                csv.Configuration.Delimiter = "\t";
                            }

                            csv.Configuration.HasHeaderRecord = true;


                            var foo = csv.Configuration.AutoMap<CsvOut>();
                            foo.Map(t => t.SourceAccessed)
                                .ConvertUsing(t => t.SourceAccessed.ToString(_dateTimeFormat));
                            foo.Map(t => t.SourceCreated).ConvertUsing(t => t.SourceCreated.ToString(_dateTimeFormat));
                            foo.Map(t => t.SourceModified)
                                .ConvertUsing(t => t.SourceModified.ToString(_dateTimeFormat));

                            csv.WriteHeader(typeof(CsvOut));
                            csv.NextRecord();

                            csv.WriteRecords(GetCsvFormat(rfc));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                $"Unable to open '{outFile}' for writing. Export canceled. Error: {ex.Message}");
                        }
                    }

                    if (_fluentCommandLineParser.Object.JsonDirectory?.Length > 0)
                    {
                        if (Directory.Exists(_fluentCommandLineParser.Object.JsonDirectory) == false)
                        {
                            _logger.Warn(
                                $"'{_fluentCommandLineParser.Object.JsonDirectory} does not exist. Creating...'");
                            Directory.CreateDirectory(_fluentCommandLineParser.Object.JsonDirectory);
                        }

                        _logger.Warn($"Saving json output to '{_fluentCommandLineParser.Object.JsonDirectory}'");

                        SaveJson(rfc, _fluentCommandLineParser.Object.JsonPretty,
                            _fluentCommandLineParser.Object.JsonDirectory);
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
                    $"Unable to access '{_fluentCommandLineParser.Object.File}'. Are you running as an administrator? Error: {ua.Message}");
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Error processing file '{_fluentCommandLineParser.Object.File}' Please send it to saericzimmerman@gmail.com. Error: {ex.Message}");
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


        private static void SetupNLog()
        {
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