using Prometheus;
using System;

namespace DockerExporter
{
    static class Constants
    {
        // Will be replaced with real version string (AssemblyInfo number + build parameters) on automated build.
        public const string VersionString = "__VERSIONSTRING__";

        /// <summary>
        /// Docker can sometimes be slow to respond. If that is the case, we just give up and try
        /// again later. This limit is applied per individual API call, so does not reflect the
        /// total possible duration of a scrape, which is handled by the timeout values below.
        /// </summary>
        public static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// We are willing to delay a single scrape up to this long to wait for fresh data.
        /// Beyond this point, the update can still continue but will be done in the background.
        /// </summary>
        public static readonly TimeSpan MaxInlineUpdateDuration = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Even if the update happens in the background, it will be cancelled if it takes
        /// more time than this. The next scrape will try again from scratch.
        /// </summary>
        public static readonly TimeSpan MaxTotalUpdateDuration = TimeSpan.FromMinutes(2);

        /// <summary>
        /// The default buckets used to measure Docker probe operation durations.
        /// </summary>
        public static readonly double[] DurationBuckets = Histogram.ExponentialBuckets(0.5, 1.5, 14);
    }
}
