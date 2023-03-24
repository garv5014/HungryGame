using HungryGame;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
var requestErrorCount = 0L;

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<Counters>();
builder.Services.AddSingleton<GameLogic>();
builder.Services.AddSingleton<IRandomService, SystemRandomService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = builder.Environment.ApplicationName, Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseCors(builder =>
{
    builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader();
});

app.UseStaticFiles();
app.UseMetricServer();

//THROW_ERRORS middleware
app.Use(async (context, next) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    if (app.Configuration["THROW_ERRORS"] == "true")
    {
        Interlocked.Increment(ref requestErrorCount);
        if (Interlocked.Read(ref requestErrorCount) % 4 == 0)
        {
            logger.LogInformation("THROW_ERRORS enabled...every 4th request dies.");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Every 4th request fails!");
            return;
        }
    }
    await next();
});

app.UseRouting();
app.MapBlazorHub();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"));

app.MapFallbackToPage("/_Host");

//API endpoints
app.MapGet("/join", (string? userName, string? playerName, GameLogic gameLogic) =>
{
    var name = userName ?? playerName ?? throw new ArgumentNullException(nameof(userName), "Must define either a userName or playerName in the query string.");
    return gameLogic.JoinPlayer(name);
});
app.MapGet("/move/left", (string token, GameLogic gameLogic) => gameLogic.Move(token, Direction.Left));
app.MapGet("/move/right", (string token, GameLogic gameLogic) => gameLogic.Move(token, Direction.Right));
app.MapGet("/move/up", (string token, GameLogic gameLogic) => gameLogic.Move(token, Direction.Up));
app.MapGet("/move/down", (string token, GameLogic gameLogic) => gameLogic.Move(token, Direction.Down));
app.MapGet("/players", ([FromServices] GameLogic gameLogic, IMemoryCache memoryCache) =>
{
    return memoryCache.GetOrCreate("players", cacheEntry =>
    {
        cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
        return gameLogic.GetPlayersByScoreDescending().Select(p => new { p.Name, p.Id, p.Score });
    });

});
app.MapGet("/start", (int numRows, int numCols, string password, int? timeLimit, GameLogic gameLogic) =>
{
    var gameStart = new NewGameInfo
    {
        NumColumns = numCols,
        NumRows = numRows,
        SecretCode = password,
        IsTimed = timeLimit.HasValue,
        TimeLimitInMinutes = timeLimit,
    };
    gameLogic.StartGame(gameStart);
});
app.MapGet("/reset", (string password, GameLogic gameLogic) => gameLogic.ResetGame(password));
app.MapGet("/board", ([FromServices] GameLogic gameLogic, IMemoryCache memoryCache, ILogger<Program> logger) =>
{
    logger.LogInformation("Getting /board");
    return memoryCache.GetOrCreate("board",
        cacheEntry =>
        {
            logger.LogInformation("Cache expired.  Re-computing /board");
            cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
            return gameLogic.GetBoardState();
        });
});
app.MapGet("/state", ([FromServices] GameLogic gameLogic, IMemoryCache memoryCache) =>
{
    return memoryCache.GetOrCreate("state", cacheEntry =>
   {
       cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
       return gameLogic.CurrentGameState.ToString();
   });
});

app.Run();