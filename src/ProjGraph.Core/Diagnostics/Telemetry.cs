using System.Diagnostics;

namespace ProjGraph.Core.Diagnostics;

public static class Telemetry
{
    public const string ServiceName = "ProjGraph";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
}
