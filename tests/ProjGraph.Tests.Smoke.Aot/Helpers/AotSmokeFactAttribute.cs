namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// A fact that runs only when all four <c>PROJGRAPH_SMOKE_*</c> variables are set, so a plain
/// <c>dotnet test ProjGraph.slnx</c> reports the smoke suite as skipped instead of failing it. When
/// <c>PROJGRAPH_SMOKE_REQUIRED=true</c>, nothing is skipped and a missing variable fails the test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AotSmokeFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AotSmokeFactAttribute"/> class.
    /// </summary>
    public AotSmokeFactAttribute()
    {
        var missing = SmokeEnvironment.GetMissingVariables();
        if (missing.Count > 0 && !SmokeEnvironment.IsRequired)
        {
            Skip = $"Native AOT smoke suite not configured; set {string.Join(", ", missing)}.";
        }
    }
}
