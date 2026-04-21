# CookingRecipe API

ASP.NET Core Web API for recipe search, Spoonacular integration, favorites, and search history.

## Requirements

- .NET SDK 9.0+

## Run

```powershell
dotnet restore
dotnet run --project CookingRecipe.csproj
```

## Build

```powershell
dotnet build cookingrecipe.sln
```

## Database (EF Core + SQLite)

Create migration:

```powershell
dotnet tool run dotnet-ef migrations add InitialCreate --context CookingRecipeContext --output-dir Migrations
```

Apply migrations:

```powershell
dotnet tool run dotnet-ef database update --context CookingRecipeContext
```

The app also runs pending migrations automatically at startup.

## Tests

```powershell
dotnet test cookingrecipe.sln
```

## Configure Secrets (Required for live Spoonacular search)

Use user-secrets in development:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Spoonacular:ApiKey" "YOUR_SPOONACULAR_KEY"
dotnet user-secrets set "ConnectionStrings:Redis" "redis://username:password@host:port"
```

Notes:
- `Spoonacular:ApiKey` is required for live provider endpoints.
- Redis is optional; if not configured, the app falls back to in-memory storage.

## API Endpoints

`Recipes`
- `GET /api/recipes/search?ingredients=rice,beans&max=10`
- `POST /api/recipes/suggest-by-ingredients`
- `GET /api/recipes`
- `GET /api/recipes/{id}`
- `GET /api/recipes/stored/search?ingredients=tomato,onion&max=50`
- `POST /api/recipes/{id}/favorite`
- `DELETE /api/recipes/{id}/favorite`
- `GET /api/recipes/favorites`

`Search History`
- `GET /api/searchhistory`
- `POST /api/searchhistory`

`Health`
- `GET /health`

## Notes

- Swagger UI is enabled at startup (`/swagger`).
- App root (`/`) redirects to Swagger.
