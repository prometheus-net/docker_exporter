using Prometheus;
using System;

namespace DockerExporter
{
    sealed class ContainerTrackerMetrics : IDisposable
    {
        public Counter.Child FailedProbeCount { get; }

        // These two are NOT differentiated by container, just to avoid a large number of series for each container.
        // Aggregate results seem useful, container scope less so. Can be expanded in the future if need be.
        public Histogram InspectContainerDuration => BaseInspectContainerDuration;
        public Histogram GetResourceStatsDuration => BaseGetResourceStatsDuration;

        public ContainerTrackerMetrics(string displayName)
        {
            FailedProbeCount = BaseFailedProbeCount.WithLabels(displayName);
        }

        public void Dispose()
        {
            FailedProbeCount.Remove();
        }

        private static readonly Counter BaseFailedProbeCount = Metrics.CreateCounter("docker_probe_container_failed_total", "Number of times the exporter failed to collect information about a specific container.", new CounterConfiguration
        {
            LabelNames = new[] { "name" }
        });

        private static readonly Histogram BaseInspectContainerDuration = Metrics
            .CreateHistogram("docker_probe_inspect_duration_seconds", "How long it takes to query Docker for the basic information about a single container. Includes failed requests.", new HistogramConfiguration
            {
                Buckets = Constants.DurationBuckets
            });

        private static readonly Histogram BaseGetResourceStatsDuration = Metrics
            .CreateHistogram("docker_probe_stats_duration_seconds", "How long it takes to query Docker for the resource usage of a single container. Includes failed requests.", new HistogramConfiguration
            {
                Buckets = Constants.DurationBuckets
            });
    }
}
