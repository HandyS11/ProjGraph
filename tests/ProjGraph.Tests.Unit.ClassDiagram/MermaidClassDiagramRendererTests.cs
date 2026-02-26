using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Rendering;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Tests.Unit.ClassDiagram;

[Trait("Category", "ClassDiagram")]
public class MermaidClassDiagramRendererTests
{
    private readonly MermaidClassDiagramRenderer _renderer = new();

    [Fact]
    public void Render_EmptyModel_ReturnsEmptyDiagram()
    {
        var model = new ClassModel(null, [], []);
        var result = _renderer.Render(model);
        var expected = $"```mermaid{Environment.NewLine}classDiagram{Environment.NewLine}```{Environment.NewLine}";
        result.Should().Be(expected);
    }

    [Fact]
    public void Render_WithClassAndMembers_ReturnsCorrectMermaid()
    {
        var type = new TypeDefinition(
            "User",
            "Models",
            "Models.User",
            TypeKind.Class,
            [
                new MemberDefinition("Id", "int", Visibility.Public, MemberKind.Property),
                new MemberDefinition("Username", "string", Visibility.Private, MemberKind.Field),
                new MemberDefinition("Save", "void", Visibility.Protected, MemberKind.Method, []),
                new MemberDefinition("Tags", "List<string>", Visibility.Public, MemberKind.Property),
                new MemberDefinition("Metadata", "Dictionary<string, string>", Visibility.Public,
                    MemberKind.Property)
            ]);

        var model = new ClassModel("Test", [type], []);

        var result = _renderer.Render(model);

        result.Should().Contain("class Models_User [\"User\"]");
        result.Should().Contain("+int Id");
        result.Should().Contain("-string Username");
        result.Should().Contain("#Save() void");
        result.Should().Contain("+List~string~ Tags");
        result.Should().Contain("+Dictionary~string, string~ Metadata");
    }

    [Fact]
    public void Render_WithShowTitle_ShouldShowTitle()
    {
        var model = new ClassModel("MyTitle", [], []);
        var result = _renderer.Render(model);

        result.Should().Contain("---");
        result.Should().Contain("title: MyTitle");
    }

    [Fact]
    public void Render_WithShowTitleFalse_ShouldNotShowTitle()
    {
        var model = new ClassModel("MyTitle", [], []);
        var result = _renderer.Render(model, new DiagramOptions(false));

        result.Should().NotContain("---");
        result.Should().NotContain("title: MyTitle");
    }

    [Fact]
    public void Render_WithInheritance_ReturnsCorrectMermaid()
    {
        var model = new ClassModel(null, [
            new TypeDefinition("Base", "Ns", "Ns.Base", TypeKind.Class, []),
            new TypeDefinition("Derived", "Ns", "Ns.Derived", TypeKind.Class, [])
        ], [new Relationship("Ns.Derived", "Ns.Base", RelationshipKind.Inheritance)]);

        var result = _renderer.Render(model);

        result.Should().Contain("Ns_Base <|-- Ns_Derived");
    }

    [Fact]
    public void Render_WithGenerics_SanitizesCorrectly()
    {
        var type = new TypeDefinition(
            "List<T>",
            "System.Collections.Generic",
            "System.Collections.Generic.List<T>",
            TypeKind.Class,
            []);
        var model = new ClassModel(null, [type], []);

        var result = _renderer.Render(model);

        result.Should().Contain("class System_Collections_Generic_List_T_ [\"List~T~\"]");
    }

    [Fact]
    public void Render_WithMultipleGenerics_SanitizesCorrectly()
    {
        var type = new TypeDefinition(
            "Dictionary<TKey, TValue>",
            "System.Collections.Generic",
            "System.Collections.Generic.Dictionary<TKey, TValue>",
            TypeKind.Class,
            []);
        var model = new ClassModel(null, [type], []);

        var result = _renderer.Render(model);

        result.Should()
            .Contain("class System_Collections_Generic_Dictionary_TKey__TValue_ [\"Dictionary~TKey, TValue~\"]");
    }

    [Fact]
    public void Render_WithCrossNamespaceInheritance_UsesSanitizedFullyQualifiedNames()
    {
        var model = new ClassModel(null, [
                new TypeDefinition("BaseEntity", "SimpleHierarchy.Base", "SimpleHierarchy.Base.BaseEntity",
                    TypeKind.Class,
                    []),
                new TypeDefinition("User", "SimpleHierarchy.Models", "SimpleHierarchy.Models.User", TypeKind.Class, [])
            ],
            [
                new Relationship("SimpleHierarchy.Models.User", "SimpleHierarchy.Base.BaseEntity",
                    RelationshipKind.Inheritance)
            ]);

        var result = _renderer.Render(model);

        // Should use sanitized fully qualified names in the relationship
        result.Should().Contain("SimpleHierarchy_Base_BaseEntity <|-- SimpleHierarchy_Models_User");
        // Should NOT contain the short name at the beginning of the relationship (with proper word boundary)
        result.Should().NotContain("    BaseEntity <|--", "should use fully qualified name, not short name");
    }

    [Fact]
    public void Render_WithRelationshipLabels_RendersLabelsCorrectly()
    {
        var model = new ClassModel(null, [
                new TypeDefinition("User", "Models", "Models.User", TypeKind.Class, []),
                new TypeDefinition("Address", "Models", "Models.Address", TypeKind.Class, [])
            ],
            [
                new Relationship("Models.User", "Models.Address", RelationshipKind.Association, "PrimaryAddress", "1"),
                new Relationship("Models.User", "Models.Address", RelationshipKind.Association, "ShippingAddresses",
                    "*")
            ]);

        var result = _renderer.Render(model);

        result.Should().Contain("Models_User \"1\" --> Models_Address : PrimaryAddress");
        result.Should().Contain("Models_User \"*\" --> Models_Address : ShippingAddresses");
    }

    [Fact]
    public void Render_WithAbstractClass_RendersAbstractStereotype()
    {
        var abstractType = new TypeDefinition(
            "BaseEntity",
            "Models",
            "Models.BaseEntity",
            TypeKind.Class,
            [],
            true);

        var concreteType = new TypeDefinition(
            "User",
            "Models",
            "Models.User",
            TypeKind.Class,
            []);

        var model = new ClassModel(null, [abstractType, concreteType],
            [new Relationship("Models.User", "Models.BaseEntity", RelationshipKind.Inheritance)]);

        var result = _renderer.Render(model);

        result.Should().Contain("<<abstract>> Models_BaseEntity");
        result.Should().NotContain("<<abstract>> Models_User");
    }

    [Fact]
    public void Render_WithEnum_NoTypeInMembersAndNoSelfReferences()
    {
        var enumType = new TypeDefinition(
            "Types",
            "SimpleHierarchy.Enums",
            "SimpleHierarchy.Enums.Types",
            TypeKind.Enum,
            [
                new MemberDefinition("None", string.Empty, Visibility.Public, MemberKind.Field),
                new MemberDefinition("TypeA", string.Empty, Visibility.Public, MemberKind.Field),
                new MemberDefinition("TypeB", string.Empty, Visibility.Public, MemberKind.Field),
                new MemberDefinition("TypeC", string.Empty, Visibility.Public, MemberKind.Field)
            ]);

        var model = new ClassModel("Types.cs", [enumType], []);

        var result = _renderer.Render(model);

        // Should contain enum stereotype
        result.Should().Contain("<<enum>> SimpleHierarchy_Enums_Types");

        // Enum members should only show names without types
        result.Should().Contain("+None");
        result.Should().Contain("+TypeA");
        result.Should().Contain("+TypeB");
        result.Should().Contain("+TypeC");

        // Should NOT contain the full type name in members
        result.Should().NotContain("SimpleHierarchy.Enums.Types None");
        result.Should().NotContain("SimpleHierarchy.Enums.Types TypeA");

        // Should not contain self-referential relationships
        result.Should().NotContain("SimpleHierarchy_Enums_Types --> SimpleHierarchy_Enums_Types");
    }

    [Fact]
    public void Render_WithInterface_OnlyShowsInterfaceStereotype()
    {
        var interfaceType = new TypeDefinition(
            "IRepository<T>",
            "SimpleHierarchy.Interfaces",
            "SimpleHierarchy.Interfaces.IRepository<T>",
            TypeKind.Interface,
            [
                new MemberDefinition("GetById", "T?", Visibility.Public, MemberKind.Method,
                    [new ParameterDefinition("id", "Guid")]),
                new MemberDefinition("GetAll", "IEnumerable<T>", Visibility.Public, MemberKind.Method,
                    []),
                new MemberDefinition("Save", "void", Visibility.Public, MemberKind.Method,
                    [new ParameterDefinition("entity", "T")])
            ],
            true); // Interfaces are marked as abstract by Roslyn

        var model = new ClassModel("IRepository.cs", [interfaceType], []);

        var result = _renderer.Render(model);

        // Should contain interface stereotype
        result.Should().Contain("<<interface>> SimpleHierarchy_Interfaces_IRepository_T_");

        // Should NOT contain abstract stereotype (interfaces are inherently abstract)
        result.Should().NotContain("<<abstract>> SimpleHierarchy_Interfaces_IRepository_T_");

        // Should contain the methods
        result.Should().Contain("+GetById(Guid id) T?");
        result.Should().Contain("+GetAll() IEnumerable~T~");
        result.Should().Contain("+Save(T entity) void");
    }

    [Fact]
    public void Render_WithColonsInFullName_SanitizesCorrectly()
    {
        // Simulates the case where global:: remains inside a generic type argument
        var type = new TypeDefinition(
            "AbstractValidator<SomeInput>",
            "FluentValidation",
            "FluentValidation.AbstractValidator<global::App.SomeInput>",
            TypeKind.Class,
            []);
        var model = new ClassModel(null, [type], []);

        var result = _renderer.Render(model);

        // Colons must be sanitized to underscores so Mermaid doesn't interpret :: as a style separator
        result.Should().NotContain("::");
        result.Should().Contain("FluentValidation_AbstractValidator_global__App_SomeInput_");
    }

    [Fact]
    public void Render_WithRecord_ShowsRecordStereotype()
    {
        var recordType = new TypeDefinition(
            "Localisation",
            "SimpleHierarchy.Models",
            "SimpleHierarchy.Models.Localisation",
            TypeKind.Record,
            [
                new MemberDefinition("X", "int", Visibility.Public, MemberKind.Property),
                new MemberDefinition("Y", "int", Visibility.Public, MemberKind.Property),
                new MemberDefinition("Z", "int", Visibility.Public, MemberKind.Property)
            ]);

        var model = new ClassModel("Localisation.cs", [recordType], []);

        var result = _renderer.Render(model);

        // Should contain record stereotype
        result.Should().Contain("<<record>> SimpleHierarchy_Models_Localisation");

        // Should contain the properties
        result.Should().Contain("+int X");
        result.Should().Contain("+int Y");
        result.Should().Contain("+int Z");

        // Should not contain self-referential relationships
        result.Should().NotContain("SimpleHierarchy_Models_Localisation ..> SimpleHierarchy_Models_Localisation");
    }
}
