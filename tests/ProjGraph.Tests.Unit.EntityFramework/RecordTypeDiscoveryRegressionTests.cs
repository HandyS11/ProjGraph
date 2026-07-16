using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// End-to-end regressions for the file-discovery layer's record blind spots (issue #163's hazard
/// family): <see cref="EntityFileDiscovery"/> scanned for <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// classes and for entity base types with <c>ClassDeclarationSyntax</c> only, so a record-declared
/// config class or base entity in a SEPARATE file was never pulled into the compilation — making the
/// record widenings in <see cref="EntityConfigurationWalker"/> and <see cref="EntityAnalyzer"/>
/// unreachable for exactly the cross-file case they exist for. Fixtures live in isolated
/// <see cref="TestDirectory"/> instances, matching <c>OwnedRecordOwnerRegressionTests</c>.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class RecordTypeDiscoveryRegressionTests
{
    private static EfAnalysisService CreateService()
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        return new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
    }

    [Fact]
    public async Task AnalyzeContextAsync_RecordConfigClassInSeparateFile_ConfigureBodyIsFolded()
    {
        using var temp = new TestDirectory();

        const string contextContent = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class RecordConfigContext : DbContext
            {
                public DbSet<Widget> Widgets { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                    => modelBuilder.ApplyConfiguration(new WidgetConfiguration());
            }

            public class Widget
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """;
        const string configContent = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            namespace Test;

            public record WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                    => builder.Property(w => w.Name).HasMaxLength(64);
            }
            """;

        var contextPath = temp.CreateFile("Context.cs", contextContent);
        temp.CreateFile("WidgetConfiguration.cs", configContent);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "RecordConfigContext");

        var widget = model.Entities.Should().ContainSingle(e => e.Name == "Widget").Subject;
        widget.Properties.Should().ContainSingle(p => p.Name == "Name")
            .Which.MaxLength.Should().Be(64,
                "the record-declared config class lives in a separate file, so it only reaches the " +
                "walker if the cross-file config discovery scans record declarations too");
    }

    [Fact]
    public async Task AnalyzeContextAsync_RecordEntityWithRecordBaseInSeparateFile_BaseColumnsCaptured()
    {
        using var temp = new TestDirectory();

        const string contextContent = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class RecordBaseContext : DbContext
            {
                public DbSet<AuditedInvoice> Invoices { get; set; } = null!;
            }

            public record AuditedInvoice : InvoiceBase
            {
                public decimal Total { get; set; }
            }
            """;
        const string baseContent = """
            namespace Test;

            public record InvoiceBase
            {
                public int Id { get; set; }
                public string CreatedBy { get; set; } = "";
            }
            """;

        var contextPath = temp.CreateFile("Context.cs", contextContent);
        temp.CreateFile("InvoiceBase.cs", baseContent);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "RecordBaseContext");

        var invoice = model.Entities.Should().ContainSingle(e => e.Name == "AuditedInvoice").Subject;
        invoice.Properties.Select(p => p.Name).Should().Contain(["Total", "Id", "CreatedBy"],
            "the record base type's file must be discovered through the record-aware base-name scan; " +
            "without it, InvoiceBase never joins the compilation and its inherited columns vanish");
        invoice.Properties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey,
            "the inherited Id follows the EF primary-key convention");
    }
}
