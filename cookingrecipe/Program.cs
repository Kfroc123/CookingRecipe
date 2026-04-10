
using StackExchange.Redis;
using CookingRecipe.Services;

namespace cookingrecipe
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Redis connection (only use Redis when explicitly configured)
            var redisConn = builder.Configuration.GetConnectionString("Redis");
            builder.Services.AddSingleton<IRedisStore>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Program>>();
                if (string.IsNullOrWhiteSpace(redisConn))
                {
                    logger.LogInformation("No Redis connection configured. Using in-memory recipe store.");
                    return new InMemoryRecipeStore();
                }

                try
                {
                    var normalizedRedisConn = NormalizeRedisConnectionString(redisConn);
                    var multiplexer = ConnectionMultiplexer.Connect($"{normalizedRedisConn},abortConnect=false");
                    if (!multiplexer.IsConnected)
                    {
                        logger.LogWarning("Redis configured but not connected. Falling back to in-memory recipe store.");
                        return new InMemoryRecipeStore();
                    }

                    logger.LogInformation("Using Redis recipe store at {RedisConnection}.", redisConn);
                    return new RedisRecipeStore(multiplexer);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Redis unavailable. Falling back to in-memory recipe store.");
                    return new InMemoryRecipeStore();
                }
            });

            // Register HttpClient and Spoonacular service
            builder.Services.AddHttpClient<ISpoonacularService, SpoonacularService>();

            builder.Services.AddControllers();
            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();


            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();


            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            // ensure anonymous device id cookie is present
            app.UseMiddleware<CookingRecipe.Middleware.DeviceIdMiddleware>();

            app.UseAuthorization();


            app.MapGet("/", () => Results.Redirect("/swagger"));
            app.MapControllers();

            app.Run();
        }

        private static string NormalizeRedisConnectionString(string connectionString)
        {
            if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            {
                return connectionString;
            }

            if (!string.Equals(uri.Scheme, "redis", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "rediss", StringComparison.OrdinalIgnoreCase))
            {
                return connectionString;
            }

            var user = string.Empty;
            var password = string.Empty;
            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                user = Uri.UnescapeDataString(parts[0]);
                if (parts.Length > 1)
                {
                    password = Uri.UnescapeDataString(parts[1]);
                }
            }

            var list = new List<string> { $"{uri.Host}:{uri.Port}" };
            if (!string.IsNullOrWhiteSpace(user))
            {
                list.Add($"user={user}");
            }
            if (!string.IsNullOrWhiteSpace(password))
            {
                list.Add($"password={password}");
            }
            if (string.Equals(uri.Scheme, "rediss", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("ssl=true");
            }

            return string.Join(",", list);
        }
    }
}
