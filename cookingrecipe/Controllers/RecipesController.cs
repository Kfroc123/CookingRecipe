using Microsoft.AspNetCore.Mvc;
using CookingRecipe.Services;
using CookingRecipe.Entities;
using CookingRecipe.Dtos;
using System.Text.Json;

namespace CookingRecipe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecipesController : ControllerBase
    {
        private readonly ISpoonacularService _spoon;
        private readonly IRedisStore _store;
        private const string CookieName = "deviceId";

        public RecipesController(ISpoonacularService spoon, IRedisStore store)
        {
            _spoon = spoon;
            _store = store;
        }

        // Search live (Spoonacular) and save results to Redis
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string ingredients, [FromQuery] int max = 10)
        {
            if (string.IsNullOrWhiteSpace(ingredients)) return BadRequest("ingredients required");
            if (!_spoon.IsConfigured)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Spoonacular API key is missing. Set Spoonacular:ApiKey in appsettings or user secrets.");
            }

            var sanitizedMax = Math.Clamp(max, 1, 50);
            var list = await _spoon.SearchByIngredientsAsync(ingredients, sanitizedMax);

            // save results to redis for faster subsequent access
            foreach (var r in list)
            {
                await _store.SaveRecipeAsync(r);
            }
            await SaveSearchHistorySafeAsync(ingredients, "ingredients-search", list);

            return Ok(list);
        }

        // Search by a JSON body so users can submit a list of ingredients directly
        [HttpPost("suggest-by-ingredients")]
        public async Task<IActionResult> SuggestByIngredients([FromBody] IngredientSuggestionRequestDto request)
        {
            if (request is null || request.Ingredients.Count == 0)
            {
                return BadRequest("Provide at least one ingredient.");
            }
            if (!_spoon.IsConfigured)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Spoonacular API key is missing. Set Spoonacular:ApiKey in appsettings or user secrets.");
            }

            var normalized = request.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalized.Length == 0)
            {
                return BadRequest("Provide at least one valid ingredient.");
            }

            var ingredientsCsv = string.Join(',', normalized);
            var sanitizedMax = Math.Clamp(request.Max, 1, 50);

            var candidateCount = Math.Clamp(sanitizedMax * 10, sanitizedMax, 100);
            var candidateIds = await _spoon.SearchRecipeIdsByIncludedIngredientsAsync(ingredientsCsv, candidateCount);

            // enrich candidates with full details (includes instructions) and keep strict matches only
            var enriched = new List<Recipe>(sanitizedMax);
            foreach (var recipeId in candidateIds)
            {
                var detail = await _spoon.GetRecipeDetailAsync(recipeId);
                if (detail == null) continue;
                if (!normalized.All(term => RecipeContainsTerm(detail, term))) continue;

                enriched.Add(detail);
                if (enriched.Count >= sanitizedMax) break;
            }

            foreach (var recipe in enriched)
            {
                await _store.SaveRecipeAsync(recipe);
            }
            await SaveSearchHistorySafeAsync(ingredientsCsv, "suggest-by-ingredients", enriched);

            return Ok(enriched);
        }

        // Get recipe detail (from redis or spoonacular)
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var r = await _store.GetRecipeAsync(id);
            if (r != null) return Ok(r);
            if (!_spoon.IsConfigured)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Spoonacular API key is missing. Set Spoonacular:ApiKey in appsettings or user secrets.");
            }

            r = await _spoon.GetRecipeDetailAsync(id);

            if (r == null) return NotFound();

            await _store.SaveRecipeAsync(r);
            return Ok(r);
        }

        [HttpPost("{id:int}/favorite")]
        public async Task<IActionResult> AddFavorite(int id)
        {
            var deviceId = GetOrCreateDeviceId();
            await _store.AddFavoriteAsync(deviceId, id);
            return Ok();
        }

        [HttpDelete("{id:int}/favorite")]
        public async Task<IActionResult> RemoveFavorite(int id)
        {
            var deviceId = GetOrCreateDeviceId();
            await _store.RemoveFavoriteAsync(deviceId, id);
            return Ok();
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var deviceId = GetOrCreateDeviceId();
            var list = await _store.GetFavoritesAsync(deviceId);
            return Ok(list);
        }

        [HttpGet("stored/search")]
        public async Task<IActionResult> SearchStored([FromQuery] string ingredients, [FromQuery] int max = 50)
        {
            if (string.IsNullOrWhiteSpace(ingredients)) return BadRequest("ingredients required");
            var parts = ingredients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var list = await _store.SearchStoredRecipesAsync(parts, max);
            return Ok(list);
        }

        private string GetOrCreateDeviceId()
        {
            var deviceId = Request.Cookies[CookieName];
            if (!string.IsNullOrWhiteSpace(deviceId)) return deviceId;

            if (HttpContext.Items.TryGetValue(CookieName, out var v) && v is string s && !string.IsNullOrWhiteSpace(s)) return s;

            deviceId = Guid.NewGuid().ToString();
            Response.Cookies.Append(CookieName, deviceId, new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });

            return deviceId;
        }

        private async Task SaveSearchHistorySafeAsync(string searchText, string category, List<Recipe> results)
        {
            try
            {
                var deviceId = GetOrCreateDeviceId();
                var history = new SearchHistory
                {
                    DeviceId = deviceId,
                    SearchText = searchText,
                    Category = category,
                    SearchDate = DateTime.UtcNow,
                    JsonResult = JsonSerializer.Serialize(results)
                };

                await _store.SaveSearchHistoryAsync(deviceId, history);
            }
            catch
            {
                // do not break core recipe search if history write fails
            }
        }

        private static bool RecipeContainsTerm(Recipe recipe, string term)
        {
            var normalizedTerm = term.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedTerm)) return false;

            var variants = BuildVariants(normalizedTerm);
            var values = new List<string>();

            if (!string.IsNullOrWhiteSpace(recipe.Title))
            {
                values.Add(recipe.Title.ToLowerInvariant());
            }

            if (recipe.Ingredients != null)
            {
                values.AddRange(recipe.Ingredients
                    .Select(i => i.Ingredient?.Name ?? i.Quantity ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.ToLowerInvariant()));
            }

            return variants.Any(v => values.Any(val => val.Contains(v)));
        }

        private static IEnumerable<string> BuildVariants(string value)
        {
            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { value };

            if (value.EndsWith("es", StringComparison.OrdinalIgnoreCase) && value.Length > 2)
            {
                variants.Add(value[..^2]);
            }

            if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) && value.Length > 1)
            {
                variants.Add(value[..^1]);
            }

            return variants;
        }
    }
}
