using Microsoft.AspNetCore.Mvc;
using CookingRecipe.Services;
using CookingRecipe.Entities;
using CookingRecipe.Dtos;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CookingRecipe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecipesController : ControllerBase
    {
        private static readonly Dictionary<string, string[]> IngredientAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["maggi"] = ["bouillon cube", "stock cube", "seasoning cube", "bouillon"],
            ["tomato paste"] = ["tomato puree", "tomato sauce", "concentrated tomato"],
            ["stew"] = ["stewed tomato", "tomato sauce", "sauce"],
            ["beans"] = ["bean", "kidney beans", "black beans", "pinto beans", "white beans"]
        };

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

            var parts = ParseIngredientsInput(ingredients);
            if (parts.Length == 0) return BadRequest("Provide at least one valid ingredient.");

            List<Recipe> list;
            try
            {
                list = await GetStrictSuggestionsAsync(parts, max);
                if (list.Count == 0)
                {
                    list = await GetNameFallbackSuggestionsAsync(ingredients, max);
                }
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
            }
            await SaveSearchHistorySafeAsync(string.Join(',', parts), "ingredients-search", list);
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
                .SelectMany(SplitIngredientParts)
                .Select(i => i.Trim())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalized.Length == 0)
            {
                return BadRequest("Provide at least one valid ingredient.");
            }

            List<Recipe> enriched;
            try
            {
                enriched = await GetStrictSuggestionsAsync(normalized, request.Max);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
            }

            foreach (var recipe in enriched)
            {
                await _store.SaveRecipeAsync(recipe);
            }
            await SaveSearchHistorySafeAsync(string.Join(',', normalized), "suggest-by-ingredients", enriched);

            return Ok(enriched);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var recipes = await _store.SearchStoredRecipesAsync(Array.Empty<string>(), 50);
            return Ok(recipes);
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

        private async Task<List<Recipe>> GetStrictSuggestionsAsync(string[] normalizedIngredients, int max)
        {
            var sanitizedMax = Math.Clamp(max, 1, 50);
            var providerTerms = normalizedIngredients
                .Select(MapToProviderIngredient)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var ingredientsCsv = string.Join(',', providerTerms);

            var candidateCount = Math.Clamp(sanitizedMax * 15, sanitizedMax, 150);
            var candidateIds = new List<int>();
            var fallbackCandidates = new List<Recipe>();
            HttpRequestException? providerFailure = null;
            try
            {
                candidateIds = await _spoon.SearchRecipeIdsByIncludedIngredientsAsync(ingredientsCsv, candidateCount);
                fallbackCandidates = await _spoon.SearchByIngredientsAsync(ingredientsCsv, candidateCount);
            }
            catch (HttpRequestException ex)
            {
                providerFailure = ex;
                // Continue with local/cached fallback paths below.
            }
            var fallbackById = fallbackCandidates
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .ToDictionary(r => r.Id, r => r);

            // Fallback source: findByIngredients can return broader candidates for strict post-filtering.
            if (candidateIds.Count == 0)
            {
                candidateIds = fallbackById.Keys.ToList();
            }
            else
            {
                candidateIds = candidateIds
                    .Concat(fallbackById.Keys)
                    .Distinct()
                    .ToList();
            }

            var enriched = new List<Recipe>(sanitizedMax);
            foreach (var recipeId in candidateIds)
            {
                Recipe? detail = null;
                try
                {
                    detail = await _spoon.GetRecipeDetailAsync(recipeId);
                }
                catch
                {
                    // Use fallback candidate if detail call fails.
                }
                var candidate = detail;
                if (candidate == null && fallbackById.TryGetValue(recipeId, out var fallback))
                {
                    candidate = fallback;
                }
                if (candidate == null) continue;
                if (!normalizedIngredients.All(term => RecipeContainsRequestedIngredient(candidate, term))) continue;

                enriched.Add(candidate);
                if (enriched.Count >= sanitizedMax) break;
            }

            if (enriched.Count == 0 && normalizedIngredients.Length == 1)
            {
                // For single-ingredient searches, return best available fallback candidates.
                var term = normalizedIngredients[0];
                foreach (var candidate in fallbackById.Values)
                {
                    if (!RecipeContainsRequestedIngredient(candidate, term)) continue;
                    enriched.Add(candidate);
                    if (enriched.Count >= sanitizedMax) break;
                }
            }

            if (enriched.Count == 0)
            {
                var cached = await _store.SearchStoredRecipesAsync(normalizedIngredients, sanitizedMax);
                enriched.AddRange(cached);
            }

            if (enriched.Count == 0 && providerFailure != null)
            {
                throw providerFailure;
            }

            foreach (var recipe in enriched)
            {
                await _store.SaveRecipeAsync(recipe);
            }

            return enriched;
        }

        private async Task<List<Recipe>> GetNameFallbackSuggestionsAsync(string query, int max)
        {
            var sanitizedMax = Math.Clamp(max, 1, 50);
            var candidateCount = Math.Clamp(sanitizedMax * 10, sanitizedMax, 100);
            var ids = await _spoon.SearchRecipeIdsByTitleAsync(query.Trim(), candidateCount);
            if (ids.Count == 0) return new List<Recipe>();

            var list = new List<Recipe>(sanitizedMax);
            foreach (var recipeId in ids.Distinct())
            {
                var detail = await _spoon.GetRecipeDetailAsync(recipeId);
                if (detail == null) continue;
                list.Add(detail);
                if (list.Count >= sanitizedMax) break;
            }

            foreach (var recipe in list)
            {
                await _store.SaveRecipeAsync(recipe);
            }

            return list;
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

        private static bool RecipeContainsRequestedIngredient(Recipe recipe, string requestedIngredient)
        {
            var candidates = BuildSearchCandidates(requestedIngredient);
            return candidates.Any(candidate => RecipeContainsTerm(recipe, candidate));
        }

        private static IEnumerable<string> BuildSearchCandidates(string requestedIngredient)
        {
            var cleaned = requestedIngredient.Trim().ToLowerInvariant();
            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variant in BuildVariants(cleaned))
            {
                variants.Add(variant);
            }

            if (IngredientAliases.TryGetValue(cleaned, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    foreach (var variant in BuildVariants(alias.ToLowerInvariant()))
                    {
                        variants.Add(variant);
                    }
                }
            }

            return variants;
        }

        private static IEnumerable<string> SplitIngredientParts(string input)
        {
            return input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string MapToProviderIngredient(string ingredient)
        {
            var cleaned = ingredient.Trim().ToLowerInvariant();
            return cleaned switch
            {
                "maggi" => "bouillon cube",
                _ => cleaned
            };
        }

        private static string[] ParseIngredientsInput(string input)
        {
            return Regex.Split(input, @"\s*(?:,|&|\band\b)\s*", RegexOptions.IgnoreCase)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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
