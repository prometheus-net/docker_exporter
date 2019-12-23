using Prometheus;

namespace DockerExporter
{
    sealed class DockerTrackerMetrics
    {
        public static readonly Gauge ContainerCount = Metrics
            .CreateGauge("docker_containers", "Number of containers that exist.");

        public static readonly Counter ListContainersErrorCount = Metrics
            .CreateCounter("docker_probe_list_containers_failed_total", "How many times the attempt to list all containers has failed.");

        public static readonly Histogram ProbeDuration = Metrics
            .CreateHistogram("docker_probe_duration_seconds", "How long it takes to query Docker for the complete data set. Includes failed requests.", new HistogramConfiguration
            {
                Buckets = Constants.DurationBuckets
            });

        public static readonly Histogram ListContainersDuration = Metrics
            .CreateHistogram("docker_probe_list_containers_duration_seconds", "How long it takes to query Docker for the list of containers. Includes failed requests.", new HistogramConfiguration
            {
                Buckets = Constants.DurationBuckets
            });

        public static readonly Gauge SuccessfulProbeTime = Metrics
            .CreateGauge("docker_probe_successfully_completed_time", "When the last Docker probe was successfully completed.", new GaugeConfiguration
            {
                SuppressInitialValue = true
            });
    }
}
