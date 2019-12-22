using Prometheus;

namespace DockerExporter
{
    sealed class DockerTrackerMetrics
    {
        public static readonly Gauge ContainerCount = Metrics
            .CreateGauge("docker_containers", "Number of containers that exist.");

        public static readonly Counter ListContainersErrorCount = Metrics
            .CreateCounter("docker_list_containers_failed_total", "How many times the attempt to list all containers has failed.");
    }
}
