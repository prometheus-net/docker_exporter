using Axinom.Toolkit;
using Prometheus;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DockerExporter
{
    public sealed class ExporterLogic
    {
        public string DockerUrl { get; set; }

        public ExporterLogic()
        {
            // Default value only valid if not running as container.
            // This is intended for development purposes only.
            if (Helpers.Environment.IsMicrosoftOperatingSystem())
            {
                DockerUrl = "npipe://./pipe/docker_engine";
            }
            else
            {
                DockerUrl = "unix:///var/run/docker.sock";
            }
        }

        public async Task RunAsync(CancellationToken cancel)
        {
            _log.Info($"Configured to probe Docker on {DockerUrl}");

            _tracker = new DockerTracker(new Uri(DockerUrl));

            Metrics.DefaultRegistry.AddBeforeCollectCallback(UpdateMetricsAsync);

            var server = new MetricServer(9417);
#if DEBUG
            _log.Info($"Open http://localhost:9417/metrics to initiate a probe.");
#endif

            server.Start();

            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(-1, cancel);
                }
                catch (TaskCanceledException) when (cancel.IsCancellationRequested)
                {
                    // Totally normal - we are exiting.
                    break;
                }
            }

            await server.StopAsync();
        }

        private DockerTracker? _tracker;

        /// <summary>
        /// Called before every Prometheus collection in order to update metrics.
        /// </summary>
        /// <remarks>
        /// The Docker API can be very slow at times, so there is a risk that the scrape will
        /// just time out under load. To avoid that, we enforce a maximum update duration and
        /// will give up on fetching new values if the update takes longer than that. If the
        /// threshold is crossed, we simply allow the scrape to proceed with stale data, while
        /// the update keeps running in the background, hopefully eventually succeeding.
        /// 
        /// If multiple parallel scrapes are made, the results from the first one will be used
        /// to satisfy all requests that come in while the data loading triggered by the first
        /// scrape is still being performed (even if we give up with the scrape before loading finishes).
        /// This acts as a primitive form of rate control to avoid overloading the fragile Docker API.
        /// The implementation for this is in DockerTracker.
        /// </remarks>
        private async Task UpdateMetricsAsync(CancellationToken cancel)
        {
            _log.Debug("Probing Docker.");

            using var inlineCancellation = new CancellationTokenSource(Constants.MaxInlineUpdateDuration);
            using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(inlineCancellation.Token, cancel);

            var updateTask = _tracker!.TryUpdateAsync()
                .WithAbandonment(combinedCancellation.Token);

            try
            {
                await updateTask;
            }
            catch (TaskCanceledException) when (inlineCancellation.IsCancellationRequested)
            {
                _log.Debug("Probe took too long - will return stale results and finish probe in background.");

                // This is expected if it goes above the inline threshold, and will be ignored.
                // Other exceptions are caught, logged, and ignored in DockerState itself.
                ExporterLogicMetrics.InlineTimeouts.Inc();
            }
            catch (Exception ex)
            {
                // Errors that reach this point are fatal errors that we should never hide.
                _log.Error(Helpers.Debug.GetAllExceptionMessages(ex));
                _log.Error(ex.ToString());
                Environment.Exit(-1);
            }
        }

        private static readonly LogSource _log = Log.Default;
    }
}
