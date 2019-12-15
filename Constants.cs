using System;

namespace DockerExporter
{
    static class Constants
    {
        // Will be replaced with real version string (AssemblyInfo number + build parameters) on automated build.
        public const string VersionString = "__VERSIONSTRING__";

        /// <summary>
        /// Docker can sometimes be slow to respond. If that is the case, we just give up and try again later.
        /// </summary>
        public static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromSeconds(30);
    }
}
