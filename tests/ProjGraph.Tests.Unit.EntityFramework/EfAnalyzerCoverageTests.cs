using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Edge-case coverage for the Roslyn-backed EF analyzers: attribute shapes the happy path never sees,
/// navigation types that are not entity references, unusual CLR/SQL type spellings, and snapshots whose
/// <c>BuildModel</c> or <c>[DbContext]</c> attribute is missing or unusual.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class EfAnalyzerCoverageTests
{
    private static IPropertySymbol GetProperty(string source, string typeName, string propertyName)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, typeName)!;
        return type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == propertyName);
    }

    [Fact]
    public void AnalyzeEntity_TypeAttributeThatIsNotPrimaryKey_ShouldNotMarkAnyKey()
    {
        // [Serializable] is a resolvable type-level attribute that is not [PrimaryKey]; both the
        // semantic-model and the syntax pass must skip it rather than treat its arguments as key names.
        const string source = """
                              using System;

                              [Serializable]
                              public class Doc
                              {
                                  public string Code { get; set; }
                                  public string Title { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entity = EntityAnalyzer.AnalyzeEntity(RoslynTestHelper.GetTypeSymbol(compilation, "Doc")!);

        entity.Properties.Should().HaveCount(2);
        entity.Properties.Should().OnlyContain(p => !p.IsPrimaryKey);
    }

    [Fact]
    public void AnalyzeEntity_PrimaryKeyAttributeWithoutArguments_ShouldNotMarkAnyKey()
    {
        // A bare [PrimaryKey] names no columns, so there is nothing to promote — the parser must not
        // dereference the missing argument list.
        const string source = """
                              [PrimaryKey]
                              public class Doc
                              {
                                  public string Code { get; set; }
                                  public string Title { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entity = EntityAnalyzer.AnalyzeEntity(RoslynTestHelper.GetTypeSymbol(compilation, "Doc")!);

        entity.Properties.Should().OnlyContain(p => !p.IsPrimaryKey);
    }

    [Fact]
    public void AnalyzeEntity_PrimaryKeyWithQualifiedNameof_ShouldMarkThatProperty()
    {
        // nameof(Doc.Code) is the idiomatic spelling; the argument is a member access, not a bare
        // identifier, so the name has to be taken from the accessed member.
        const string source = """
                              [PrimaryKey(nameof(Doc.Code))]
                              public class Doc
                              {
                                  public string Code { get; set; }
                                  public string Title { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entity = EntityAnalyzer.AnalyzeEntity(RoslynTestHelper.GetTypeSymbol(compilation, "Doc")!);

        entity.Properties.Should().ContainSingle(p => p.IsPrimaryKey)
            .Which.Name.Should().Be("Code");
    }

    [Fact]
    public void AnalyzeEntity_PrimaryKeyWithNonNameofArgument_ShouldNotMarkAnyKey()
    {
        // A constant reference is not a column name the analyzer can resolve syntactically; guessing
        // "Code" from Keys.Code would be wrong whenever the constant's value differs from its name.
        const string source = """
                              public static class Keys
                              {
                                  public const string Code = "Code";
                              }

                              [PrimaryKey(Keys.Code)]
                              public class Doc
                              {
                                  public string Code { get; set; }
                                  public string Title { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entity = EntityAnalyzer.AnalyzeEntity(RoslynTestHelper.GetTypeSymbol(compilation, "Doc")!);

        entity.Properties.Should().OnlyContain(p => !p.IsPrimaryKey);
    }

    [Fact]
    public void AnalyzeEntity_ColumnAttributeWithoutTypeName_ShouldNotSetPrecision()
    {
        const string source = """
                              using System.ComponentModel.DataAnnotations.Schema;

                              public class Invoice
                              {
                                  public int Id { get; set; }

                                  [Column("total_amount", Order = 1)]
                                  public decimal Total { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entity = EntityAnalyzer.AnalyzeEntity(RoslynTestHelper.GetTypeSymbol(compilation, "Invoice")!);

        var total = entity.Properties.First(p => p.Name == "Total");
        total.Precision.Should().BeNull();
        total.Scale.Should().BeNull();
    }

    [Fact]
    public void AnalyzeEntity_ColumnTypeNameWithoutDecimalPrecision_ShouldNotSetPrecision()
    {
        // nvarchar(100) carries a length, not a precision/scale pair — reading "100" as a precision
        // would render a bogus "precision:100" constraint on a string column.
        const string source = """
                              using System.ComponentModel.DataAnnotations.Schema;

                              public class Invoice
                              {
                                  public int Id { get; set; }

                                  [Column(TypeName = "nvarchar(100)")]
                                  public string Reference { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entity = EntityAnalyzer.AnalyzeEntity(RoslynTestHelper.GetTypeSymbol(compilation, "Invoice")!);

        var reference = entity.Properties.First(p => p.Name == "Reference");
        reference.Precision.Should().BeNull();
        reference.Scale.Should().BeNull();
    }

    [Fact]
    public void AnalyzeEntity_EnumType_ShouldProduceNoColumns()
    {
        // An enum declaration is a BaseTypeDeclarationSyntax but not a TypeDeclarationSyntax, so the
        // syntax-based key scan must skip it instead of crashing on the unexpected node kind.
        const string source = """
                              public enum Status
                              {
                                  Draft = 0,
                                  Published = 1
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entity = EntityAnalyzer.AnalyzeEntity(RoslynTestHelper.GetTypeSymbol(compilation, "Status")!);

        entity.Name.Should().Be("Status");
        entity.Properties.Should().BeEmpty();
    }

    [Fact]
    public void IsNavigationProperty_ArrayOfEntities_ShouldReturnFalse()
    {
        // An array type is an IArrayTypeSymbol, not an INamedTypeSymbol; the analyzer reports it as a
        // non-navigation so it stays a scalar column rather than silently vanishing.
        const string source = """
                              public class Order { public int Id { get; set; } }

                              public class Customer
                              {
                                  public int Id { get; set; }
                                  public Order[] Orders { get; set; }
                              }
                              """;

        var prop = GetProperty(source, "Customer", "Orders");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out var target, out var isCollection);

        result.Should().BeFalse();
        target.Should().BeNull();
        isCollection.Should().BeFalse();
    }

    [Fact]
    public void IsNavigationProperty_SingleArgumentGenericThatIsNotACollection_ShouldReturnFalse()
    {
        // Lazy<Order> has exactly one type argument but is not a collection, so the element-type path
        // must not fire; falling through, Lazy itself is a System type and therefore not an entity.
        const string source = """
                              using System;

                              public class Order { public int Id { get; set; } }

                              public class Customer
                              {
                                  public int Id { get; set; }
                                  public Lazy<Order> Deferred { get; set; }
                              }
                              """;

        var prop = GetProperty(source, "Customer", "Deferred");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out _, out var isCollection);

        result.Should().BeFalse();
        isCollection.Should().BeFalse();
    }

    [Fact]
    public void IsNavigationProperty_CollectionOfArrays_ShouldReturnFalse()
    {
        // List<string[]>'s element type is an array symbol, not a named type, so no entity element can
        // be extracted and the property is not a navigation.
        const string source = """
                              using System.Collections.Generic;

                              public class Report
                              {
                                  public int Id { get; set; }
                                  public List<string[]> Rows { get; set; }
                              }
                              """;

        var prop = GetProperty(source, "Report", "Rows");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out _, out var isCollection);

        result.Should().BeFalse();
        isCollection.Should().BeFalse();
    }

    [Fact]
    public void HasInverseReference_SelfReferencingNavigation_ShouldReturnFalse()
    {
        // Node.Parent points back at Node, so the only candidate inverse is the property itself; a
        // navigation must never be treated as its own inverse.
        const string source = """
                              public class Node
                              {
                                  public int Id { get; set; }
                                  public Node Parent { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var node = RoslynTestHelper.GetTypeSymbol(compilation, "Node")!;
        var parent = node.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Parent");

        NavigationPropertyAnalyzer.HasInverseReference(parent, node).Should().BeFalse();
    }

    [Fact]
    public void HasInverseCollection_SelfReferencingCollection_ShouldReturnFalse()
    {
        const string source = """
                              using System.Collections.Generic;

                              public class Node
                              {
                                  public int Id { get; set; }
                                  public List<Node> Children { get; set; }
                              }
                              """;

        var compilation = RoslynTestHelper.CreateCompilation(source);
        var node = RoslynTestHelper.GetTypeSymbol(compilation, "Node")!;
        var children = node.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Children");

        NavigationPropertyAnalyzer.HasInverseCollection(children, node).Should().BeFalse();
    }

    [Fact]
    public void IsNullable_NullableValueTypeWithoutNrt_ShouldReturnTrue()
    {
        // Without an enabled nullable context the annotation is oblivious, so nullability has to be read
        // off the Nullable<T> type itself.
        const string source = """
                              public class Reading
                              {
                                  public int? Value { get; set; }
                              }
                              """;

        GetProperty(source, "Reading", "Value").Type.IsNullable().Should().BeTrue();
    }

    [Fact]
    public void IsEfValueType_NestedReferenceType_ShouldReturnFalse()
    {
        // A nested type's minimally-qualified display string is "Outer.Inner"; the fallback lookup has to
        // compare the last segment, not the whole dotted string.
        const string source = """
                              public class Outer
                              {
                                  public class Inner { }
                              }

                              public class Holder
                              {
                                  public Outer.Inner Nested { get; set; }
                              }
                              """;

        GetProperty(source, "Holder", "Nested").Type.IsEfValueType().Should().BeFalse();
    }

    [Fact]
    public void CreateWithDefaultValue_MethodCallExpression_ShouldPreserveTheRawExpression()
    {
        // A method call has no compile-time constant to resolve; the raw text is kept so the rendered
        // ERD still shows what was configured instead of silently dropping the default.
        var compilation = RoslynTestHelper.CreateCompilation("public class Anything { }");
        var property = new EfProperty { Name = "CreatedAt", Type = "DateTime" };

        var result = DefaultValueResolver.CreateWithDefaultValue(property, "GetUtcNow()", compilation);

        result.DefaultValue.Should().Be("GetUtcNow()");
    }

    [Fact]
    public void CreateWithDefaultValue_CastFollowedByCompoundExpression_ShouldPreserveTheRawExpression()
    {
        // The cast-stripping shortcut only applies when what follows the cast is a single token; here it
        // is an arithmetic expression, so the whole thing stays verbatim rather than being mangled.
        var compilation = RoslynTestHelper.CreateCompilation("public class Anything { }");
        var property = new EfProperty { Name = "Retries", Type = "int" };

        var result = DefaultValueResolver.CreateWithDefaultValue(property, "(int) 1 + 2", compilation);

        result.DefaultValue.Should().Be("(int) 1 + 2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ToClrType_BlankColumnType_ShouldReturnNull(string columnType)
    {
        SqlColumnTypeMapper.ToClrType(columnType).Should().BeNull();
    }

    [Fact]
    public void ToClrType_QuotedTypeWithPrecisionSuffix_ShouldMapToClrType()
    {
        // Snapshot files spell the column type as a quoted literal, e.g. "decimal(18,2)".
        SqlColumnTypeMapper.ToClrType("\"decimal(18,2)\"").Should().Be("decimal");
    }

    [Fact]
    public void ToClrType_StringBackedColumnType_ShouldReturnNull()
    {
        // nvarchar maps to string; returning null keeps the caller's already-correct string type rather
        // than round-tripping it through a guess.
        SqlColumnTypeMapper.ToClrType("nvarchar(200)").Should().BeNull();
    }

    [Fact]
    public void ToClrType_UnknownColumnType_ShouldReturnNull()
    {
        SqlColumnTypeMapper.ToClrType("hierarchyid").Should().BeNull();
    }

    private static (ClassDeclarationSyntax Class, INamedTypeSymbol Symbol, Compilation Compilation) LoadSnapshot(
        string source, string metadataName)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var symbol = compilation.GetTypeByMetadataName(metadataName)!;
        var declaration = (ClassDeclarationSyntax)symbol.DeclaringSyntaxReferences[0].GetSyntax();
        return (declaration, symbol, compilation);
    }

    [Fact]
    public void Parse_SnapshotWithDbContextAttribute_ShouldTakeContextNameFromTheAttribute()
    {
        // The snapshot class is named after the migration assembly's convention, not the context; the
        // [DbContext(typeof(T))] argument is the authoritative source of the context name.
        const string source = """
                              using System;

                              namespace Microsoft.EntityFrameworkCore.Infrastructure
                              {
                                  public sealed class DbContextAttribute : Attribute
                                  {
                                      public DbContextAttribute(Type contextType) => ContextType = contextType;

                                      public Type ContextType { get; }
                                  }
                              }

                              namespace App
                              {
                                  public class OrderingContext { }

                                  [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(OrderingContext))]
                                  public class AppSnapshot { }
                              }
                              """;

        var (declaration, symbol, compilation) = LoadSnapshot(source, "App.AppSnapshot");

        var model = ModelSnapshotParser.Parse(declaration, symbol, compilation);

        model.ContextName.Should().Be("OrderingContext");
    }

    [Fact]
    public void Parse_SnapshotWithoutBuildModel_ShouldReturnAnEmptyModelNamedAfterTheClass()
    {
        const string source = """
                              namespace App
                              {
                                  public class OrderingModelSnapshot { }
                              }
                              """;

        var (declaration, symbol, compilation) = LoadSnapshot(source, "App.OrderingModelSnapshot");

        var model = ModelSnapshotParser.Parse(declaration, symbol, compilation);

        model.ContextName.Should().Be("Ordering");
        model.Entities.Should().BeEmpty();
        model.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SnapshotWithBodilessBuildModel_ShouldReturnAnEmptyModel()
    {
        // An abstract/partial BuildModel declaration has neither a block body nor an expression body,
        // there is nothing to walk and the walkers must not be handed a null body.
        const string source = """
                              namespace App
                              {
                                  public abstract class OrderingModelSnapshot
                                  {
                                      protected abstract void BuildModel(object modelBuilder);
                                  }
                              }
                              """;

        var (declaration, symbol, compilation) = LoadSnapshot(source, "App.OrderingModelSnapshot");

        var model = ModelSnapshotParser.Parse(declaration, symbol, compilation);

        model.ContextName.Should().Be("Ordering");
        model.Entities.Should().BeEmpty();
    }
}
