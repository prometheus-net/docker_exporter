using Axinom.Toolkit;
using Docker.DotNet;
using Docker.DotNet.Models;
using Prometheus;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DockerExporter
{
    /// <summary>
    /// Tracks the status of one container and exports metrics, updating the data when new scrapes are requested.
    /// </summary>
    /// <remarks>
    /// NOT thread-safe! No concurrent usage is expected.
    /// DockerTracker performs the necessary synchronization logic.
    /// </remarks>
    sealed class ContainerTracker : IDisposable
    {
        public string Id { get; }
        public string DisplayName { get; }

        public ContainerTracker(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;

            _metrics = new ContainerTrackerMetrics(displayName);
        }

        public void Dispose()
        {
            _resourceMetrics?.Dispose();
            _stateMetrics?.Dispose();
        }

        public void Unpublish()
        {
            _resourceMetrics?.Unpublish();
            _stateMetrics?.Unpublish();
        }

        /// <summary>
        /// Requests the tracker to update its data set.
        /// </summary>
        /// <remarks>
        /// Method does not throw exceptions on transient failures, merely logs and ignores them.
        /// </remarks>
        public async Task TryUpdateAsync(DockerClient client, CancellationToken cancel)
        {
            ContainerInspectResponse container;
            var resourceStatsRecorder = new StatsRecorder();

            try
            {
                // First, inspect to get some basic information.
                using (_metrics.InspectContainerDuration.NewTimer())
                    container = await client.Containers.InspectContainerAsync(Id, cancel);

                // Then query for the latest resource usage stats (if container is running).
                if (container.State.Running)
                {
                    using var statsTimer = _metrics.GetResourceStatsDuration.NewTimer();
                    await client.Containers.GetContainerStatsAsync(Id, new ContainerStatsParameters
                    {
                        Stream = false // Only get latest, then stop.
                    }, resourceStatsRecorder, cancel);
                }
            }
            catch (Exception ex)
            {
                _metrics.FailedProbeCount.Inc();
                _log.Error(Helpers.Debug.GetAllExceptionMessages(ex));
                _log.Debug(ex.ToString()); // Only to verbose output.

                // Errors are ignored - if we fail to get data, we just skip an update and log the failure.
                // The next update will hopefully get past the error. For now, we just unpublish.
                Unpublish();
                return;
            }

            // If anything goes wrong below, it is a fatal error not to be ignored, so not in the try block.

            // Now that we have the data assembled, update the metrics.
            if (_stateMetrics == null)
            {
                _log.Debug($"First update of state metrics for {DisplayName} ({Id}).");
                _stateMetrics = new ContainerTrackerStateMetrics(DisplayName);
            }

            UpdateStateMetrics(_stateMetrics, container);

            if (resourceStatsRecorder.Response != null)
            {
                if (_resourceMetrics == null)
                {
                    _log.Debug($"Initializing resource metrics for {DisplayName} ({Id}).");
                    _resourceMetrics = new ContainerTrackerResourceMetrics(DisplayName);
                }

                UpdateResourceMetrics(_resourceMetrics, container, resourceStatsRecorder.Response);
            }
            else
            {
                // It could be we already had resource metrics and now they should go away.
                // They'll be recreated once we get the resource metrics again (e.g. after it starts).
                _resourceMetrics?.Dispose();
                _resourceMetrics = null;
            }
        }

        private void UpdateStateMetrics(ContainerTrackerStateMetrics metrics, ContainerInspectResponse container)
        {
            metrics.RestartCount.Set(container.RestartCount);

            if (container.State.Running)
                metrics.RunningState.Set(1);
            else if (container.State.Restarting)
                metrics.RunningState.Set(0.5);
            else
                metrics.RunningState.Set(0);

            if (container.State.Health != null)
            {
                // Publish container health if it exists
                if (container.State.Health.Status == "healthy")
                    metrics.HealthState.Set(1);
                else if (container.State.Health.Status == "starting")
                    metrics.HealthState.Set(0.5);
                else // "unhealthy"
                    metrics.HealthState.Set(0);
            }
            else
            {
                // Makes sure to unpublish it if it wasn't initially published
                metrics.HealthState.Unpublish();
            }

            if (container.State.Running && !string.IsNullOrWhiteSpace(container.State.StartedAt))
                metrics.StartTime.SetToTimeUtc(DateTimeOffset.Parse(container.State.StartedAt));
        }

        private void UpdateResourceMetrics(ContainerTrackerResourceMetrics metrics, ContainerInspectResponse container, ContainerStatsResponse resources)
        {
            // The resource reporting is very different for different operating systems.
            // This field is only used on Windows. We assume a container can't exist with 0 memory.
            bool isWindowsContainer = resources.MemoryStats.Commit != 0;

            // CPU usage
            // The mechanism of calculation is the rate of increase in container CPU time versus available ("system") CPU time.
            // The idea here is that we build two series - one counting used CPU in whatever units
            // the other counting potentially available CPU in whatever units. The % always comes right.
            // Docker CPU usage on Windows counts 100ns ticks.
            // Docker CPU usage on Linux counts unspecified ticks in relation to some other stats.
            // See https://github.com/moby/moby/blob/eb131c5383db8cac633919f82abad86c99bffbe5/cli/command/container/stats_helpers.go#L175
            if (isWindowsContainer)
            {
                // To compensate for core count on Windows, we normalize the container usage to a single core.
                // We also normalize the available CPU time to a single core.
                // This way the Windows calculation is always per-core averaged.
                // A .NET DateTimeOffset tick is 100ns, exactly, so matches what Docker uses.
                metrics.CpuCapacity.Set(CpuBaselineTimer.Elapsed.Ticks);
                metrics.CpuUsage.Set(resources.CPUStats.CPUUsage.TotalUsage / resources.NumProcs);
            }
            else
            {
                // This is counting all cores (right?).
                metrics.CpuCapacity.Set(resources.CPUStats.SystemUsage);
                metrics.CpuUsage.Set(resources.CPUStats.CPUUsage.TotalUsage);
            }

            // Memory usage
            if (isWindowsContainer)
            {
                // Windows reports Private Working Set in Docker stats... but seems to use Commit Bytes to enforce limit!
                // We want to report the same metric that is limited, so there we go.
                metrics.MemoryUsage.Set(resources.MemoryStats.Commit);
            }
            else
            {
                metrics.MemoryUsage.Set(resources.MemoryStats.Usage);
            }

            // Network I/O
            if (resources.Networks == null)
            {
                metrics.TotalNetworkBytesIn.Set(0);
                metrics.TotalNetworkBytesOut.Set(0);
            }
            else
            {
                metrics.TotalNetworkBytesIn.Set(resources.Networks.Values.Sum(n => (double)n.RxBytes));
                metrics.TotalNetworkBytesOut.Set(resources.Networks.Values.Sum(n => (double)n.TxBytes));
            }

            // Disk I/O
            if (isWindowsContainer)
            {
                metrics.TotalDiskBytesRead.Set(resources.StorageStats.ReadSizeBytes);
                metrics.TotalDiskBytesWrite.Set(resources.StorageStats.WriteSizeBytes);
            }
            else
            {
                var readEntries = resources.BlkioStats.IoServiceBytesRecursive?
                    .Where(entry => entry.Op.Equals("read", StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                var writeEntries = resources.BlkioStats.IoServiceBytesRecursive?
                    .Where(entry => entry.Op.Equals("write", StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                var totalRead = readEntries == null ? 0 : readEntries.Any() ? readEntries.Sum(entry => (long)entry.Value) : 0;
                var totalWrite = writeEntries == null ? 0 : writeEntries.Any() ? writeEntries.Sum(entry => (long)entry.Value) : 0;

                metrics.TotalDiskBytesRead.Set(totalRead);
                metrics.TotalDiskBytesWrite.Set(totalWrite);
            }
        }

        private sealed class StatsRecorder : IProgress<ContainerStatsResponse>
        {
            public ContainerStatsResponse? Response { get; private set; }
            public void Report(ContainerStatsResponse value) => Response = value;
        }

        // We just need a monotonically increasing timer that does not use excessively large numbers (no 1970 base).
        private static readonly Stopwatch CpuBaselineTimer = Stopwatch.StartNew();

        private ContainerTrackerMetrics _metrics;
        private ContainerTrackerStateMetrics? _stateMetrics;
        private ContainerTrackerResourceMetrics? _resourceMetrics;

        private readonly LogSource _log = Log.Default;
    }
}
