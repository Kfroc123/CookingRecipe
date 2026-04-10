using CookingRecipe.Entities;

namespace CookingRecipe.Services
{
    public class InMemoryRecipeStore : IRedisStore
    {
        private readonly Dictionary<int, Recipe> _recipes = new();
        private readonly Dictionary<string, List<SearchHistory>> _historyByDevice = new();
        private readonly Dictionary<string, HashSet<int>> _favoritesByDevice = new();
        private readonly object _lock = new();

        public Task SaveRecipeAsync(Recipe recipe)
        {
            lock (_lock)
            {
                _recipes[recipe.Id] = recipe;
            }
            return Task.CompletedTask;
        }

        public Task<Recipe?> GetRecipeAsync(int id)
        {
            lock (_lock)
            {
                _recipes.TryGetValue(id, out var recipe);
                return Task.FromResult(recipe);
            }
        }

        public Task SaveSearchHistoryAsync(string deviceId, SearchHistory history)
        {
            lock (_lock)
            {
                if (!_historyByDevice.TryGetValue(deviceId, out var list))
                {
                    list = new List<SearchHistory>();
                    _historyByDevice[deviceId] = list;
                }

                list.Insert(0, history);
                if (list.Count > 100)
                {
                    list.RemoveRange(100, list.Count - 100);
                }
            }

            return Task.CompletedTask;
        }

        public Task<List<SearchHistory>> GetSearchHistoryAsync(string deviceId, int count = 50)
        {
            lock (_lock)
            {
                if (!_historyByDevice.TryGetValue(deviceId, out var list))
                {
                    return Task.FromResult(new List<SearchHistory>());
                }

                return Task.FromResult(list.Take(Math.Max(0, count)).ToList());
            }
        }

        public Task AddFavoriteAsync(string deviceId, int recipeId)
        {
            lock (_lock)
            {
                if (!_favoritesByDevice.TryGetValue(deviceId, out var set))
                {
                    set = new HashSet<int>();
                    _favoritesByDevice[deviceId] = set;
                }

                set.Add(recipeId);
            }

            return Task.CompletedTask;
        }

        public Task RemoveFavoriteAsync(string deviceId, int recipeId)
        {
            lock (_lock)
            {
                if (_favoritesByDevice.TryGetValue(deviceId, out var set))
                {
                    set.Remove(recipeId);
                }
            }

            return Task.CompletedTask;
        }

        public Task<List<Recipe>> GetFavoritesAsync(string deviceId)
        {
            lock (_lock)
            {
                if (!_favoritesByDevice.TryGetValue(deviceId, out var set))
                {
                    return Task.FromResult(new List<Recipe>());
                }

                var list = set
                    .Where(id => _recipes.ContainsKey(id))
                    .Select(id => _recipes[id])
                    .ToList();

                return Task.FromResult(list);
            }
        }

        public Task<List<Recipe>> SearchStoredRecipesAsync(IEnumerable<string> ingredients, int max = 50)
        {
            var normalized = ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim().ToLowerInvariant())
                .ToArray();

            lock (_lock)
            {
                var result = _recipes.Values
                    .Where(recipe =>
                    {
                        var names = recipe.Ingredients?
                            .Select(ri => ri.Ingredient?.Name ?? ri.Quantity ?? string.Empty)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.ToLowerInvariant())
                            .ToArray() ?? Array.Empty<string>();

                        return normalized.All(n => names.Any(name => name.Contains(n)));
                    })
                    .Take(Math.Max(1, max))
                    .ToList();

                return Task.FromResult(result);
            }
        }
    }
}
