using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace foolhearty;

public class SmartyPants : BasePlayerLogic
{
    private readonly ILogger<SmartyPants> logger;
    private Dictionary<Location, Cell> map;
    private List<Cell> board;

    public SmartyPants(IConfiguration config, ILogger<SmartyPants> logger) : base(config)
    {
        this.logger = logger;
    }

    public override string PlayerName => "SmartyPants";

    public override async Task PlayAsync(CancellationTokenSource cancellationTokenSource)
    {
        logger.LogInformation("SmartyPants starting to play");

        var timer = new Timer(getBoard, null, 0, 1_000);

        var direction = "right";
        var moveResult = new MoveResult { newLocation = new Location(0, 0) };
        while (true)
        {
            await refreshBoardAndMap();
            var destination = acquireTarget(moveResult?.newLocation, board);
            direction = inferDirection(moveResult?.newLocation, destination);
            moveResult = await httpClient.GetFromJsonAsync<MoveResult>($"{url}/move/{direction}?token={token}");
            if (moveResult?.ateAPill == false)
            {
                continue;
            }
            var nextLocation = advance(moveResult?.newLocation, direction);
            Task<MoveResult> lastRequest = null;
            while (map.ContainsKey(nextLocation) && map[nextLocation].isPillAvailable)
            {
                lastRequest = httpClient.GetFromJsonAsync<MoveResult>($"{url}/move/{direction}?token={token}");
                nextLocation = advance(nextLocation, direction);
            }
            if (lastRequest != null)
            {
                moveResult = await lastRequest;
            }
        }

        return;

        //old logic *********************************************************
        direction = "right";
        var oldLocation = new Location(0, 0);
        while (true)
        {
            logger.LogInformation("moving {direction}", direction);
            moveResult = await httpClient.GetFromJsonAsync<MoveResult>($"{url}/move/{direction}?token={token}");

            if (moveResult?.newLocation == oldLocation)
            {
                logger.LogInformation($"I can't go {direction} so I'll go {tryNextDirection(direction)}");
                direction = tryNextDirection(direction);
                oldLocation = moveResult?.newLocation;
            }
            else if (moveResult?.ateAPill == false)
            {
                //didn't eat a pill, better pick a new direction.
                var nextLocation = advance(moveResult?.newLocation, direction);
                do
                {
                    var destination = acquireTarget(moveResult?.newLocation, board);
                    direction = inferDirection(moveResult?.newLocation, destination);

                    logger.LogInformation($"Didn't eat a pill...moving {direction} toward next target.");
                    moveResult = await httpClient.GetFromJsonAsync<MoveResult>($"{url}/move/{direction}?token={token}");
                    nextLocation = advance(nextLocation, direction);
                } while (moveResult?.ateAPill == false && map.ContainsKey(nextLocation));
            }
            else
            {
                var nextLocation = advance(moveResult?.newLocation, direction);
                Task<MoveResult> lastRequest = null;
                while (map != null && map.ContainsKey(nextLocation) && map[nextLocation].isPillAvailable)
                {
                    logger.LogInformation($"There's still a pill to my {direction} so I'll keep moving that way.");
                    lastRequest = httpClient.GetFromJsonAsync<MoveResult>($"{url}/move/{direction}?token={token}");
                    nextLocation = advance(nextLocation, direction);
                }
                logger.LogInformation("End of the road...where to now?");
                if (lastRequest != null)
                {
                    var result = await lastRequest;
                    oldLocation = result.newLocation;
                }
                else
                {
                    oldLocation = nextLocation;
                }
            }
        }
    }

    private async Task refreshBoardAndMap()
    {
        board = await getBoardAsync();
        map = new Dictionary<Location, Cell>(board.Select(c => new KeyValuePair<Location, Cell>(c.location, c)));
    }

    private Location advance(Location? lastLocation, string direction)
    {
        return direction switch
        {
            "left" => lastLocation with { column = lastLocation.column - 1 },
            "right" => lastLocation with { column = lastLocation.column + 1 },
            "up" => lastLocation with { row = lastLocation.row - 1 },
            "down" => lastLocation with { row = lastLocation.row + 1 },
            _ => lastLocation
        };
    }

    private async void getBoard(object _)
    {
        var newBoard = await getBoardAsync();
        var newMap = new Dictionary<Location, Cell>(newBoard.Select(c => new KeyValuePair<Location, Cell>(c.location, c)));
        Interlocked.Exchange(ref board, newBoard);
        Interlocked.Exchange(ref map, newMap);
    }

    public class MoveResult
    {
        public Location newLocation { get; set; }
        public bool ateAPill { get; set; }
    }

}
