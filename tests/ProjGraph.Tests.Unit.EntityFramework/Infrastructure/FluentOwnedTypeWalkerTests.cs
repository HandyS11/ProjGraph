using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework.Infrastructure;

public sealed class FluentOwnedTypeWalkerTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", "fixtures", fileName);

    internal static EfModel Analyze(string fileName, string contextName)
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge, as in EfGoldenRunner.
        return service.AnalyzeContextAsync(FixturePath(fileName), contextName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    [Fact]
    public void OwnsOne_LambdaForm_CapturesOwnedEntityWithOwnerMetadata()
    {
        var model = Analyze("OwnedAndJoinContext.cs", "OwnedAndJoinContext");

        var address = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        address.Name.Should().Be("Address");
        address.OwnerEntity.Should().Be("Customer");
        address.NavigationName.Should().Be("Address");
        address.IsCollection.Should().BeFalse();
        address.TableName.Should().Be("Customer",
            "OwnsOne without ToTable is table-splitting, so it maps to the owner's effective table");
    }

    [Fact]
    public void OwnsOne_LambdaForm_AppliesNestedPropertyConfigurationToOwnedNotOwner()
    {
        var model = Analyze("OwnedAndJoinContext.cs", "OwnedAndJoinContext");

        var address = model.Entities.Single(e => e.IsOwned);
        address.Properties.Should().ContainSingle(p => p.Name == "City")
            .Which.MaxLength.Should().Be(50);

        var customer = model.Entities.Single(e => e.Name == "Customer");
        customer.Properties.Should().NotContain(p => p.Name == "City",
            "nested owned config must never leak onto the owner");
        customer.Properties.Should().NotContain(p => p.Name == "Address",
            "the owned navigation is not a column");
    }
}
