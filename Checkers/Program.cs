using System.Text.Json;
using System.Text.Json.Serialization;
using Checkers.Contracts.Responses;
using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<IPieceFactory, PieceFactory>();
builder.Services.AddSingleton<IBoardFactory, BoardFactory>();
builder.Services.AddSingleton<IGameIdGenerator, GameIdGenerator>();
builder.Services.AddSingleton<IGameStore, InMemoryGameStore>();
builder.Services.AddSingleton<IGameFactory, GameFactory>();
builder.Services.AddSingleton<IGameService, GameService>();
builder.Services.AddSingleton<IGameProvider, GameProvider>();

// Swagger: explorer powers the schema, SwaggerGen builds /swagger/v1/swagger.json
// from the controllers plus the XML doc comments (GenerateDocumentationFile).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Checkers API",
        Version = "v1",
        Description = "10x10 international draughts REST API: create a game, "
            + "query legal moves, play moves, offer draws and resign.",
    });

    // Pull in the /// XML comments from the controllers and contracts.
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Checkers.xml"));
});

const string CorsPolicy = "Frontend";
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
        {
            policy.SetIsOriginAllowed(_ => true); 
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");
        logger.LogError(exception, "Unhandled exception while handling {Path}", context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(
            new ErrorResponse { Message = "internal server error" },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    });
});

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Checkers API v1");
});

app.UseCors(CorsPolicy);

app.MapGet("/health", () => "ok");

app.MapControllers();

app.Run();

public partial class Program
{
}

