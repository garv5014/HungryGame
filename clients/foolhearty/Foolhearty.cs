using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace foolhearty;

public interface IPlayerLogic
{
    Task PlayAsync(CancellationTokenSource cancellationTokenSource);
}

public class Foolhearty : BasePlayerLogic
{
    private readonly ILogger<Foolhearty> logger;
    private readonly IConfiguration config;
    private string? token;
    private int errorCount = 0;
    private int sleepTime = 2_000;

    public Foolhearty(ILogger<Foolhearty> logger, IConfiguration config)
    {
        this.logger = logger;
        this.config = config;
    }

    public override async Task PlayAsync(CancellationTokenSource cancellationTokenSource)
    {
        await JoinGameAsync();
        await waitForGameToStart(cancellationTokenSource.Token);
        Console.WriteLine("Game started - making moves.");
        Location currentLocation = new Location(0, 0);
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            var board = await getBoardAsync();
            logger.LogInformation("Got board state; {cellsWithPills} cells with pills remain", board.Count(c => c.isPillAvailable));
            try
            {
                var destination = acquireTarget(currentLocation, board);
                var direction = inferDirection(currentLocation, destination);
            MOVE:
                var moveResultString = await httpClient.GetStringAsync($"{url}/move/{direction}?token={token}");
                var moveResultJson = JsonDocument.Parse(moveResultString).RootElement;
                var currentRow = moveResultJson.GetProperty("newLocation").GetProperty("row").GetInt32();
                var currentCol = moveResultJson.GetProperty("newLocation").GetProperty("column").GetInt32();
                var newLocation = new Location(currentRow, currentCol);
                if (newLocation == currentLocation)//we didn't move
                {
                    var newDirection = tryNextDirection(direction);
                    logger.LogInformation("Moving {lastDirection} didn't work, trying {newDirection} instead", direction, newDirection);
                    direction = newDirection;
                    if (cancellationTokenSource.IsCancellationRequested || await checkIfGameOver())
                    {
                        break;
                    }

                    goto MOVE;
                }
                else
                {
                    currentLocation = new Location(currentRow, currentCol);
                }

                if (await checkIfGameOver())
                {
                    Console.WriteLine("Game over.  Waiting for next game.");
                    while (true)
                    {
                        await Task.Delay(sleepTime, cancellationTokenSource.Token);
                        errorCount++;
                        sleepTime += 500;
                        if (errorCount > 200)
                        {
                            errorCount = 0;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Uh oh...");
                await Task.Delay(sleepTime, cancellationTokenSource.Token);
                errorCount++;
                sleepTime += 500;
                if (errorCount > 200)
                {
                    return;
                }
            }
        }
    }


    private Location acquireTarget(Location curLocation, List<Cell> board)
    {
        var max = new Location(int.MaxValue, int.MaxValue);
        var closest = max;
        var minDistance = double.MaxValue;
        foreach (var cell in board)
        {
            if (cell.isPillAvailable == false)
            {
                continue;
            }
            var a = curLocation.row - cell.location.row;
            var b = curLocation.column - cell.location.column;
            var newDistance = Math.Sqrt(a * a + b * b);
            if (newDistance < minDistance)
            {
                minDistance = newDistance;
                closest = cell.location;
            }
        }

        if (closest == max)//e.g. didn't find a pill to eat...look for another player
        {
            var minScore = int.MaxValue;
            foreach (var cell in board)
            {
                if (cell.occupiedBy == null || cell.location == curLocation)
                {
                    continue;
                }
                if (cell.occupiedBy.score < minScore)
                {
                    minScore = cell.occupiedBy.score;
                    closest = cell.location;
                }
            }
        }

        return closest;
    }

    protected override async Task JoinGameAsync()
    {
        var name = config["NAME"] ?? $"Client {DateTime.Now:HH.mm.ffff}";
        string fileName = $"connectionInfo_{name}.txt";
        if (File.Exists(fileName))
        {
            var parts = File.ReadAllText(fileName).Split('|');
            url = parts[0];
            token = parts[1];
        }
        else
        {
            url = config["SERVER"] ?? "https://hungrygame.azurewebsites.net";
            token = await httpClient.GetStringAsync($"{url}/join?playerName={name}");
            File.WriteAllText(fileName, $"{url}|{token}");
        }
    }
}
