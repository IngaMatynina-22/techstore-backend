using TechStore.Application.Features.Categories;

namespace TechStore.IntegrationTests.Catalog;

public class CategoryServiceTests
{
    [Fact]
    public async Task CreateAndUpdate_PersistsCategory()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var service = CatalogTestHelper.CreateCategoryService(db);

        var create = await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "Phones",
            Slug = "phones",
            SortOrder = 1
        });

        Assert.True(create.IsSuccess);
        var details = await service.GetByIdAsync(create.Value!);
        Assert.NotNull(details);
        Assert.Equal("phones", details!.Slug);

        var update = await service.UpdateAsync(create.Value!, new UpdateCategoryRequest
        {
            Name = "Smartphones",
            Slug = "smartphones",
            SortOrder = 2,
            IsActive = true
        });

        Assert.True(update.IsSuccess);
        details = await service.GetByIdAsync(create.Value!);
        Assert.Equal("Smartphones", details!.Name);
        Assert.Equal("smartphones", details.Slug);
    }

    [Fact]
    public async Task Create_DuplicateSlug_Fails()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var service = CatalogTestHelper.CreateCategoryService(db);

        var first = await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "One",
            Slug = "same-slug"
        });
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "Two",
            Slug = "same-slug"
        });

        Assert.True(second.IsFailure);
        Assert.Contains("slug", second.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_CircularParent_Fails()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var service = CatalogTestHelper.CreateCategoryService(db);

        var root = await service.CreateAsync(new CreateCategoryRequest { Name = "Root", Slug = "root" });
        var child = await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "Child",
            Slug = "child",
            ParentId = root.Value
        });

        var result = await service.UpdateAsync(root.Value!, new UpdateCategoryRequest
        {
            Name = "Root",
            Slug = "root",
            ParentId = child.Value,
            IsActive = true
        });

        Assert.True(result.IsFailure);
        Assert.Contains("circular", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivate_SetsInactive()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var service = CatalogTestHelper.CreateCategoryService(db);

        var create = await service.CreateAsync(new CreateCategoryRequest { Name = "Old", Slug = "old" });
        var result = await service.DeactivateAsync(create.Value!);

        Assert.True(result.IsSuccess);
        var details = await service.GetByIdAsync(create.Value!);
        Assert.False(details!.IsActive);
    }
}
