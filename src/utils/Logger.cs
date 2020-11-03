using System;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Logging;

namespace Logging
{
    /// To use:
    /// Add following to `.csproj` file
    /// ```
    /// <PackageReference Include="Microsoft.Extensions.Logging" Version="3.1.7" />
    /// <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="3.1.7" />
    /// <PackageReference Include="Serilog.Sinks.Seq" Version="4.0.0" />
    /// <PackageReference Include="Serilog.Sinks.Console" Version="3.1.1" />
    /// <PackageReference Include="Serilog.Extensions.Logging" Version="3.0.1" />
    /// ```
    ///
    /// In `Program.Main()`
    /// * Import
    /// ```
    /// using static Logging.Logger;
    /// using Microsoft.Extensions.Logging;
    /// ```
    /// * Initialize once with:
    /// ```
    /// InitLogger();`
    /// ```
    ///
    /// To use throughout the program:
    /// * Import
    /// ```
    /// using static Logging.Logger;
    /// using Microsoft.Extensions.Logging;
    /// ```
    /// * In your code call `Log.LogInformation(message)`
    public class Logger
    {
        private static Microsoft.Extensions.Logging.ILogger _log { get; set; }

        private static Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory { get; set; }
        public static Microsoft.Extensions.Logging.ILogger Log => _log;

        public static Microsoft.Extensions.Logging.ILoggerFactory LoggerFactory => _loggerFactory;

        public static void InitLogger(LogLevel logLevel = LogLevel.Information)
        {
            LogEventLevel internalLogLevel = ToSerilogLogLevel(logLevel);
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                // Removing the timestamp to make messages more concise
                // .WriteTo.Console(outputTemplate: "[{Timestamp:o} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", internalLogLevel)
                // .WriteTo.ApplicationInsightsTraces(TelemetryConfiguration.Active)
                .CreateLogger();

            var loggerFactory = (ILoggerFactory)new LoggerFactory();
            loggerFactory.AddSerilog(serilogLogger);
            _loggerFactory = loggerFactory;
            _log = loggerFactory.CreateLogger("datasync");
        }

        private static LogEventLevel ToSerilogLogLevel(LogLevel logLevel) =>
            logLevel == LogLevel.Verbose ? LogEventLevel.Verbose
            : logLevel == LogLevel.Debug ? LogEventLevel.Debug
            : logLevel == LogLevel.Information ? LogEventLevel.Information
            : logLevel == LogLevel.Warning ? LogEventLevel.Warning
            : logLevel == LogLevel.Error ? LogEventLevel.Error
            : logLevel == LogLevel.Fatal ? LogEventLevel.Fatal
            : throw new Exception($"Unknown log level: {logLevel}");
    }
    public enum LogLevel
    {
        Verbose = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5
    }
}