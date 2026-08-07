using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Regression tests for a live-found gap: entity-file discovery searched only the context directory and
/// its immediate parent, while base-class discovery already walked up to the workspace root. In the
/// standard layered layout — entities in <c>src/Core</c>, the DbContext in <c>src/Infrastructure/Data/</c> —
/// the entity CLR files sit two or more levels above the context directory, so none of them were ever read
/// and every column had to come from an explicit fluent <c>Property()</c> call. Found by validating against
/// ardalis/CleanArchitecture, where <c>Contributor.PhoneNumber</c> (a bare <c>OwnsOne</c> whose CLR type
/// lives in the Core project) rendered as an empty <c>PhoneNumber {}</c> box, and against eShopOnWeb, where
/// <c>Order.OrderDate</c> and <c>CatalogItem.Description</c> were missing entirely.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class CrossProjectEntityDiscoveryTests
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
    public async Task AnalyzeContextAsync_EntityInSiblingProject_ClrPropertiesCaptured()
    {
        using var temp = new TestDirectory();

        // Layered layout: the solution marker sits at the root, the entity lives in one project and the
        // DbContext in another, nested a directory deeper (src/Infrastructure/Data) exactly as EF templates
        // scaffold it. Customer carries no fluent configuration at all, so its columns can only reach the
        // model by reading Customer.cs — which the old one-parent search radius never reached.
        temp.CreateFile("App.slnx", "<Solution />");
        temp.CreateFile("src/Core/Customer.cs", """
            namespace Test;

            public class Customer
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public string Email { get; set; } = "";
            }
            """);
        var contextPath = temp.CreateFile("src/Infrastructure/Data/ShopContext.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class ShopContext : DbContext
            {
                public DbSet<Customer> Customers { get; set; } = null!;
            }
            """);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "ShopContext");

        var customer = model.Entities.SingleOrDefault(e => e.Name == "Customer");
        customer.Should().NotBeNull("the DbSet<Customer> entity must be discovered across the project boundary");
        customer.Properties.Select(p => p.Name).Should().BeEquivalentTo(["Id", "Name", "Email"],
            "Customer has no fluent configuration, so these columns prove Customer.cs was read");
    }

    [Fact]
    public async Task AnalyzeContextAsync_OwnedTypeInSiblingProject_ColumnsCaptured()
    {
        using var temp = new TestDirectory();

        // The ardalis/CleanArchitecture shape: a bare OwnsOne with no property configuration, whose CLR
        // type lives in another project. Without the widened radius the owned entity is captured with zero
        // properties and renders as an empty box.
        temp.CreateFile("App.slnx", "<Solution />");
        // The navigation is declared nullable (`PhoneNumber?`), exactly as ardalis/CleanArchitecture
        // declares it: the owned CLR type must be resolved through the nullable annotation rather than
        // being read as the type `Nullable`.
        temp.CreateFile("src/Core/Contributor.cs", """
            namespace Test;

            public class Contributor
            {
                public int Id { get; set; }
                public PhoneNumber? PhoneNumber { get; private set; }
            }
            """);
        temp.CreateFile("src/Core/PhoneNumber.cs", """
            namespace Test;

            public class PhoneNumber(string countryCode, string number)
            {
                public string CountryCode { get; private set; } = countryCode;
                public string Number { get; private set; } = number;
            }
            """);
        var contextPath = temp.CreateFile("src/Infrastructure/Data/AppDbContext.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class AppDbContext : DbContext
            {
                public DbSet<Contributor> Contributors { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Contributor>().OwnsOne(c => c.PhoneNumber);
                }
            }
            """);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "AppDbContext");

        var owned = model.Entities.SingleOrDefault(e => e.Key == "Contributor.PhoneNumber");
        owned.Should().NotBeNull("the owned navigation must be captured");
        owned.Properties.Select(p => p.Name).Should().BeEquivalentTo(["CountryCode", "Number"],
            "the owned type's columns can only come from CLR seeding of PhoneNumber.cs in the sibling project");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ConfigurationInUnrelatedSolution_DoesNotLeakEntities()
    {
        using var temp = new TestDirectory();

        // A repository holding more than one solution — the shape of ardalis/CleanArchitecture, which
        // carries `sample/` and `MinimalClean/` trees alongside the main one. Widening entity discovery to
        // the repository root must not drag in IEntityTypeConfiguration<T> classes from a sibling solution:
        // those configure entities this DbContext never declares.
        temp.CreateFile("App.slnx", "<Solution />");
        temp.CreateFile("src/Core/Customer.cs", """
            namespace Test;

            public class Customer
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);
        var contextPath = temp.CreateFile("src/Infrastructure/Data/ShopContext.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class ShopContext : DbContext
            {
                public DbSet<Customer> Customers { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShopContext).Assembly);
                }
            }
            """);

        // A second, self-contained solution nested inside the same repository, declaring a context class
        // of the SAME name. This is what leaked on ardalis/CleanArchitecture: the nested MinimalClean
        // solution's AppDbContext contributed its DbSets (Cart, GuestUser, Order, …) to an ERD of the main
        // solution's AppDbContext, which declares only DbSet<Contributor>.
        temp.CreateFile("sample/Sample.slnx", "<Solution />");
        temp.CreateFile("sample/src/Widget.cs", """
            namespace Other;

            public class Widget
            {
                public int Id { get; set; }
                public string Label { get; set; } = "";
            }
            """);
        temp.CreateFile("sample/src/Web/Infrastructure/Data/Config/WidgetConfiguration.cs", """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            namespace Other;

            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                {
                    builder.Property(w => w.Label).IsRequired().HasMaxLength(50);
                }
            }
            """);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "ShopContext");

        model.Entities.Select(e => e.Name).Should().NotContain("Widget",
            "Widget belongs to a different solution nested in the same repository and is not declared by this DbContext");
        model.Entities.Select(e => e.Name).Should().Contain("Customer",
            "the analysed solution's own entity must still be found");
    }

    [Fact]
    public async Task AnalyzeContextAsync_MigrationClassSharesEntityName_ResolvesTheRealType()
    {
        using var temp = new TestDirectory();

        // EF names a migration class after the change it makes, so `dotnet ef migrations add PhoneNumber`
        // produces `public partial class PhoneNumber : Migration` right beside the DbContext — nearer than
        // the value object it is named for. Matching it made the owned navigation resolve to a type with no
        // columns (rendered as an empty box on ardalis/CleanArchitecture). Migration classes are never
        // entities and must be skipped regardless of how close they sit.
        temp.CreateFile("App.slnx", "<Solution />");
        temp.CreateFile("src/Core/Contributor.cs", """
            namespace Test;

            public class Contributor
            {
                public int Id { get; set; }
                public PhoneNumber? PhoneNumber { get; private set; }
            }
            """);
        temp.CreateFile("src/Core/PhoneNumber.cs", """
            namespace Test;

            public class PhoneNumber
            {
                public string CountryCode { get; private set; } = "";
                public string Number { get; private set; } = "";
            }
            """);
        temp.CreateFile("src/Infrastructure/Data/Migrations/20231218143922_PhoneNumber.cs", """
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace Test.Migrations;

            public partial class PhoneNumber : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """);
        // The designer half of the same partial class, written with fully qualified names: migrations are
        // generated code, so neither the base type nor the attribute is guaranteed to be unqualified.
        temp.CreateFile("src/Infrastructure/Data/Migrations/20231218143922_PhoneNumber.Designer.cs", """
            namespace Test.Migrations;

            [Microsoft.EntityFrameworkCore.Migrations.Migration("20231218143922_PhoneNumber")]
            public partial class PhoneNumber : Microsoft.EntityFrameworkCore.Migrations.Migration
            {
            }
            """);
        var contextPath = temp.CreateFile("src/Infrastructure/Data/AppDbContext.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class AppDbContext : DbContext
            {
                public DbSet<Contributor> Contributors { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Contributor>().OwnsOne(c => c.PhoneNumber);
                }
            }
            """);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "AppDbContext");

        var owned = model.Entities.SingleOrDefault(e => e.Key == "Contributor.PhoneNumber");
        owned.Should().NotBeNull();
        owned.Properties.Select(p => p.Name).Should().BeEquivalentTo(["CountryCode", "Number"],
            "the value object must win over the identically named migration class");
    }

    [Fact]
    public void BuildSearchDirectories_ContextNestedUnderSolutionRoot_IncludesThatRoot()
    {
        using var temp = new TestDirectory();

        temp.CreateFile("App.slnx", "<Solution />");
        var contextPath = temp.CreateFile("src/Infrastructure/Data/AppDbContext.cs", "// context");
        var contextDirectory = Path.GetDirectoryName(contextPath)!;

        var result = new EntityFileDiscovery(new PhysicalFileSystem()).BuildSearchDirectories(contextDirectory);

        result.Should().Contain(contextDirectory, "the context directory is always searched first");
        result.Should().Contain(temp.DirectoryPath,
            "the enclosing solution root bounds the search, so sibling projects are reachable");
    }

    [Fact]
    public void BuildSearchDirectories_NoWorkspaceMarkerAnywhere_DoesNotEscapeToTemp()
    {
        using var temp = new TestDirectory();

        var contextPath = temp.CreateFile("src/Infrastructure/Data/AppDbContext.cs", "// context");
        var contextDirectory = Path.GetDirectoryName(contextPath)!;

        var result = new EntityFileDiscovery(new PhysicalFileSystem()).BuildSearchDirectories(contextDirectory);

        result.Should().NotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
            "with no marker to bound it the walk must stop rather than scan the whole temp directory");
    }
}
