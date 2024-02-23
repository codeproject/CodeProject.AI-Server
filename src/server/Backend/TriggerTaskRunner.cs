
using System;
using System.Diagnostics;
using System.IO;
using CodeProject.AI.SDK.Utils;
using Microsoft.Extensions.Logging;

namespace CodeProject.AI.Server.Backend
{
    /// <summary>
    /// For managing the life cycle of commands triggered by inference.
    /// </summary>
    public class TriggerTaskRunner
    {
        private ILogger<TriggerTaskRunner> _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">The logger</param>
        public TriggerTaskRunner(ILogger<TriggerTaskRunner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Runs a task command
        /// </summary>
        /// <param name="task">the task</param>
        /// <param name="workingDir">The working directory of this process</param>
        /// <returns>A Task</returns>
        public bool RunCommand(TriggerTask task, string workingDir = "")
        {
            if (task is null || string.IsNullOrWhiteSpace(task.Command))
                return false;

            Process? process = null;
            try
            {
                var procStartInfo = string.IsNullOrEmpty(task.Args)
                                  ? new ProcessStartInfo(task.Command)
                                  {
                                      UseShellExecute        = false,
                                      WorkingDirectory       = workingDir,
                                      CreateNoWindow         = false,
                                      RedirectStandardOutput = true,
                                      RedirectStandardError  = true
                                  }
                                  : new ProcessStartInfo(task.Command, task.Args)
                                  {
                                      UseShellExecute        = false,
                                      WorkingDirectory       = workingDir,
                                      CreateNoWindow         = false,
                                      RedirectStandardOutput = true,
                                      RedirectStandardError  = true
                                  };
                process = new Process
                {
                    StartInfo = procStartInfo,
                    EnableRaisingEvents = true
                };
                process.OutputDataReceived += SendOutputToLog;
                process.ErrorDataReceived  += SendErrorToLog;

                // Start the process
                _logger.LogTrace($"Starting {Text.ShrinkPath(task.Command, 50)} {Text.ShrinkPath(task.Args, 50)}");

                if (process.Start())
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error trying to start {task.Command} in {workingDir}");
                _logger.LogError("Error is: " + ex.Message);
                return false;
            }
        }

        private void SendOutputToLog(object sender, DataReceivedEventArgs data)
        {
            string? message = data?.Data;

            string filename = string.Empty;
            if (sender is Process process)
            {
                filename = Path.GetFileName(process.StartInfo.Arguments.Replace("\"", ""));
                if (string.IsNullOrWhiteSpace(filename))
                    filename = Path.GetFileName(process.StartInfo.FileName.Replace("\"", ""));

                if (process.HasExited && string.IsNullOrEmpty(message))
                    return;
            }

            if (string.IsNullOrWhiteSpace(message))
                return;

            if (!string.IsNullOrEmpty(filename))
                filename += ": ";

            var testString = message.ToLower();

            // We're picking up messages written to the console so let's provide a little help for
            // messages that are trying to get themselves categorised properly.
            // Optimisation: We probably should order these by info/trace/debug/warn/error/crit, but
            // for sanity we'll keep them in order of anxiety.
            if (testString.StartsWith("crit: "))
                _logger.LogCritical(filename + message.Substring("crit: ".Length));
            else if (testString.StartsWith("critical: "))
                _logger.LogCritical(filename + message.Substring("critical: ".Length));
            else if (testString.StartsWith("err: "))
                _logger.LogError(filename + message.Substring("err: ".Length));
            else if (testString.StartsWith("error:"))
                _logger.LogError(filename + message.Substring("error:".Length));
            else if (testString.StartsWith("warn: "))
                _logger.LogWarning(filename + message.Substring("warn: ".Length));
            else if (testString.StartsWith("warning:"))
                _logger.LogWarning(filename + message.Substring("warning:".Length));
            else if (testString.StartsWith("info: "))
                _logger.LogInformation(filename + message.Substring("info: ".Length));
            else if (testString.StartsWith("information: "))
                _logger.LogInformation(filename + message.Substring("information: ".Length));
            else if (testString.StartsWith("dbg: "))
                _logger.LogDebug(filename + message.Substring("dbg: ".Length));
            else if (testString.StartsWith("debug:"))
                _logger.LogDebug(filename + message.Substring("debug:".Length));
            else if (testString.StartsWith("trc: "))
                _logger.LogTrace(filename + message.Substring("trc: ".Length));
            else if (testString.StartsWith("trace:"))
                _logger.LogTrace(filename + message.Substring("trace:".Length));
            else
                _logger.LogInformation(filename + message);
        }

        private void SendErrorToLog(object sender, DataReceivedEventArgs data)
        {
            string? error = data?.Data;

            string filename = string.Empty;
            if (sender is Process process)
            {
                filename = Path.GetFileName(process.StartInfo.Arguments.Replace("\"", ""));
                if (string.IsNullOrWhiteSpace(filename))
                    filename = Path.GetFileName(process.StartInfo.FileName.Replace("\"", ""));

                // This same logic (and output) is sent to stdout so no need to duplicate here.
                // if (process.HasExited && string.IsNullOrEmpty(error))
                //    error = "has exited";
            }

            if (string.IsNullOrWhiteSpace(error))
                return;

            if (!string.IsNullOrEmpty(filename))
                filename += ": ";

            if (string.IsNullOrEmpty(error))
                error = "No error provided";

             _logger.LogError(filename + error);
        }
    }
}