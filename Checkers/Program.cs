using System.Text.Json;
using System.Text.Json.Serialization;
using Checkers.Contracts.Responses;
using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi;
using Serilog;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/checkers.log")
    .CreateLogger();

try
{
    Log.Information("Checkers API starting up");

    var builder = WebApplication.CreateBuilder(args);

  
    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

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

    // Swagger
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
            
            Log.Error(exception, "Unhandled exception while handling {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new ErrorResponse { Message = "internal server error" },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        });
    });

   
    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Checkers API v1");
    });

    app.UseCors(CorsPolicy);

    app.MapGet("/health", () => "ok");

    app.MapControllers();

    app.Run();

    Log.Information("Checkers API stopped cleanly");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Checkers API terminated unexpectedly");
    return 1;
}
finally
{

    Log.CloseAndFlush();
}

public partial class Program
{
}
