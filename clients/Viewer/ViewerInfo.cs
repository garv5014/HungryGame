using System.Net.Http.Json;

namespace Viewer;

public class ViewerInfo
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration config;
    private readonly ILogger<ViewerInfo> logger;
    private readonly Timer timer;
    private readonly string server;
    private List<Cell> board = new(new[] { new Cell(new Location(0, 0), false, null) });
    private Dictionary<Location, Cell> map = new();
    private List<PlayerInfo> players = new();
    private string gameState = String.Empty;
    public event EventHandler? UpdateTick;

    public ViewerInfo(IConfiguration config, ILogger<ViewerInfo> logger)
    {
        logger.LogInformation("Instantiating ViewerInfo");
        this.httpClient = new HttpClient();
        this.config = config;
        this.logger = logger;
        timer = new Timer(timerTick, null, 0, 1_000);
        server = config["SERVER"] ?? "https://hungrygame.azurewebsites.net";
    }

    private async void timerTick(object? state)
    {
        logger.LogInformation("timerTick() start");

        var newBoard = (await httpClient.GetFromJsonAsync<IEnumerable<Cell>>($"{server}/board")).ToList();

        var newMap = new Dictionary<Location, Cell>(newBoard.Select(c => new KeyValuePair<Location, Cell>(c.Location, c)));
        Interlocked.Exchange(ref board, newBoard);
        Interlocked.Exchange(ref map, newMap);

        var newPlayers = (await httpClient.GetFromJsonAsync<IEnumerable<PlayerInfo>>($"{server}/players")).ToList();
        Interlocked.Exchange(ref players, newPlayers);

        var newGameState = await httpClient.GetStringAsync($"{server}/state");
        Interlocked.Exchange(ref gameState, newGameState);

        logger.LogInformation("timerTick() end");
        UpdateTick?.Invoke(this, EventArgs.Empty);
    }

    public bool IsGameStarted => gameState != "Joining" && gameState != "GameOver";
    public string CurrentGameState => gameState;
    public DateTime? GameEndsOn { get; private set; }
    public TimeSpan TimeRemaining => (GameEndsOn ?? DateTime.Now) - DateTime.Now;
    public int MaxRows => board.Max(c => c.Location.Row);
    public int MaxColumns => board.Max(c => c.Location.Column);
    public Cell GetCell(int row, int col) => map[new Location(row, col)];
    public List<PlayerInfo> GetPlayersByScoreDescending() => players.OrderByDescending(p => p.Score).ToList();
}

public class PlayerInfo
{
    public string Name { get; set; }
    public int Id { get; set; }
    public int Score { get; set; }
}

public record Location(int Row, int Column);
public record RedactedPlayer(int Id, string Name, int Score);
public record Cell(Location Location, bool IsPillAvailable, RedactedPlayer OccupiedBy);