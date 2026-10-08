# Cooking Recipe App

This repository contains two projects:

- `CookingRecipe` - ASP.NET Core Web API backend
- `cooking_recipe_ui` - React + Vite frontend

The folders are kept inside this repository as normal project folders.

## Requirements

- .NET SDK 9.0 or newer
- Node.js and npm

## Run the Backend

Open a terminal in the backend folder:

```powershell
cd CookingRecipe
dotnet restore
dotnet run --project CookingRecipe.csproj
```

The API runs at:

```text
http://localhost:5209
```

Swagger is available at:

```text
http://localhost:5209/swagger
```

## Backend Configuration

The backend can run with its local Nigerian recipe dataset. Live external services need API keys.

For development, configure secrets from inside the `CookingRecipe` folder:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Spoonacular:ApiKey" "YOUR_SPOONACULAR_KEY"
dotnet user-secrets set "YouTube:ApiKey" "YOUR_YOUTUBE_API_KEY"
```

Redis is optional. If Redis is not configured or unavailable, the app falls back to SQLite-backed storage.

Optional Redis configuration:

```powershell
dotnet user-secrets set "ConnectionStrings:Redis" "redis://username:password@host:port"
```

## Run the Frontend

Open another terminal in the frontend folder:

```powershell
cd cooking_recipe_ui
npm install
npm run dev
```

The frontend runs at:

```text
http://localhost:5173
```

The frontend uses this API base URL by default:

```text
http://localhost:5209
```

You can also set it explicitly by creating `cooking_recipe_ui/.env`:

```text
VITE_API_BASE_URL=http://localhost:5209
```

## Typical Local Workflow

1. Start the backend from `CookingRecipe`.
2. Start the frontend from `cooking_recipe_ui`.
3. Open `http://localhost:5173` in your browser.
4. Use `http://localhost:5209/swagger` to inspect and test API endpoints.

## Useful Commands

Backend:

```powershell
cd CookingRecipe
dotnet build cookingrecipe.sln
```

Frontend:

```powershell
cd cooking_recipe_ui
npm run build
```
