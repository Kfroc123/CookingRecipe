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

## Deploy

Production layout:

- Backend: Render (Docker web service from `CookingRecipe`)
- Frontend: Vercel (Vite app from `cooking_recipe_ui`)
- The Vercel `/api` serverless proxy forwards requests to Render. Leave `VITE_API_BASE_URL` unset on Vercel so the browser calls same-origin `/api/...` (needed for the `deviceId` cookie / favorites).

`render.yaml` at the repo root describes the API service. `cooking_recipe_ui/vercel.json` is the Vercel SPA config (rewrites skip `/api`).

### 1. Deploy the API on Render

1. Push this repository to GitHub.
2. In [Render](https://dashboard.render.com), create a Blueprint from the repo, or a **Web Service** with:
   - Root directory: `CookingRecipe`
   - Runtime: Docker
   - Health check path: `/health`
3. Set environment variables:

| Variable | Required | Notes |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` (already in `render.yaml`) |
| `Spoonacular__ApiKey` | No | Live Spoonacular search; otherwise the Nigerian dataset is used |
| `YouTube__ApiKey` | No | YouTube tutorials |
| `Cors__AllowedOrigins__0` | No | Your Vercel URL, e.g. `https://your-app.vercel.app` (backup; the UI proxy is server-to-server) |
| `ConnectionStrings__Redis` | No | Favorites persist across deploys. Without Redis, SQLite lives in temp storage and **resets on each Render restart** |

4. After the deploy finishes, confirm:

```text
https://<your-service>.onrender.com/health
https://<your-service>.onrender.com/swagger
```

The first request on a free Render instance can take a minute while the service wakes up.

### 2. Deploy the UI on Vercel

1. In [Vercel](https://vercel.com), import the same GitHub repository.
2. Set **Root Directory** to `cooking_recipe_ui`.
3. Framework: Vite. Build command: `npm run build`. Output: `dist`.
4. Environment variables:

| Variable | Required | Notes |
| --- | --- | --- |
| `BACKEND_API_BASE_URL` | Yes | Render URL, e.g. `https://<your-service>.onrender.com` |
| `VITE_API_BASE_URL` | Do not set | Empty means the browser uses `/api` on Vercel |

5. Deploy. If you add or change `BACKEND_API_BASE_URL` after the first deploy, redeploy so the proxy picks it up.

### 3. Verify

1. Open the Vercel URL: home, search, recipe detail, YouTube section, favorites.
2. In the browser Network tab, API calls should go to `/api/...` on the Vercel host, not `localhost`.
3. `https://<your-service>.onrender.com/health` should return OK.
