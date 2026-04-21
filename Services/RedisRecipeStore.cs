using StackExchange.Redis;
using System.Text.Json;
using CookingRecipe.Entities;
using System.Linq;

namespace CookingRecipe.Services
{
    public interface IRedisStore
    {
        Task SaveRecipeAsync(Recipe recipe);
        Task<Recipe?> GetRecipeAsync(int id);

        Task SaveSearchHistoryAsync(string deviceId, SearchHistory history);
        Task<List<SearchHistory>> GetSearchHistoryAsync(string deviceId, int count = 50);

        // favorites per device
        Task AddFavoriteAsync(string deviceId, int recipeId);
        Task RemoveFavoriteAsync(string deviceId, int recipeId);
        Task<List<Recipe>> GetFavoritesAsync(string deviceId);

        // search stored recipes (simple scan)
        Task<List<Recipe>> SearchStoredRecipesAsync(IEnumerable<string> ingredients, int max = 50);
    }

    public class RedisRecipeStore : IRedisStore
    {
        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _mux;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public RedisRecipeStore(IConnectionMultiplexer multiplexer)
        {
            _mux = multiplexer;
            _db = multiplexer.GetDatabase();
        }

        public async Task SaveRecipeAsync(Recipe recipe)
        {
            var key = $"recipe:{recipe.Id}";
            var json = JsonSerializer.Serialize(recipe, _jsonOptions);
            await _db.StringSetAsync(key, json);
        }

        public async Task<Recipe?> GetRecipeAsync(int id)
        {
            var key = $"recipe:{id}";
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<Recipe>(value!, _jsonOptions);
        }

        public async Task SaveSearchHistoryAsync(string deviceId, SearchHistory history)
        {
            // ensure the history carries the device id and timestamp
            history.DeviceId = deviceId;
            if (history.SearchDate == default)
            {
                history.SearchDate = DateTime.UtcNow;
            }

            var key = $"history:{deviceId}";
            var json = JsonSerializer.Serialize(history, _jsonOptions);
            await _db.ListLeftPushAsync(key, json);
            // keep only the most recent 100 entries
            await _db.ListTrimAsync(key, 0, 99);
        }

        public async Task<List<SearchHistory>> GetSearchHistoryAsync(string deviceId, int count = 50)
        {
            var key = $"history:{deviceId}";
            var values = await _db.ListRangeAsync(key, 0, count - 1);
            var result = new List<SearchHistory>(values.Length);
            foreach (var v in values)
            {
                if (v.IsNullOrEmpty) continue;
                var item = JsonSerializer.Deserialize<SearchHistory>(v!, _jsonOptions);
                if (item != null) result.Add(item);
            }
            return result;
        }

        // Favorites stored as Redis set of recipe ids
        public async Task AddFavoriteAsync(string deviceId, int recipeId)
        {
            var key = $"favorites:{deviceId}";
            await _db.SetAddAsync(key, recipeId.ToString());
        }

        public async Task RemoveFavoriteAsync(string deviceId, int recipeId)
        {
            var key = $"favorites:{deviceId}";
            await _db.SetRemoveAsync(key, recipeId.ToString());
        }

        public async Task<List<Recipe>> GetFavoritesAsync(string deviceId)
        {
            var key = $"favorites:{deviceId}";
            var members = await _db.SetMembersAsync(key);
            var result = new List<Recipe>(members.Length);
            foreach (var m in members)
            {
                if (m.IsNullOrEmpty) continue;
                if (int.TryParse(m, out var id))
                {
                    var r = await GetRecipeAsync(id);
                    if (r != null) result.Add(r);
                }
            }
            return result;
        }

        // naive scan of stored recipe keys and filter by ingredient names
        public async Task<List<Recipe>> SearchStoredRecipesAsync(IEnumerable<string> ingredients, int max = 50)
        {
            var normalized = ingredients.Where(i => !string.IsNullOrWhiteSpace(i))
                                        .Select(i => i.Trim().ToLowerInvariant())
                                        .ToArray();

            var server = _mux.GetServer(_mux.GetEndPoints().First());
            var keys = server.Keys(pattern: "recipe:*").Take(1000);
            var result = new List<Recipe>();
            foreach (var key in keys)
            {
                var value = await _db.StringGetAsync(key);
                if (value.IsNullOrEmpty) continue;
                var recipe = JsonSerializer.Deserialize<Recipe>(value!, _jsonOptions);
                if (recipe == null) continue;

                // build a searchable string of ingredient names
                var names = recipe.Ingredients?.Select(ri => ri.Ingredient?.Name ?? ri.Quantity ?? string.Empty)
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .Select(s => s.ToLowerInvariant())
                                  .ToArray() ?? Array.Empty<string>();

                var matchesAll = normalized.All(n => names.Any(name => name.Contains(n)));
                if (matchesAll)
                {
                    result.Add(recipe);
                    if (result.Count >= max) break;
                }
            }

            return result;
        }
    }
}
