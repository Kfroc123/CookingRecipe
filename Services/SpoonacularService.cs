using System.Net.Http.Json;
using System.Text.Json;
using CookingRecipe.Entities;
using System.Net;

namespace CookingRecipe.Services
{
    public interface ISpoonacularService
    {
        bool IsConfigured { get; }
        Task<List<Recipe>> SearchByIngredientsAsync(string ingredientsCsv, int number = 10);
        Task<List<int>> SearchRecipeIdsByIncludedIngredientsAsync(string ingredientsCsv, int number = 10);
        Task<List<int>> SearchRecipeIdsByTitleAsync(string query, int number = 10);
        Task<Recipe?> GetRecipeDetailAsync(int id);
    }

    public class SpoonacularService : ISpoonacularService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public SpoonacularService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["Spoonacular:ApiKey"] ?? string.Empty;
        }

        public async Task<List<Recipe>> SearchByIngredientsAsync(string ingredientsCsv, int number = 10)
        {
            var url = $"https://api.spoonacular.com/recipes/findByIngredients?ingredients={Uri.EscapeDataString(ingredientsCsv)}&number={number}&ranking=2&ignorePantry=true&apiKey={_apiKey}";
            var res = await _http.GetAsync(url);
            await EnsureSuccessAsync(res, "findByIngredients");

            using var stream = await res.Content.ReadAsStreamAsync();
            var docs = await JsonSerializer.DeserializeAsync<List<JsonElement>>(stream, _json);
            var list = new List<Recipe>();
            if (docs == null) return list;

            foreach (var el in docs)
            {
                try
                {
                    var id = el.GetProperty("id").GetInt32();
                    var title = el.GetProperty("title").GetString() ?? string.Empty;
                    var image = el.GetProperty("image").GetString() ?? string.Empty;

                    var ingredients = new List<RecipeIngredient>();
                    if (el.TryGetProperty("usedIngredients", out var used))
                    {
                        foreach (var u in used.EnumerateArray())
                        {
                            var name = u.GetProperty("name").GetString() ?? string.Empty;
                            ingredients.Add(new RecipeIngredient
                            {
                                Ingredient = new Ingredient { Name = name },
                                Quantity = string.Empty
                            });
                        }
                    }

                    if (el.TryGetProperty("missedIngredients", out var missed))
                    {
                        foreach (var u in missed.EnumerateArray())
                        {
                            var name = u.GetProperty("name").GetString() ?? string.Empty;
                            ingredients.Add(new RecipeIngredient
                            {
                                Ingredient = new Ingredient { Name = name },
                                Quantity = string.Empty
                            });
                        }
                    }

                    var recipe = new Recipe
                    {
                        Id = id,
                        Title = title,
                        ImageUrl = image,
                        Ingredients = ingredients
                    };

                    list.Add(recipe);
                }
                catch
                {
                    // ignore malformed entries
                }
            }

            return list;
        }

        public async Task<List<int>> SearchRecipeIdsByIncludedIngredientsAsync(string ingredientsCsv, int number = 10)
        {
            var url = $"https://api.spoonacular.com/recipes/complexSearch?includeIngredients={Uri.EscapeDataString(ingredientsCsv)}&number={number}&sort=popularity&sortDirection=desc&apiKey={_apiKey}";
            var res = await _http.GetAsync(url);
            await EnsureSuccessAsync(res, "complexSearch");

            using var stream = await res.Content.ReadAsStreamAsync();
            var root = await JsonSerializer.DeserializeAsync<JsonElement>(stream, _json);
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                return new List<int>();
            }

            var ids = new List<int>();
            foreach (var item in results.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out var id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        public async Task<List<int>> SearchRecipeIdsByTitleAsync(string query, int number = 10)
        {
            var url = $"https://api.spoonacular.com/recipes/complexSearch?query={Uri.EscapeDataString(query)}&number={number}&sort=popularity&sortDirection=desc&apiKey={_apiKey}";
            var res = await _http.GetAsync(url);
            await EnsureSuccessAsync(res, "complexSearch title query");

            using var stream = await res.Content.ReadAsStreamAsync();
            var root = await JsonSerializer.DeserializeAsync<JsonElement>(stream, _json);
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                return new List<int>();
            }

            var ids = new List<int>();
            foreach (var item in results.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out var id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        public async Task<Recipe?> GetRecipeDetailAsync(int id)
        {
            var url = $"https://api.spoonacular.com/recipes/{id}/information?includeNutrition=true&apiKey={_apiKey}";
            var res = await _http.GetAsync(url);
            if (res.StatusCode == HttpStatusCode.NotFound) return null;
            await EnsureSuccessAsync(res, "recipe information");

            using var stream = await res.Content.ReadAsStreamAsync();
            var el = await JsonSerializer.DeserializeAsync<JsonElement>(stream, _json);
            if (el.ValueKind == JsonValueKind.Undefined) return null;

            try
            {
                var recipe = new Recipe();
                recipe.Id = el.GetProperty("id").GetInt32();
                recipe.Title = el.GetProperty("title").GetString() ?? string.Empty;
                recipe.ImageUrl = el.GetProperty("image").GetString() ?? string.Empty;
                recipe.Summary = el.GetProperty("summary").GetString() ?? string.Empty;

                if (el.TryGetProperty("extendedIngredients", out var ext))
                {
                    var list = new List<RecipeIngredient>();
                    foreach (var item in ext.EnumerateArray())
                    {
                        var name = item.GetProperty("name").GetString() ?? string.Empty;
                        var original = item.TryGetProperty("original", out var o) ? o.GetString() ?? string.Empty : string.Empty;
                        list.Add(new RecipeIngredient
                        {
                            Ingredient = new Ingredient { Name = name },
                            Quantity = original
                        });
                    }
                    recipe.Ingredients = list;
                }

                recipe.Instructions = el.TryGetProperty("instructions", out var instr) ? instr.GetString() ?? string.Empty : string.Empty;
                recipe.ReadyInMinutes = el.TryGetProperty("readyInMinutes", out var r) ? r.GetInt32() : 0;
                recipe.Servings = el.TryGetProperty("servings", out var s) ? s.GetInt32() : 0;
                recipe.SourceUrl = el.TryGetProperty("sourceUrl", out var su) ? su.GetString() ?? string.Empty : string.Empty;

                // nutrition simplified
                if (el.TryGetProperty("nutrition", out var nut) && nut.TryGetProperty("nutrients", out var nutrients))
                {
                    var ni = new NutritionInfo();
                    foreach (var n in nutrients.EnumerateArray())
                    {
                        var name = n.GetProperty("name").GetString() ?? string.Empty;
                        var amount = n.TryGetProperty("amount", out var a) ? a.GetDouble() : 0.0;
                        if (string.Equals(name, "Calories", StringComparison.OrdinalIgnoreCase)) ni.Calories = (int)amount;
                        if (string.Equals(name, "Fat", StringComparison.OrdinalIgnoreCase)) ni.FatGrams = amount;
                        if (string.Equals(name, "Carbohydrates", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Carbs", StringComparison.OrdinalIgnoreCase)) ni.CarbsGrams = amount;
                        if (string.Equals(name, "Protein", StringComparison.OrdinalIgnoreCase)) ni.ProteinGrams = amount;
                    }
                    recipe.Nutrition = ni;
                }

                return recipe;
            }
            catch
            {
                return null;
            }
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
        {
            if (response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();
            var shortBody = string.IsNullOrWhiteSpace(body)
                ? "No details from provider."
                : body[..Math.Min(250, body.Length)];

            throw new HttpRequestException(
                $"Spoonacular {operation} failed with {(int)response.StatusCode} ({response.StatusCode}). {shortBody}",
                null,
                response.StatusCode);
        }
    }
}
