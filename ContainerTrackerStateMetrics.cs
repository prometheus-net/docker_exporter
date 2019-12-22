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
            _id = id;
            _displayName = displayName;

            RestartCount = BaseRestartCount.WithLabels(id, displayName);
            RunningState = BaseRunningState.WithLabels(id, displayName);
            StartTime = BaseStartTime.WithLabels(id, displayName);
        }

        private readonly string _id;
        private readonly string _displayName;

        public void Dispose()
        {
            BaseRestartCount.RemoveLabelled(_id, _displayName);
            BaseRunningState.RemoveLabelled(_id, _displayName);
            BaseStartTime.RemoveLabelled(_id, _displayName);
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
