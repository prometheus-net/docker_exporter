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

        public ContainerTrackerResourceMetrics(string displayName)
        {
            CpuUsage = BaseCpuUsage.WithLabels(displayName);
            CpuCapacity = BaseCpuCapacity.WithLabels(displayName);
            MemoryUsage = BaseMemoryUsage.WithLabels(displayName);
            TotalNetworkBytesIn = BaseTotalNetworkBytesIn.WithLabels(displayName);
            TotalNetworkBytesOut = BaseTotalNetworkBytesOut.WithLabels(displayName);
            TotalDiskBytesRead = BaseTotalDiskBytesRead.WithLabels(displayName);
            TotalDiskBytesWrite = BaseTotalDiskBytesWrite.WithLabels(displayName);
        }

        public void Dispose()
        {
            CpuUsage.Remove();
            CpuCapacity.Remove();
            MemoryUsage.Remove();
            TotalNetworkBytesIn.Remove();
            TotalNetworkBytesOut.Remove();
            TotalDiskBytesRead.Remove();
            TotalDiskBytesWrite.Remove();
        }

        public void Unpublish()
        {
            CpuUsage.Unpublish();
            CpuCapacity.Unpublish();
            MemoryUsage.Unpublish();
            TotalNetworkBytesIn.Unpublish();
            TotalNetworkBytesOut.Unpublish();
            TotalDiskBytesRead.Unpublish();
            TotalDiskBytesWrite.Unpublish();
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

        private static GaugeConfiguration ConfigureGauge() => new GaugeConfiguration
        {
            LabelNames = new[] { "name" },
            SuppressInitialValue = true
        };
    }
}
