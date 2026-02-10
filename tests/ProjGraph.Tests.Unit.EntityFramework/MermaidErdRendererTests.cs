using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Rendering;

namespace ProjGraph.Tests.Unit.EntityFramework;

[Trait("Category", "EntityFramework")]
public class MermaidErdRendererTests
{
    private readonly MermaidErdRenderer _renderer = new();

    [Fact]
    public void Render_ShouldGenerateValidMermaidErDiagram()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "User",
            Properties =
            [
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "Name", Type = "string", IsRequired = true }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("```mermaid");
        result.Should().Contain("erDiagram");
        result.Should().Contain("User {");
        result.Should().Contain("int Id PK");
        result.Should().Contain("string Name");
        result.Should().Contain("```");
    }

    [Fact]
    public void Render_ShouldHandleEmptyModel()
    {
        // Arrange
        var model = new EfModel { ContextName = "TestDbContext", Entities = [] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("```mermaid");
        result.Should().Contain("erDiagram");
        result.Should().Contain("```");
    }

    [Fact]
    public void Render_ShouldShowTitle()
    {
        // Arrange
        var model = new EfModel { ContextName = "MyContext", Entities = [] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("---");
        result.Should().Contain("title: MyContext");
    }

    [Fact]
    public void Render_ShouldNotShowTitle_WhenShowTitleIsFalse()
    {
        // Arrange
        var model = new EfModel { ContextName = "MyContext", Entities = [] };

        // Act
        var result = _renderer.Render(model, new DiagramOptions(false));

        // Assert
        result.Should().NotContain("---");
        result.Should().NotContain("title: MyContext");
    }

    [Fact]
    public void Render_ShouldMarkPrimaryKeys()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "Product",
            Properties =
            [
                new EfProperty { Name = "ProductId", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "Name", Type = "string" }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("ProductId PK");
    }

    [Fact]
    public void Render_ShouldMarkForeignKeys()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "Order",
            Properties =
            [
                new EfProperty { Name = "OrderId", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "CustomerId", Type = "int", IsForeignKey = true }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("CustomerId FK");
    }

    [Fact]
    public void Render_ShouldMarkCompositeKeys()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "OrderItem",
            Properties =
            [
                new EfProperty { Name = "OrderId", Type = "int", IsPrimaryKey = true, IsForeignKey = true },
                new EfProperty { Name = "ProductId", Type = "int", IsPrimaryKey = true, IsForeignKey = true }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("OrderId PK,FK");
        result.Should().Contain("ProductId PK,FK");
    }

    [Fact]
    public void Render_ShouldIncludeRequiredConstraint()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "Customer",
            Properties =
            [
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "Email", Type = "string", IsRequired = true, IsExplicitlyRequired = true }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("Email").And.Contain("required");
    }

    [Fact]
    public void Render_ShouldIncludeMaxLengthConstraint()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "User",
            Properties =
            [
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "Username", Type = "string", MaxLength = 50 }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("Username").And.Contain("max:50");
    }

    [Fact]
    public void Render_ShouldIncludePrecisionConstraint()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "Product",
            Properties =
            [
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "Price", Type = "decimal", Precision = 18, Scale = 2 }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("Price").And.Contain("precision(18,2)");
    }

    [Fact]
    public void Render_ShouldIncludeDefaultValueConstraint()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "Setting",
            Properties =
            [
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "IsActive", Type = "bool", DefaultValue = "true" }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("IsActive").And.Contain("default:true");
    }

    [Fact]
    public void Render_ShouldSanitizeNullableTypes()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "User",
            Properties =
            [
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "Age", Type = "int?" }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("int Age");
        result.Should().NotContain("int? Age");
    }

    [Fact]
    public void Render_ShouldSanitizeGenericTypes()
    {
        // Arrange
        var entity = new EfEntity
        {
            Name = "Container",
            Properties =
            [
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "Items", Type = "List<string>" }
            ]
        };

        var model = new EfModel { ContextName = "TestDbContext", Entities = [entity] };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("List~string~ Items");
    }

    [Fact]
    public void Render_ShouldRenderOneToOneRelationship()
    {
        // Arrange
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity { Name = "User", Properties = [] },
                new EfEntity { Name = "Profile", Properties = [] }
            ],
            Relationships =
            [
                new EfRelationship
                {
                    SourceEntity = "User",
                    TargetEntity = "Profile",
                    Type = EfRelationshipType.OneToOne,
                    IsRequired = true
                }
            ]
        };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("User ||--|| Profile : \"\"");
    }

    [Fact]
    public void Render_ShouldRenderOneToManyRelationship()
    {
        // Arrange
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity { Name = "Customer", Properties = [] },
                new EfEntity { Name = "Order", Properties = [] }
            ],
            Relationships =
            [
                new EfRelationship
                {
                    SourceEntity = "Customer",
                    TargetEntity = "Order",
                    Type = EfRelationshipType.OneToMany,
                    IsRequired = true
                }
            ]
        };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("Customer ||--o{ Order : \"\"");
    }

    [Fact]
    public void Render_ShouldRenderOptionalOneToManyRelationship()
    {
        // Arrange
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity { Name = "Category", Properties = [] },
                new EfEntity { Name = "Product", Properties = [] }
            ],
            Relationships =
            [
                new EfRelationship
                {
                    SourceEntity = "Category",
                    TargetEntity = "Product",
                    Type = EfRelationshipType.OneToMany,
                    IsRequired = false
                }
            ]
        };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("Category |o--o{ Product : \"\"");
    }

    [Fact]
    public void Render_ShouldRenderManyToManyRelationship()
    {
        // Arrange
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity { Name = "Student", Properties = [] },
                new EfEntity { Name = "Course", Properties = [] }
            ],
            Relationships =
            [
                new EfRelationship
                {
                    SourceEntity = "Student", TargetEntity = "Course", Type = EfRelationshipType.ManyToMany
                }
            ]
        };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("Student }|--|{ Course : \"\"");
    }

    [Fact]
    public void Render_ShouldHandleMultipleEntitiesAndRelationships()
    {
        // Arrange
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity
                {
                    Name = "User",
                    Properties =
                        [new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true }]
                },

                new EfEntity
                {
                    Name = "Post",
                    Properties =
                        [new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true }]
                },

                new EfEntity
                {
                    Name = "Comment",
                    Properties =
                        [new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true }]
                }
            ],
            Relationships =
            [
                new EfRelationship
                {
                    SourceEntity = "User",
                    TargetEntity = "Post",
                    Type = EfRelationshipType.OneToMany,
                    IsRequired = true
                },

                new EfRelationship
                {
                    SourceEntity = "Post",
                    TargetEntity = "Comment",
                    Type = EfRelationshipType.OneToMany,
                    IsRequired = true
                }
            ]
        };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("User {");
        result.Should().Contain("Post {");
        result.Should().Contain("Comment {");
        result.Should().Contain("User ||--o{ Post : \"\"");
        result.Should().Contain("Post ||--o{ Comment : \"\"");
    }
}