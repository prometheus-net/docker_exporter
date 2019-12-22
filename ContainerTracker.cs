using Axinom.Toolkit;
using Prometheus;
using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

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

        public ContainerTracker(string id)
        {
            Id = id;
        }

        public void Dispose()
        {
            _resourceMetrics?.Dispose();
            _stateMetrics?.Dispose();
        }

        /// <summary>
        /// Requests the tracker to update its data set.
        /// </summary>
        /// <remarks>
        /// May be called multiple times concurrently.
        /// 
        /// Method does not throw exceptions on transient failures, merely logs and ignores them.
        /// </remarks>
        public async Task TryUpdateAsync(DockerClient client, CancellationToken cancel)
        {
            ContainerInspectResponse container;
            StatsRecorder resourceStatsRecorder = new StatsRecorder();

            try
            {
                // First, inspect to get some basic information.
                container = await client.Containers.InspectContainerAsync(Id, cancel);

                // Then query for the latest resource usage stats (if container is running).
                if (container.State.Running)
                {
                    await client.Containers.GetContainerStatsAsync(Id, new ContainerStatsParameters
                    {
                        Stream = false // Only get latest, then stop.
                    }, resourceStatsRecorder, cancel);
                }
            }
            catch (Exception ex)
            {
                // TODO: DockerTrackerMetrics.ListContainersErrorCount.Inc();
                _log.Error(Helpers.Debug.GetAllExceptionMessages(ex));
                _log.Debug(ex.ToString()); // Only to verbose output.

                // Errors are ignored - if we fail to get data, we just skip an update and log the failure.
                // The next update will hopefully get past the error.
                return;
            }

            // If anything goes wrong below, it is a fatal error not to be ignored, so not in the try block.

            // Now that we have the data assembled, update the metrics.
            if (_stateMetrics == null)
            {
                var displayName = GetDisplayNameOrId(container);
                _log.Debug($"First update of state metrics for {displayName} ({Id}).");
                _stateMetrics = new ContainerTrackerStateMetrics(Id, displayName);
            }

            UpdateStateMetrics(_stateMetrics, container);

            if (resourceStatsRecorder.Response != null)
            {
                if (_resourceMetrics == null)
                {
                    var displayName = GetDisplayNameOrId(container);
                    _log.Debug($"Initializing resource metrics for {displayName} ({Id}).");
                    _resourceMetrics = new ContainerTrackerResourceMetrics(Id, displayName);
                }

                UpdateResourceMetrics(_resourceMetrics, container, resourceStatsRecorder.Response);
            }
            else
            {
                // TODO: It could be we already had resource metrics and now they should go away.
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
                var readEntries = resources.BlkioStats.IoServiceBytesRecursive
                    .Where(entry => entry.Op.Equals("read", StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                var writeEntries = resources.BlkioStats.IoServiceBytesRecursive
                    .Where(entry => entry.Op.Equals("write", StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                var totalRead = readEntries.Any() ? readEntries.Sum(entry => (long)entry.Value) : 0;
                var totalWrite = writeEntries.Any() ? writeEntries.Sum(entry => (long)entry.Value) : 0;

                metrics.TotalDiskBytesRead.Set(totalRead);
                metrics.TotalDiskBytesWrite.Set(totalWrite);
            }
        }

        private sealed class StatsRecorder : IProgress<ContainerStatsResponse>
        {
            public ContainerStatsResponse? Response { get; private set; }
            public void Report(ContainerStatsResponse value) => Response = value;
        }

        /// <summary>
        /// If a display name can be determined, returns it. Otherwise returns the container ID.
        /// </summary>
        private static string GetDisplayNameOrId(ContainerInspectResponse container)
        {
            if (!string.IsNullOrWhiteSpace(container.Name))
                return container.Name.Trim('/');

            return container.ID;
        }

        // We just need a monotonically increasing timer that does not use excessively large numbers (no 1970 base).
        private static readonly Stopwatch CpuBaselineTimer = Stopwatch.StartNew();

        private ContainerTrackerStateMetrics? _stateMetrics;
        private ContainerTrackerResourceMetrics? _resourceMetrics;

        private readonly LogSource _log = Log.Default;
    }
}
