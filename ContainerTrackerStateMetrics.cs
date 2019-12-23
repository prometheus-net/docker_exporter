using Prometheus;
using System;
using System.Linq;

namespace DockerExporter
{
    sealed class ContainerTrackerStateMetrics : IDisposable
    {
        public Gauge.Child RestartCount { get; private set; }
        public Gauge.Child RunningState { get; private set; }
        public Gauge.Child StartTime { get; private set; }

        public ContainerTrackerStateMetrics(string id, string displayName)
        {
            RestartCount = BaseRestartCount.WithLabels(id, displayName);
            RunningState = BaseRunningState.WithLabels(id, displayName);
            StartTime = BaseStartTime.WithLabels(id, displayName);
        }

        public void Dispose()
        {
            RestartCount.Remove();
            RunningState.Remove();
            StartTime.Remove();
        }

        public void Unpublish()
        {
            RestartCount.Unpublish();
            RunningState.Unpublish();
            StartTime.Unpublish();
        }

        private static readonly Gauge BaseRestartCount = Metrics
            .CreateGauge("docker_container_restart_count", "Number of times the runtime has restarted this container without explicit user action, since the container was last started.", ConfigureGauge());

        private static readonly Gauge BaseRunningState = Metrics
            .CreateGauge("docker_container_running_state", "Whether the container is running (value 1), restarting (value 0.5) or stopped (value 0).", ConfigureGauge());

        private static readonly Gauge BaseStartTime = Metrics
            .CreateGauge("docker_container_start_time", "Timestamp indicating when the container was started. Does not get reset by automatic restarts.", ConfigureGauge());

        private static string[] LabelNames(params string[] extra) =>
            new[] { "id", "display_name" }.Concat(extra).ToArray();

        private static GaugeConfiguration ConfigureGauge() => new GaugeConfiguration
        {
            LabelNames = LabelNames(),
            SuppressInitialValue = true
        };
    }
}
