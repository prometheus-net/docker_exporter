using Axinom.Toolkit;
using Docker.DotNet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DockerExporter
{
    public sealed class ExporterLogic
    {
        public string DockerUrl { get; set; }

        public ExporterLogic()
        {
            // Default value only valid if not running as container.
            if (Helpers.Environment.IsMicrosoftOperatingSystem())
            {
                DockerUrl = "npipe://./pipe/docker_engine";
            }
            else
            {
                DockerUrl = "unix:///var/run/docker.sock";
            }
        }

        public async Task RunAsync(CancellationToken cancel)
        {
            _log.Info($"Connecting to Docker via {DockerUrl}");

            var clientConfig = new DockerClientConfiguration(new Uri(DockerUrl), null, Constants.DockerCommandTimeout);

            using (var client = clientConfig.CreateClient())
            {
                var allContainers = await client.Containers.ListContainersAsync(new Docker.DotNet.Models.ContainersListParameters
                {
                    All = true
                }, cancel);

                _log.Info(Helpers.Debug.ToDebugString(allContainers));
            }
        }

        private static readonly LogSource _log = Log.Default;
    }
}
