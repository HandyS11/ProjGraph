using FluentAssertions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Rendering;

namespace ProjGraph.Tests.Unit.Rendering;

public class MermaidClassDiagramRendererTests
{
    [Fact]
    public void Render_EmptyModel_ReturnsEmptyDiagram()
    {
        var model = new ClassModel(null, [], []);
        var result = MermaidClassDiagramRenderer.Render(model);
        result.Should().Be("```mermaid\r\nclassDiagram\r\n```\r\n");
    }

    [Fact]
    public void Render_WithClassAndMembers_ReturnsCorrectMermaid()
    {
        var type = new TypeDefinition(
            "User",
            "Models",
            "Models.User",
            TypeKind.Class,
            []);

        type.Members.Add(new MemberDefinition("Id", "int", Visibility.Public, MemberKind.Property));
        type.Members.Add(new MemberDefinition("Username", "string", Visibility.Private, MemberKind.Field));
        type.Members.Add(new MemberDefinition("Save", "void", Visibility.Protected, MemberKind.Method, []));

        var model = new ClassModel("Test", [type], []);

        var result = MermaidClassDiagramRenderer.Render(model);

        result.Should().Contain("class Models_User [\"User\"]");
        result.Should().Contain("+int Id");
        result.Should().Contain("-string Username");
        result.Should().Contain("#Save() void");
    }

    [Fact]
    public void Render_WithInheritance_ReturnsCorrectMermaid()
    {
        var model = new ClassModel(null, [
            new TypeDefinition("Base", "Ns", "Ns.Base", TypeKind.Class, []),
            new TypeDefinition("Derived", "Ns", "Ns.Derived", TypeKind.Class, [])
        ], [new Relationship("Ns.Derived", "Ns.Base", RelationshipKind.Inheritance)]);

        var result = MermaidClassDiagramRenderer.Render(model);

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

        var result = MermaidClassDiagramRenderer.Render(model);

        result.Should().Contain("class System_Collections_Generic_List_T_ [\"List~T~\"]");
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

        var result = MermaidClassDiagramRenderer.Render(model);

        // Should use sanitized fully qualified names in the relationship
        result.Should().Contain("SimpleHierarchy_Base_BaseEntity <|-- SimpleHierarchy_Models_User");
        // Should NOT contain the short name at the beginning of the relationship (with proper word boundary)
        result.Should().NotContain("    BaseEntity <|--", "should use fully qualified name, not short name");
    }
}