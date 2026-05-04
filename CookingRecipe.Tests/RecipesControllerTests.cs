using CookingRecipe.Controllers;
using CookingRecipe.Dtos;
using CookingRecipe.Entities;
using CookingRecipe.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CookingRecipe.Tests;

public class RecipesControllerTests
{
    [Fact]
    public async Task Search_WhenSpoonacularNotConfiguredAndNoLocalMatch_Returns500()
    {
        var controller = new RecipesController(new FakeSpoonacularService(), new NigerianRecipeDatasetService(), new InMemoryRecipeStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Search("avocado", 10);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task SuggestByIngredients_WhenBodyIsEmpty_ReturnsBadRequest()
    {
        var controller = new RecipesController(new FakeSpoonacularService(), new NigerianRecipeDatasetService(), new InMemoryRecipeStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SuggestByIngredients(new IngredientSuggestionRequestDto());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Search_NigerianMeal_WhenSpoonacularNotConfigured_ReturnsDatasetResults()
    {
        var controller = new RecipesController(new FakeSpoonacularService(), new NigerianRecipeDatasetService(), new InMemoryRecipeStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Search("jollof", 10);

        var ok = Assert.IsType<OkObjectResult>(result);
        var recipes = Assert.IsType<List<Recipe>>(ok.Value);
        Assert.NotEmpty(recipes);
        Assert.Contains(recipes, r => r.Title.Contains("Jollof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_RiceAndTomato_PrioritizesNigerianMatches()
    {
        var controller = new RecipesController(new FakeSpoonacularService(), new NigerianRecipeDatasetService(), new InMemoryRecipeStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Search("rice, tomato", 10);

        var ok = Assert.IsType<OkObjectResult>(result);
        var recipes = Assert.IsType<List<Recipe>>(ok.Value);
        Assert.NotEmpty(recipes);
        Assert.Contains(recipes, r => r.Title.Contains("Jollof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_BeansMaggiTomato_ReturnsMoiMoiCandidate()
    {
        var controller = new RecipesController(new FakeSpoonacularService(), new NigerianRecipeDatasetService(), new InMemoryRecipeStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Search("beans, maggi, tomato", 10);

        var ok = Assert.IsType<OkObjectResult>(result);
        var recipes = Assert.IsType<List<Recipe>>(ok.Value);
        Assert.NotEmpty(recipes);
        Assert.Contains(recipes, r => r.Title.Contains("Moi Moi", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeSpoonacularService : ISpoonacularService
    {
        public bool IsConfigured => false;

        public Task<Recipe?> GetRecipeDetailAsync(int id) => Task.FromResult<Recipe?>(null);

        public Task<List<Recipe>> SearchByIngredientsAsync(string ingredientsCsv, int number = 10) =>
            Task.FromResult(new List<Recipe>());

        public Task<List<int>> SearchRecipeIdsByIncludedIngredientsAsync(string ingredientsCsv, int number = 10) =>
            Task.FromResult(new List<int>());

        public Task<List<int>> SearchRecipeIdsByTitleAsync(string query, int number = 10) =>
            Task.FromResult(new List<int>());

        public Task<List<Recipe>> SearchOpenRecipesAsync(string query, IEnumerable<string> requiredTerms, int number = 10) =>
            Task.FromResult(new List<Recipe>());
    }
}
