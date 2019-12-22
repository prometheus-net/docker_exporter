using Prometheus;

namespace DockerExporter
{
    static class ExporterLogicMetrics
    {
        public static readonly Counter InlineTimeouts = Metrics.CreateCounter("docker_probe_inline_timeouts_total", "Total number of times we have forced the scrape to happen in the background and returned outdated data because performing an update inline took too long.");
    }
}
