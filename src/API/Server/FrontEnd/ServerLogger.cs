using CodeProject.AI.API.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
// using System.Runtime.Versioning;

namespace CodeProject.AI.API.Server.Frontend
{
    #pragma warning disable IDE1006 // Naming Styles
    /// <summary>
    /// Represents a single log entry
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// The id of the log entry
        /// </summary>
        public int id { get; set; }

        /// <summary>
        /// The timestamp as UTC time
        /// </summary>
        public DateTime timestamp { get; set; }

        /// <summary>
        /// The log entry itself
        /// </summary>
        public string? entry { get; set; }

        /// <summary>
        /// The logging level of this entry
        /// </summary>
        public string? level { get; set; }

        /// <summary>
        /// The category for this entry, as defined in .NET logging
        /// </summary>
        public string? category { get; set; }

        /// <summary>
        /// The label for this entry. Can be anything such as 'timing', 'setup' etc. Something that
        /// provides context for whoever is consuming this entry.
        /// </summary>
        public string? label { get; set; }
    }

    /// <summary>
    /// The Response when requesting the list of log entries
    /// </summary>
    public class LogListResponse : SuccessResponse
    {
        /// <summary>
        /// A list of log entries
        /// </summary>
        public LogEntry[]? entries { get; set; }
    }
    #pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    /// Configuration for the ServerLogger class
    /// </summary>
    public class ServerLoggerConfiguration
    {
        /// <summary>
        /// A dictionary of colours per logging level
        /// </summary>
        public Dictionary<LogLevel, ConsoleColor> LogLevels { get; set; } = new()
        {
            [LogLevel.Information] = ConsoleColor.Green
        };
    }

    /// <summary>
    /// A logging provider specifically for the AI server. 
    /// </summary>
    public sealed class ServerLogger : ILogger
    {
        private const int MaxLogEntries = 5000;

        private static readonly List<LogEntry> _latestLogEntries = new List<LogEntry>();
        private static readonly object _logListLock = new object();
        private static int _logEntriesRecorded = 0;

        private readonly string _categoryName;
        private readonly Func<ServerLoggerConfiguration> _getCurrentConfig;

        /// <summary>
        /// Creates a new instance of the ServerLogger class
        /// </summary>
        /// <param name="categoryName">The category of the logger</param>
        /// <param name="getCurrentConfig">A method to get the logging config</param>
        public ServerLogger(string categoryName,
                            Func<ServerLoggerConfiguration> getCurrentConfig)
        {
            _categoryName     = categoryName;
            _getCurrentConfig = getCurrentConfig;
        }

        /// <summary>
        /// Formats the message and creates a scope. Will be in play until it's disposed.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="state"></param>
        /// <returns></returns>
        public IDisposable BeginScope<TState>(TState state) => default!;

        /// <summary>
        /// Returns a value indicating whether or not logging is enabled for the given logging 
        /// level. A logging level is enabled if a color has been set in the confuiguration for
        /// the given level.
        /// </summary>
        /// <param name="logLevel">The logging level to check</param>
        /// <returns>True if enabled; false otherwise</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return _getCurrentConfig().LogLevels.ContainsKey(logLevel);
        }

        /// <summary>
        /// Formats and records / writes an informational log message
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="logLevel">The log level</param>
        /// <param name="eventId">the event ID</param>
        /// <param name="state">The state</param>
        /// <param name="exception">Any exception info</param>
        /// <param name="formatter">A formatter for the information</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                                Exception? exception,
                                Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            lock (_logListLock)
            {
                string category = "CodeProject";
                string message  = formatter(state, exception);
                string label    = string.Empty;

                // Trim the category down a little
                if (!string.IsNullOrEmpty(_categoryName))
                {
                    var parts = _categoryName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    category  = parts[^1];
                    if (parts.Any(p => p == "CodeProject"))
                        category = "CodeProject." + category;
                }

                // We're using the .NET logger which means we don't have a huge amount of control
                // when it comes to adding extra info. We'll encode category and label info in the
                // leg message itself using special markers: [[...]] for category, {{..}} for label

                MatchCollection matches = Regex.Matches(message, @"\[\[(?<cat>.*?)\]\](?<msg>.*)", 
                                                        RegexOptions.ExplicitCapture);
                if (matches.Count > 0 && matches[0].Groups.Count > 2)
                {
                    category = matches[0].Groups["cat"].Value;
                    message = matches[0].Groups["msg"].Value;
                }

                matches = Regex.Matches(message, @"{{(?<label>.*?)}}(?<msg>.*)",
                                        RegexOptions.ExplicitCapture);
                if (matches.Count > 0 && matches[0].Groups.Count > 2)
                {
                    label  = matches[0].Groups["label"].Value;
                    message = matches[0].Groups["msg"].Value;
                }

                ServerLoggerConfiguration config = _getCurrentConfig();

                ConsoleColor originalColor = Console.ForegroundColor;

                Console.ForegroundColor = config.LogLevels[logLevel];
                // Console.WriteLine($"[{eventId.Id,2}: {logLevel,-12}]");
                Console.Write($"{logLevel.ToString()[..5]} ");
                Console.ForegroundColor = originalColor;

                if (!string.IsNullOrWhiteSpace(category))
                    Console.Write($"{category}: ");

                Console.ForegroundColor = config.LogLevels[logLevel];
                Console.Write($"{message.Trim()}");

                Console.ForegroundColor = originalColor;
                Console.WriteLine();

                Console.ResetColor();

                AggregateLog(new LogEntry()
                {
                    id        = ++_logEntriesRecorded,
                    timestamp = DateTime.UtcNow,
                    entry     = string.IsNullOrWhiteSpace(category)? message : $"{category}: {message}",
                    level     = logLevel.ToString().ToLower(),
                    category  = category,
                    label     = label
                });
            }
        }

        private static void AggregateLog(LogEntry entry)
        {
            while (_latestLogEntries.Count > MaxLogEntries)
                _latestLogEntries.RemoveAt(0);

            _latestLogEntries.Add(entry);
        }

        /// <summary>
        /// Retrieves a list of log entries. We store a list of the last N log enties for quick
        /// retrieval via the API. This list is non-persisted: if the app restarts, this list is
        /// gone.
        /// </summary>
        /// <param name="lastId">Entries after this Id will be returned. Consider it as the "last
        /// id returned from a previous request for entries". ie Give me everything starting from
        /// where I left off last time.</param>
        /// <param name="count">The maximum number of entries to return</param>
        /// <returns>A List of LogEntry objects</returns>
        public static List<LogEntry> List(int lastId, int count)
        {
            List<LogEntry> entries = new List<LogEntry>();

            lock (_logListLock)
            {
                if (_latestLogEntries.Count > 0 && _latestLogEntries[^1].id > lastId)
                {
                    // Move down to the first log entry requested
                    int i = _latestLogEntries.Count - 1;
                    while (i > 0 && _latestLogEntries[i - 1].id > lastId)
                        i--;

                    int numItems = Math.Min(count, _latestLogEntries.Count - i);
                    if (numItems > 0)
                        entries = _latestLogEntries.GetRange(i, numItems);
                }
            }

            return entries;
        }
    }

    /// <summary>
    /// The server logger provider class
    /// </summary>
    // [UnsupportedOSPlatform("browser")]
    [ProviderAlias("AIServer")]
    public sealed class ServerLoggerProvider : ILoggerProvider
    {
        private readonly IDisposable _onChangeToken;
        private ServerLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, ServerLogger> _loggers =
                                                            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new instance of the ServerLoggerProvider class
        /// </summary>
        /// <param name="config"></param>
        public ServerLoggerProvider(IOptionsMonitor<ServerLoggerConfiguration> config)
        {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
        }

        /// <summary>
        /// Creates a new logger for the given category name
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new ServerLogger(name, GetCurrentConfig));
        }

        private ServerLoggerConfiguration GetCurrentConfig()
        {
            return _currentConfig;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _loggers.Clear();
            _onChangeToken.Dispose();
        }
    }

    /// <summary>
    /// Provides extension methods to anable registering the ServerLogger
    /// </summary>
    public static class ServerLoggerExtensions
    {
        /// <summary>
        /// Adds the server logger to the list of logging providers
        /// </summary>
        /// <param name="builder">The logging builder</param>
        /// <returns></returns>
        public static ILoggingBuilder AddServerLogger(this ILoggingBuilder builder)
        {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, ServerLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions
                <ServerLoggerConfiguration, ServerLoggerProvider>(builder.Services);

            return builder;
        }

        /// <summary>
        /// Adds the server logger to the list of logging providers
        /// </summary>
        /// <param name="builder">The logging builder</param>
        /// <param name="configure">The method to configuer the provider</param>
        /// <returns></returns>
        public static ILoggingBuilder AddServerLogger(this ILoggingBuilder builder,
                                                      Action<ServerLoggerConfiguration> configure)
        {
            builder.AddServerLogger();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}