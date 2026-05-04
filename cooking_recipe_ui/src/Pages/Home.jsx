import { useEffect, useMemo, useState } from "react";
import { FaHeart, FaSearch, FaTimes } from "react-icons/fa";
import RecipeCard from "../Components/RecipeCard";
import {
  addFavorite,
  getAll,
  getFavorites,
  removeFavorite,
  searchRecipes,
} from "../Utils/Recipes";

const QUICK_INGREDIENTS = [
  "rice",
  "beans",
  "tomato",
  "onion",
  "plantain",
  "yam",
  "egg",
  "chicken",
  "fish",
  "pepper",
];

function Home() {
  const [allRecipes, setAllRecipes] = useState([]);
  const [recipes, setRecipes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searching, setSearching] = useState(false);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const [favoriteIds, setFavoriteIds] = useState(new Set());
  const [showFavoritesOnly, setShowFavoritesOnly] = useState(false);

  const parseIngredients = (value) => {
    return value
      .toLowerCase()
      .split(/\s*(?:,|;|\+|&|\band\b)\s*/i)
      .map((item) => item.trim())
      .filter(Boolean);
  };

  const selectedIngredientSet = useMemo(() => {
    return new Set(parseIngredients(search));
  }, [search]);

  useEffect(() => {
    const loadInitialData = async () => {
      setLoading(true);
      setError("");

      try {
        const [initialRecipes, favorites] = await Promise.all([getAll(), getFavorites()]);
        const safeInitial = Array.isArray(initialRecipes) ? initialRecipes : [];
        const favoriteSet = new Set((Array.isArray(favorites) ? favorites : []).map((item) => item.id));

        setAllRecipes(safeInitial);
        setRecipes(safeInitial);
        setFavoriteIds(favoriteSet);
      } catch (err) {
        setError(err.message || "Unable to load recipes right now.");
      } finally {
        setLoading(false);
      }
    };

    loadInitialData();
  }, []);

  useEffect(() => {
    const query = search.trim();

    if (!query) {
      setRecipes(allRecipes);
      setSearching(false);
      setError("");
      return;
    }

    setSearching(true);
    setError("");

    const timeoutId = setTimeout(async () => {
      try {
        const data = await searchRecipes(query, 50);
        setRecipes(Array.isArray(data) ? data : []);
      } catch (err) {
        setRecipes([]);
        setError(err.message || "Unable to search recipes right now.");
      } finally {
        setSearching(false);
      }
    }, 350);

    return () => clearTimeout(timeoutId);
  }, [search, allRecipes]);

  const visibleRecipes = useMemo(() => {
    if (!showFavoritesOnly) return recipes;
    return recipes.filter((item) => favoriteIds.has(item.id));
  }, [recipes, showFavoritesOnly, favoriteIds]);

  const toggleFavorite = async (recipeId) => {
    const isCurrentlyFavorite = favoriteIds.has(recipeId);

    setFavoriteIds((prev) => {
      const next = new Set(prev);
      if (isCurrentlyFavorite) {
        next.delete(recipeId);
      } else {
        next.add(recipeId);
      }
      return next;
    });

    try {
      if (isCurrentlyFavorite) {
        await removeFavorite(recipeId);
      } else {
        await addFavorite(recipeId);
      }
    } catch (err) {
      setFavoriteIds((prev) => {
        const next = new Set(prev);
        if (isCurrentlyFavorite) {
          next.add(recipeId);
        } else {
          next.delete(recipeId);
        }
        return next;
      });
      setError(err.message || "Unable to update favorites right now.");
    }
  };

  const toggleQuickIngredient = (ingredient) => {
    const current = new Set(parseIngredients(search));
    if (current.has(ingredient)) {
      current.delete(ingredient);
    } else {
      current.add(ingredient);
    }
    setSearch(Array.from(current).join(", "));
  };

  return (
    <section className="page-container">
      <div className="home-toolbar">
        <label className="search-bar" htmlFor="recipe-search">
          <FaSearch className="search-icon" />
          <input
            id="recipe-search"
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Try: rice and beans, tomato + onion, beans, plantain"
          />
          {search && (
            <button
              className="clear-search"
              type="button"
              onClick={() => setSearch("")}
              aria-label="Clear search"
            >
              <FaTimes />
            </button>
          )}
        </label>
        <div className="ingredient-hints">
          <span>Use comma</span>
          <span>Use and</span>
          <span>Use +</span>
        </div>
        <div className="quick-ingredients">
          {QUICK_INGREDIENTS.map((ingredient) => (
            <button
              key={ingredient}
              type="button"
              className={`ingredient-chip ${selectedIngredientSet.has(ingredient) ? "active" : ""}`}
              onClick={() => toggleQuickIngredient(ingredient)}
            >
              {ingredient}
            </button>
          ))}
        </div>

        <button
          type="button"
          className={`favorites-filter ${showFavoritesOnly ? "active" : ""}`}
          onClick={() => setShowFavoritesOnly((prev) => !prev)}
        >
          <FaHeart />
          <span>{showFavoritesOnly ? "Showing favorites" : "Show favorites"}</span>
        </button>
      </div>

      {(loading || searching) && <div className="spinner" aria-label="Loading recipes" />}

      {!loading && !searching && (
        <>
          <div className="section-header">
            <div>
              <h2>Recipe Search</h2>
              <p>
                {search
                  ? `${visibleRecipes.length} result(s) for "${search}"`
                  : `${visibleRecipes.length} recipe(s) from your latest 50. Multi-ingredient search matches all entered ingredients.`}
              </p>
            </div>
          </div>

          {error ? (
            <div className="empty-state">{error}</div>
          ) : visibleRecipes.length === 0 ? (
            <div className="empty-state">
              {showFavoritesOnly
                ? "No favorite recipes in this view yet."
                : "No recipes match your search."}
            </div>
          ) : (
            <div className="recipe-grid">
              {visibleRecipes.map((item) => (
                <RecipeCard
                  item={item}
                  key={item.id}
                  isFavorite={favoriteIds.has(item.id)}
                  onToggleFavorite={toggleFavorite}
                />
              ))}
            </div>
          )}
        </>
      )}
    </section>
  );
}

export default Home;
