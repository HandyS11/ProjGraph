using ProjGraph.Tests.Smoke.Aot.Helpers;

namespace ProjGraph.Tests.Smoke.Aot;

/// <summary>
/// Guards the suite's premise: the builds under test must be Native AOT executables. A JIT apphost
/// or a framework-dependent <c>.dll</c> matches the reference trivially, so a misconfigured job, or a
/// tool package that lost <c>PublishAot</c>, would otherwise pass every parity test.
/// </summary>
public sealed class NativeBuildGuardTests
{
    [AotSmokeFact]
    public void CliNative_ShouldBeNativeAotExecutable()
    {
        AssertNativeAotExecutable(SmokeEnvironment.CliNative);
    }

    [AotSmokeFact]
    public void McpNative_ShouldBeNativeAotExecutable()
    {
        AssertNativeAotExecutable(SmokeEnvironment.McpNative);
    }

    private static void AssertNativeAotExecutable(SmokeCommand command)
    {
        command.LeadingArguments.Should().BeEmpty("a Native AOT build starts directly, not through the dotnet host");

        // dotnet tool install links the command to the executable in its package store on Linux and macOS.
        var executable = new FileInfo(command.FileName);
        var target = executable.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? executable.FullName;

        // A JIT apphost always ships its managed entry point beside it: <name>.dll for <name> or <name>.exe.
        var stem = OperatingSystem.IsWindows() ? Path.ChangeExtension(target, extension: null) : target;
        File.Exists(stem + ".dll").Should().BeFalse(
            $"{target} has a managed entry point beside it, so it is a JIT apphost, not a Native AOT executable");
    }
}
