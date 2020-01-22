using Axinom.Toolkit;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace DockerExporter
{
    internal sealed class Program
    {
        private readonly LogSource _log = Log.Default;
        private readonly FilteringLogListener _filteringLogListener;

        private readonly ExporterLogic _logic = new ExporterLogic();

        private void Run(string[] args)
        {
            // We signal this to shut down the service.
            var cancel = new CancellationTokenSource();

            try
            {
                if (!ParseArguments(args))
                {
                    Environment.ExitCode = -1;
                    return;
                }

                _log.Info(GetVersionString());

                // Control+C will gracefully shut us down.
                Console.CancelKeyPress += (s, e) =>
                {
                    _log.Info("Canceling execution due to received signal.");
                    e.Cancel = true;
                    cancel.Cancel();
                };

                _logic.RunAsync(cancel.Token).WaitAndUnwrapExceptions();

                _log.Info("Application logic execution has completed.");
            }
            catch (OperationCanceledException)
            {
                if (cancel.IsCancellationRequested)
                {
                    // We really were cancelled. That's fine.
                }
                else
                {
                    _log.Error("Unexpected cancellation/timeout halted execution.");
                    Environment.ExitCode = -1;
                }
            }
            catch (Exception ex)
            {
                _log.Error(Helpers.Debug.GetAllExceptionMessages(ex));

                Environment.ExitCode = -1;
            }
        }

        private bool ParseArguments(string[] args)
        {
            var showHelp = false;
            var verbose = false;
            var debugger = false;

            var options = new OptionSet
            {
                GetVersionString(),
                "",
                "General",
                { "h|?|help", "Displays usage instructions.", val => showHelp = val != null },
                { "docker-url=", $"URL to use for accessing Docker. Defaults to {_logic.DockerUrl}", val => _logic.DockerUrl = val },

                "",
                "Diagnostics",
                { "verbose", "Displays extensive diagnostic information.", val => verbose = val != null },
                { "debugger", "Requests a debugger to be attached before execution starts.", val => debugger = val != null, true },
            };

            List<string> remainingOptions;

            try
            {
                remainingOptions = options.Parse(args);

                if (showHelp)
                {
                    options.WriteOptionDescriptions(Console.Out);
                    return false;
                }

                if (verbose)
                    _filteringLogListener.MinimumSeverity = LogEntrySeverity.Debug;
            }
            catch (OptionException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("For usage instructions, use the --help command line parameter.");
                return false;
            }

            if (remainingOptions.Count != 0)
            {
                Console.WriteLine("Unknown command line parameters: {0}", string.Join(" ", remainingOptions.ToArray()));
                Console.WriteLine("For usage instructions, use the --help command line parameter.");
                return false;
            }

            if (debugger)
                Debugger.Launch();

            return true;
        }

        private string GetVersionString()
        {
            return $"{typeof(Program).Namespace} v{Constants.VersionString}";
        }

        private Program()
        {
            // We default to displaying Info or higher but allow this to be reconfiured later, if the user wishes.
            _filteringLogListener = new FilteringLogListener(new ConsoleLogListener())
            {
#if !DEBUG
                MinimumSeverity = LogEntrySeverity.Info
#endif
            };

            Log.Default.RegisterListener(_filteringLogListener);
        }

        private static void Main(string[] args)
        {
            new Program().Run(args);
        }
    }
}
