using HungryHippos.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<GameInfo>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}


app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapPost("/join", (string userName, ILogger<Program> logger, GameInfo gameInfo) =>
{
    logger.LogInformation(userName + " wants to sign up.");
    return Results.Ok(gameInfo.JoinPlayer(userName));
});
app.MapPost("/move", (MoveRequest moveRequest, ILogger<Program> logger) =>
{
    logger.LogInformation($"{moveRequest.UserToken} wants to move {moveRequest.Direction}");
    return Results.Ok();
});
app.MapGet("/users", ([FromServices] GameInfo gameInfo) => gameInfo.GetPlayers());

app.Run();

public record MoveRequest(string Direction, string UserToken);

public class GameInfo
{
    private int number = 0;
    private ConcurrentDictionary<string, string> players = new();
    private ConcurrentDictionary<Location, Cell> cells = new();
    private long isGameStarted = 0;
    [ThreadStatic]
    private Random rnd = new Random();
    private readonly IConfiguration config;

    public GameInfo(IConfiguration config)
    {
        this.config = config;
    }

    public void StartGame(int numRows, int numColumns, string secretCode)
    {
        if(secretCode != config["SECRET_CODE"] || Interlocked.Read(ref isGameStarted) != 0)
        {
            return;
        }

        cells.Clear();
        for (int r = 0; r < numRows; r++)
        {
            for (int c = 0; c < numColumns; c++)
            {
                Location location = new Location(r, c);
                cells.TryAdd(location, new Cell(location, true, null));
            }
        }

        foreach(var p in players)
        {
            Location newLocation;
            do
            {
                newLocation = new Location(rnd.Next(numRows), rnd.Next(numColumns));
            }
            while (cells[newLocation].OccupiedBy != null);
            cells[newLocation] = cells[newLocation] with { OccupiedBy = p.Value };
        }
    }

    public IResult JoinPlayer(string playerName)
    {
        if (Interlocked.Read(ref isGameStarted) == 0)
        {
            var id = Interlocked.Increment(ref number).ToString();
            players.AddOrUpdate(id, playerName, (key, value) => value);
            return Results.Ok(id);
        }
        else
        {
            return Results.BadRequest("Game already started.");
        }
    }

    public IEnumerable<string> GetPlayers() =>
        players.Select(p => $"{p.Key}: {p.Value}")
            .OrderBy(s => s);

}

public record Cell(Location location, bool IsPillAvailable, string? OccupiedBy);
public record Location(int Row, int Column);