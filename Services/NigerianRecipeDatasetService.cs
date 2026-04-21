using CookingRecipe.Entities;

namespace CookingRecipe.Services
{
    public interface INigerianRecipeDatasetService
    {
        bool IsNigerianQuery(IEnumerable<string> terms, string rawQuery);
        List<Recipe> Search(IEnumerable<string> terms, int max = 10);
        Recipe? GetById(int id);
    }

    public class NigerianRecipeDatasetService : INigerianRecipeDatasetService
    {
        private static readonly string[] NigerianKeywords =
        [
            "jollof", "ofada", "egusi", "efo", "edikang", "afang", "okra", "okra soup",
            "ogbono", "banga", "fufu", "amala", "semo", "eba", "garri", "pounded yam",
            "moi moi", "moimoi", "akara", "suya", "kilishi", "asun", "pepper soup",
            "nkwobi", "isi ewu", "yam porridge", "beans porridge", "ewa agoyin",
            "nigerian", "naija", "gbegiri", "tuwo", "shinkafa", "masa"
        ];

        private readonly List<Recipe> _recipes =
        [
            CreateRecipe(
                900001,
                "Nigerian Jollof Rice",
                "Classic smoky party jollof rice.",
                ["rice", "tomato", "pepper", "onion", "thyme", "curry", "stock cube"]),
            CreateRecipe(
                900002,
                "Egusi Soup",
                "Rich melon seed soup served with swallow.",
                ["egusi", "spinach", "palm oil", "pepper", "onion", "beef", "stock fish"]),
            CreateRecipe(
                900003,
                "Efo Riro",
                "Yoruba spinach stew cooked with peppers and assorted meats.",
                ["spinach", "bell pepper", "scotch bonnet", "palm oil", "onion", "beef"]),
            CreateRecipe(
                900004,
                "Ofada Rice and Ayamase",
                "Local ofada rice with green pepper stew.",
                ["ofada rice", "green pepper", "onion", "locust beans", "palm oil", "beef"]),
            CreateRecipe(
                900005,
                "Beans and Dodo",
                "Stewed beans served with fried plantain.",
                ["beans", "plantain", "onion", "pepper", "palm oil", "stock cube"]),
            CreateRecipe(
                900006,
                "Yam Porridge (Asaro)",
                "Soft yam cooked in rich pepper sauce.",
                ["yam", "tomato", "pepper", "onion", "palm oil", "crayfish"]),
            CreateRecipe(
                900007,
                "Moi Moi",
                "Steamed bean pudding with peppers and onion.",
                ["beans", "pepper", "onion", "vegetable oil", "egg", "fish"]),
            CreateRecipe(
                900008,
                "Akara",
                "Deep-fried bean cakes for breakfast or snack.",
                ["beans", "onion", "pepper", "salt", "vegetable oil"]),
            CreateRecipe(
                900009,
                "Suya",
                "Spicy grilled beef skewers.",
                ["beef", "groundnut", "ginger", "garlic", "pepper", "onion"]),
            CreateRecipe(
                900010,
                "Pepper Soup",
                "Light spicy broth with fish or meat.",
                ["catfish", "pepper soup spice", "pepper", "onion", "uziza", "ginger"])
        ];

        public bool IsNigerianQuery(IEnumerable<string> terms, string rawQuery)
        {
            var combined = string.Join(' ', terms).ToLowerInvariant();
            var text = $"{rawQuery} {combined}".ToLowerInvariant();
            return NigerianKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        public List<Recipe> Search(IEnumerable<string> terms, int max = 10)
        {
            var normalizedTerms = terms
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray();

            if (normalizedTerms.Length == 0)
            {
                return _recipes.Take(Math.Clamp(max, 1, 50)).Select(CloneRecipe).ToList();
            }

            var matches = _recipes
                .Where(r => normalizedTerms.All(term => ContainsTerm(r, term)))
                .Take(Math.Clamp(max, 1, 50))
                .Select(CloneRecipe)
                .ToList();

            return matches;
        }

        public Recipe? GetById(int id)
        {
            var recipe = _recipes.FirstOrDefault(r => r.Id == id);
            return recipe == null ? null : CloneRecipe(recipe);
        }

        private static bool ContainsTerm(Recipe recipe, string term)
        {
            if (recipe.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) return true;
            return recipe.Ingredients.Any(i =>
                !string.IsNullOrWhiteSpace(i.Ingredient?.Name) &&
                i.Ingredient!.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static Recipe CloneRecipe(Recipe recipe)
        {
            return new Recipe
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Summary = recipe.Summary,
                Instructions = recipe.Instructions,
                Category = recipe.Category,
                ImageUrl = recipe.ImageUrl,
                SourceUrl = recipe.SourceUrl,
                ReadyInMinutes = recipe.ReadyInMinutes,
                Servings = recipe.Servings,
                Nutrition = new NutritionInfo
                {
                    Calories = recipe.Nutrition.Calories,
                    ProteinGrams = recipe.Nutrition.ProteinGrams,
                    FatGrams = recipe.Nutrition.FatGrams,
                    CarbsGrams = recipe.Nutrition.CarbsGrams
                },
                Ingredients = recipe.Ingredients.Select(i => new RecipeIngredient
                {
                    Ingredient = new Ingredient { Name = i.Ingredient?.Name ?? string.Empty },
                    Quantity = i.Quantity
                }).ToList()
            };
        }

        private static Recipe CreateRecipe(int id, string title, string summary, IEnumerable<string> ingredients)
        {
            return new Recipe
            {
                Id = id,
                Title = title,
                Summary = summary,
                Category = "Nigerian",
                Instructions = "See preparation steps in your local recipe guide.",
                Servings = 4,
                ReadyInMinutes = 45,
                Ingredients = ingredients.Select(i => new RecipeIngredient
                {
                    Ingredient = new Ingredient { Name = i }
                }).ToList()
            };
        }
    }
}
