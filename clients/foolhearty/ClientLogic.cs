using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
namespace foolhearty
{
    public class ClientLogic : IHostedService
    {
        private HttpClient httpClient = new();
        private string token;
        private int errorCount = 0;
        private int sleepTime = 2_000;
        private string url = "";
        private readonly IConfiguration config;

        public ClientLogic(IConfiguration config)
        {
            this.config = config;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await joinGame();
            await waitForGameToStart();
            Console.WriteLine("Game started - making moves.");
            Location currentLocation = new Location(0, 0);
            while (!cancellationToken.IsCancellationRequested)
            {
                var board = await getBoardAsync();
                try
                {
                    var destination = getClosest(currentLocation, board);
                    var direction = determineDirection(currentLocation, destination);
                MOVE:
                    var moveResultString = await httpClient.GetStringAsync($"{url}/move/{direction}?token={token}");
                    var moveResultJson = JsonDocument.Parse(moveResultString).RootElement;
                    var currentRow = moveResultJson.GetProperty("newLocation").GetProperty("row").GetInt32();
                    var currentCol = moveResultJson.GetProperty("newLocation").GetProperty("column").GetInt32();
                    var newLocation = new Location(currentRow, currentCol);
                    if (newLocation == currentLocation)//we didn't move
                    {
                        direction = tryNextDirection(direction);
                        goto MOVE;
                    }
                    else
                    {
                        currentLocation = new Location(currentRow, currentCol);
                    }

                    if ((await httpClient.GetStringAsync($"{url}/state")) == "GameOver")
                    {
                        Console.WriteLine("Game over.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Oops! {ex}");
                    Thread.Sleep(sleepTime);
                    errorCount++;
                    sleepTime += 500;
                    if (errorCount > 200)
                    {
                        return;
                    }
                }
            }
        }

        private static string tryNextDirection(string direction) => direction switch
        {
            "down" => "left",
            "left" => "up",
            "up" => "right",
            "right" => "down"
        };

        private static string determineDirection(Location currentLocation, Location destination)
        {
            if (currentLocation.row < destination.row)
            {
                return "down";
            }
            else if (currentLocation.row > destination.row)
            {
                return "up";
            }

            if (currentLocation.column < destination.column)
            {
                return "right";
            }
            return "left";
        }

        private static Location getClosest(Location curLocation, List<Cell> board)
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

        async Task waitForGameToStart()
        {
            var gameState = await httpClient.GetStringAsync($"{url}/state");
            while (gameState == "Joining" || gameState == "GameOver")
            {
                Thread.Sleep(2_000);
                gameState = await httpClient.GetStringAsync($"{url}/state");
            }
        }

        async Task joinGame()
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

        async Task<List<Cell>> getBoardAsync()
        {
            var boardString = await httpClient.GetStringAsync($"{url}/board");
            return JsonSerializer.Deserialize<IEnumerable<Cell>>(boardString).ToList();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
    record Location(int row, int column);
    record RedactedPlayer(int id, string name, int score);
    record Cell(Location location, bool isPillAvailable, RedactedPlayer occupiedBy);
}