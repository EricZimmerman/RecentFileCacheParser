using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Exceptionless;
using Fclp;
using Fclp.Internals.Extensions;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace RecentFileCacheParser
{
    class Program
    {
        private static Logger _logger;
        private static FluentCommandLineParser<AppArgs> _fluentCommandLineParser;


        static void Main(string[] args)
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
                    "Directory to save CSV (tab separated) formatted results to. Be sure to include the full path in double quotes");

            _fluentCommandLineParser.Setup(arg => arg.XmlDirectory)
                .As("xml")
                .WithDescription(
                    "Directory to save XML formatted results to. Be sure to include the full path in double quotes");

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


            var header =
                $"LECmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/LECmd";
                

            var footer = @"Examples: LECmd.exe -f ""C:\Temp\foobar.lnk""" + "\r\n\t " +
                         @" LECmd.exe -f ""C:\Temp\somelink.lnk"" --json ""D:\jsonOutput"" --jsonpretty" + "\r\n\t " +
                         @" LECmd.exe -d ""C:\Temp"" --csv ""c:\temp"" --html c:\temp --xml c:\temp\xml -q" + "\r\n\t " +
                         @" LECmd.exe -f ""C:\Temp\some other link.lnk"" --nid --neb " + "\r\n\t " +
                         @" LECmd.exe -d ""C:\Temp"" --all" + "\r\n\t" + 
                         "\r\n\t"+
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

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() == false && !File.Exists(_fluentCommandLineParser.Object.File))
            {
                _logger.Warn($"File '{_fluentCommandLineParser.Object.File}' not found. Exiting");
                return;
            }

     

            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

       
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
}
