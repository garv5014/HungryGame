using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace foolhearty;

public class SmartyPants : BasePlayerLogic
{
    private readonly ILogger<SmartyPants> logger;

    public SmartyPants(IConfiguration config, ILogger<SmartyPants> logger) : base(config)
    {
        this.logger = logger;
    }

    public override string PlayerName => "SmartyPants";

    public override async Task PlayAsync(CancellationTokenSource cancellationTokenSource)
    {
        logger.LogInformation("SmartyPants starting to play");
        var direction = "right";
        var oldLocation = new Location(0, 0);
        while (true)
        {
            logger.LogInformation("moving {direction}", direction);
            var moveResultString = await httpClient.GetStringAsync($"{url}/move/{direction}?token={token}");
            var moveResultJson = JsonDocument.Parse(moveResultString).RootElement;
            var newRow = moveResultJson.GetProperty("newLocation").GetProperty("row").GetInt32();
            var newCol = moveResultJson.GetProperty("newLocation").GetProperty("column").GetInt32();
            var newLocation = new Location(newRow, newCol);
            if (newLocation == oldLocation)
            {
                logger.LogInformation($"I can't go {direction} so I'll go {tryNextDirection(direction)}");
                direction = tryNextDirection(direction);
            }
            oldLocation = newLocation;
        }
    }
}
