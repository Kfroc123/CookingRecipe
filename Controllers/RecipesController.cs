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
        private const string DefaultFoodImage = "https://images.unsplash.com/photo-1498837167922-ddd27525d352?auto=format&fit=crop&w=1200&q=80";
        private static readonly Regex ExcludedTitlePattern = new(
            @"^(festival|festeval|village pot|sunday(?:\s+special)?|signature|smoky)\b|\b(?:rice|yam|plantain|beans|spaghetti|potato)\s+pepper\s+mix\b|\bofensala\s+catfish\b|\b(lagos|abuja|port harcourt|ibadan|enugu|kano|benin)\s+style\b|\b(chef style|market style|home style|family pot)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Dictionary<string, string[]> IngredientAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["maggi"] = ["bouillon cube", "stock cube", "seasoning cube", "bouillon"],
            ["tomato paste"] = ["tomato puree", "tomato sauce", "concentrated tomato"],
            ["stew"] = ["stewed tomato", "tomato sauce", "sauce"],
            ["beans"] = ["bean", "kidney beans", "black beans", "pinto beans", "white beans"]
        };

        private readonly ISpoonacularService _spoon;
        private readonly INigerianRecipeDatasetService _nigerianDataset;
        private readonly IRedisStore _store;
        private const string CookieName = "deviceId";

        public RecipesController(ISpoonacularService spoon, INigerianRecipeDatasetService nigerianDataset, IRedisStore store)
        {
            _spoon = spoon;
            _nigerianDataset = nigerianDataset;
            _store = store;
        }

        // Search live (Spoonacular) and save results to Redis
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string ingredients, [FromQuery] int max = 10)
        {
            if (string.IsNullOrWhiteSpace(ingredients)) return BadRequest("ingredients required");

            var parts = ParseIngredientsInput(ingredients);
            if (parts.Length == 0) return BadRequest("Provide at least one valid ingredient.");

            var sanitizedMax = Math.Clamp(max, 1, 50);
            var prioritizedNigerian = FilterDisallowedRecipeTitles(_nigerianDataset.SearchByIngredientsPriority(parts, sanitizedMax));
            var explicitNigerianIntent = _nigerianDataset.IsNigerianQuery(parts, ingredients);

            if (prioritizedNigerian.Count > 0 && (!_spoon.IsConfigured || explicitNigerianIntent || prioritizedNigerian.Count >= sanitizedMax))
            {
                EnsureRecipeImages(prioritizedNigerian);
                foreach (var recipe in prioritizedNigerian)
                {
                    await _store.SaveRecipeAsync(recipe);
                }
                await SaveSearchHistorySafeAsync(string.Join(',', parts), "nigerian-priority-search", prioritizedNigerian);
                return Ok(prioritizedNigerian);
            }

            if (!_spoon.IsConfigured)
            {
                if (prioritizedNigerian.Count > 0)
                {
                    EnsureRecipeImages(prioritizedNigerian);
                    foreach (var recipe in prioritizedNigerian)
                    {
                        await _store.SaveRecipeAsync(recipe);
                    }
                    await SaveSearchHistorySafeAsync(string.Join(',', parts), "nigerian-priority-search", prioritizedNigerian);
                    return Ok(prioritizedNigerian);
                }

                return StatusCode(StatusCodes.Status500InternalServerError, "Spoonacular API key is missing. Set Spoonacular:ApiKey in appsettings or user secrets.");
            }

            List<Recipe> list;
            try
            {
                list = await GetStrictSuggestionsAsync(parts, sanitizedMax);
                // Only fall back to title search for single-term queries.
                // Multi-ingredient queries should remain strict ingredient matches.
                if (list.Count == 0 && parts.Length == 1)
                {
                    list = await GetNameFallbackSuggestionsAsync(ingredients, sanitizedMax);
                }

                if (prioritizedNigerian.Count > 0)
                {
                    list = PrioritizeLocalRecipes(prioritizedNigerian, list, sanitizedMax);
                }

                list = FilterDisallowedRecipeTitles(list);
                EnsureRecipeImages(list);
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

            var sanitizedMax = Math.Clamp(request.Max, 1, 50);
            var localResults = FilterDisallowedRecipeTitles(_nigerianDataset.SearchByIngredientsPriority(normalized, sanitizedMax));
            var explicitNigerianIntent = _nigerianDataset.IsNigerianQuery(normalized, string.Join(' ', request.Ingredients));

            if (localResults.Count > 0 && (!_spoon.IsConfigured || explicitNigerianIntent || localResults.Count >= sanitizedMax))
            {
                EnsureRecipeImages(localResults);
                foreach (var recipe in localResults)
                {
                    await _store.SaveRecipeAsync(recipe);
                }
                await SaveSearchHistorySafeAsync(string.Join(',', normalized), "nigerian-priority-suggest", localResults);
                return Ok(localResults);
            }

            if (!_spoon.IsConfigured)
            {
                if (localResults.Count > 0)
                {
                    EnsureRecipeImages(localResults);
                    foreach (var recipe in localResults)
                    {
                        await _store.SaveRecipeAsync(recipe);
                    }
                    await SaveSearchHistorySafeAsync(string.Join(',', normalized), "nigerian-priority-suggest", localResults);
                    return Ok(localResults);
                }

                return StatusCode(StatusCodes.Status500InternalServerError, "Spoonacular API key is missing. Set Spoonacular:ApiKey in appsettings or user secrets.");
            }

            List<Recipe> enriched;
            try
            {
                enriched = await GetStrictSuggestionsAsync(normalized, sanitizedMax);
                if (localResults.Count > 0)
                {
                    enriched = PrioritizeLocalRecipes(localResults, enriched, sanitizedMax);
                }
                enriched = FilterDisallowedRecipeTitles(enriched);
                EnsureRecipeImages(enriched);
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

        private static List<Recipe> PrioritizeLocalRecipes(List<Recipe> localResults, List<Recipe> remoteResults, int max)
        {
            var merged = localResults
                .Concat(remoteResults)
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .Take(Math.Clamp(max, 1, 50))
                .ToList();

            return merged;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var recipes = FilterDisallowedRecipeTitles(_nigerianDataset.Search(Array.Empty<string>(), 50));
            EnsureRecipeImages(recipes);
            return Ok(recipes);
        }

        // Get recipe detail (from redis or spoonacular)
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var nigerian = _nigerianDataset.GetById(id);
            if (nigerian != null)
            {
                if (IsDisallowedTitle(nigerian.Title))
                {
                    return NotFound();
                }
                EnsureRecipeImage(nigerian);
                try
                {
                    await _store.SaveRecipeAsync(nigerian);
                }
                catch
                {
                    // ignore cache failures
                }
                return Ok(nigerian);
            }

            var r = await _store.GetRecipeAsync(id);
            if (r != null)
            {
                if (IsDisallowedTitle(r.Title))
                {
                    return NotFound();
                }
                return Ok(r);
            }

            if (!_spoon.IsConfigured)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Spoonacular API key is missing. Set Spoonacular:ApiKey in appsettings or user secrets.");
            }

            try
            {
                r = await _spoon.GetRecipeDetailAsync(id);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
            }

            if (r == null) return NotFound();
            if (IsDisallowedTitle(r.Title)) return NotFound();
            EnsureRecipeImage(r);

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
            var list = FilterDisallowedRecipeTitles(await _store.GetFavoritesAsync(deviceId));
            EnsureRecipeImages(list);
            return Ok(list);
        }

        [HttpGet("stored/search")]
        public async Task<IActionResult> SearchStored([FromQuery] string ingredients, [FromQuery] int max = 50)
        {
            if (string.IsNullOrWhiteSpace(ingredients)) return BadRequest("ingredients required");
            var parts = ingredients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var list = FilterDisallowedRecipeTitles(await _store.SearchStoredRecipesAsync(parts, max));
            EnsureRecipeImages(list);
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
            return Regex.Split(input, @"\s*(?:,|;|\+|&|\band\b)\s*", RegexOptions.IgnoreCase)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim());
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
            return Regex.Split(input, @"\s*(?:,|;|\+|&|\band\b)\s*", RegexOptions.IgnoreCase)
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

        private static void EnsureRecipeImage(Recipe recipe)
        {
            if (string.IsNullOrWhiteSpace(recipe.ImageUrl))
            {
                recipe.ImageUrl = DefaultFoodImage;
            }
        }

        private static void EnsureRecipeImages(IEnumerable<Recipe> recipes)
        {
            foreach (var recipe in recipes)
            {
                EnsureRecipeImage(recipe);
            }
        }

        private static bool IsDisallowedTitle(string? title)
        {
            return !string.IsNullOrWhiteSpace(title) && ExcludedTitlePattern.IsMatch(title);
        }

        private static List<Recipe> FilterDisallowedRecipeTitles(IEnumerable<Recipe> recipes)
        {
            return recipes
                .Where(r => !IsDisallowedTitle(r.Title))
                .ToList();
        }
    }
}
