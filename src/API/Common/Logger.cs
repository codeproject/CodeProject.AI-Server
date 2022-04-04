using System;
using System.Collections.Generic;

namespace CodeProject.SenseAI.API.Common
{
    #pragma warning disable IDE1006 // Naming Styles
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
    }
    #pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    /// About as simple an implementation of a message log queue as possible. This provides the
    /// ability to store messages which, in turn, can be displayed by whatever GUI is monitoring
    /// the system via the Log API. Currently a static object.
    /// TODO: Make this a more sensible object and inject into LogController and anywhere else
    /// that needs it via DI (and obviously ensure it's a singleton, or switch to a shared list of
    /// log entries).
    /// </summary>
    public class Logger
    {
        private const int MaxLogEntries = 1000;

        private static readonly List<LogEntry> _logs = new List<LogEntry>();
        private static readonly object _lock = new object();
        private static int _logCounter = 0;

        /// <summary>
        /// Adds an entry to the logs
        /// </summary>
        /// <param name="message">The message for the log entry</param>
        public static void Log(string message)
        {
            // Console.WriteLine($"{DateTime.Now.ToShortTimeString()}: {message}");

            lock (_lock)
            {
                while (_logs.Count > MaxLogEntries)
                    _logs.RemoveAt(0);

                _logs.Add(new LogEntry()
                {
                    id = ++_logCounter,
                    timestamp = DateTime.UtcNow,
                    entry = message
                });
            }
        }

        /// <summary>
        /// Retrieves a list of log entries
        /// </summary>
        /// <param name="lastId">Entries after this Id will be returned. Consider it as the "last
        /// id returned from a previous request for entries". ie Give me everything starting from
        /// where I left off last time.</param>
        /// <param name="count">The maximum number of entries to return</param>
        /// <returns>A List of LogEntry objects</returns>
        public static List<LogEntry> List(int lastId, int count)
        {
            List<LogEntry> entries = new List<LogEntry>();

            lock (_lock)
            {
                if (_logs.Count > 0 && _logs[^1].id > lastId)
                {
                    // Move down to the first log entry requested
                    int i = _logs.Count - 1;
                    while (i > 0 && _logs[i - 1].id > lastId)
                        i--;

                    int numItems = Math.Min(count, _logs.Count - i);
                    if (numItems > 0)
                        entries = _logs.GetRange(i, numItems);
                }
            }

            return entries;
        }
    }
}
