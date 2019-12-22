using Prometheus;
using System;
using System.Linq;

namespace DockerExporter
{
    sealed class ContainerTrackerResourceMetrics : IDisposable
    {
        public Gauge.Child CpuUsage { get; private set; }
        public Gauge.Child CpuCapacity { get; private set; }
        public Gauge.Child MemoryUsage { get; private set; }
        public Gauge.Child TotalNetworkBytesIn { get; private set; }
        public Gauge.Child TotalNetworkBytesOut { get; private set; }
        public Gauge.Child TotalDiskBytesRead { get; private set; }
        public Gauge.Child TotalDiskBytesWrite { get; private set; }

        public ContainerTrackerResourceMetrics(string id, string displayName)
        {
            _id = id;
            _displayName = displayName;

            CpuUsage = BaseCpuUsage.WithLabels(id, displayName);
            CpuCapacity = BaseCpuCapacity.WithLabels(id, displayName);
            MemoryUsage = BaseMemoryUsage.WithLabels(id, displayName);
            TotalNetworkBytesIn = BaseTotalNetworkBytesIn.WithLabels(id, displayName);
            TotalNetworkBytesOut = BaseTotalNetworkBytesOut.WithLabels(id, displayName);
            TotalDiskBytesRead = BaseTotalDiskBytesRead.WithLabels(id, displayName);
            TotalDiskBytesWrite = BaseTotalDiskBytesWrite.WithLabels(id, displayName);
        }

        private readonly string _id;
        private readonly string _displayName;

        public void Dispose()
        {
            BaseCpuUsage.RemoveLabelled(_id, _displayName);
            BaseCpuCapacity.RemoveLabelled(_id, _displayName);
            BaseMemoryUsage.RemoveLabelled(_id, _displayName);
            BaseTotalNetworkBytesIn.RemoveLabelled(_id, _displayName);
            BaseTotalNetworkBytesOut.RemoveLabelled(_id, _displayName);
            BaseTotalDiskBytesRead.RemoveLabelled(_id, _displayName);
            BaseTotalDiskBytesWrite.RemoveLabelled(_id, _displayName);
        }

        // While logically counters, all of these are gauges because we do not know when Docker might reset the values.

        private static readonly Gauge BaseCpuUsage = Metrics
            .CreateGauge("docker_container_cpu_used_total", "Accumulated CPU usage of a container, in unspecified units, averaged for all logical CPUs usable by the container.", ConfigureGauge());

        private static readonly Gauge BaseCpuCapacity = Metrics
            .CreateGauge("docker_container_cpu_capacity_total", "All potential CPU usage available to a container, in unspecified units, averaged for all logical CPUs usable by the container. Start point of measurement is undefined - only relative values should be used in analytics.", ConfigureGauge());

        private static readonly Gauge BaseMemoryUsage = Metrics
            .CreateGauge("docker_container_memory_used_bytes", "Memory usage of a container.", ConfigureGauge());

        private static readonly Gauge BaseTotalNetworkBytesIn = Metrics
            .CreateGauge("docker_container_network_in_bytes", "Total bytes received by the container's network interfaces.", ConfigureGauge());

        private static readonly Gauge BaseTotalNetworkBytesOut = Metrics
            .CreateGauge("docker_container_network_out_bytes", "Total bytes sent by the container's network interfaces.", ConfigureGauge());

        private static readonly Gauge BaseTotalDiskBytesRead = Metrics
            .CreateGauge("docker_container_disk_read_bytes", "Total bytes read from disk by a container.", ConfigureGauge());

        private static readonly Gauge BaseTotalDiskBytesWrite = Metrics
            .CreateGauge("docker_container_disk_write_bytes", "Total bytes written to disk by a container.", ConfigureGauge());

        private static string[] LabelNames(params string[] extra) =>
            new[] { "id", "display_name" }.Concat(extra).ToArray();

        private static GaugeConfiguration ConfigureGauge() => new GaugeConfiguration
        {
            LabelNames = LabelNames(),
            SuppressInitialValue = true
        };
    }
}
