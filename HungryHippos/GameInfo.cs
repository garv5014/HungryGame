using System.Collections.Concurrent;

namespace HungryHippos;

public class Player
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string Token { get; init; }
    public int Score { get; set; } = 0;
}

public class GameInfo
{
    private int number = 0;
    private readonly ConcurrentDictionary<int, Player> players = new();
    private readonly ConcurrentDictionary<Location, Cell> cells = new();
    private long isGameStarted = 0;
    private readonly Random rnd = new Random();
    private readonly IConfiguration config;
    private readonly ILogger<GameInfo> log;
    private readonly object lockObjeck = new();
    public int MaxRows { get; private set; } = 0;
    public int MaxCols { get; private set; } = 0;
    public event EventHandler GameStateChanged;

    public GameInfo(IConfiguration config, ILogger<GameInfo> log)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.log = log;
    }

    public bool IsGameStarted => Interlocked.Read(ref isGameStarted) != 0;

    public void StartGame(int numRows, int numColumns, string secretCode)
    {
        if (secretCode != config["SECRET_CODE"] || Interlocked.Read(ref isGameStarted) != 0)
        {
            return;
        }

        MaxRows = numRows;
        MaxCols = numColumns;

        lock (lockObjeck)
        {
            cells.Clear();
            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numColumns; c++)
                {
                    Location location = new Location(r, c);
                    cells.TryAdd(location, new Cell(location, true, null));
                }
            }

            foreach (var p in players)
            {
                Location newLocation;
                do
                {
                    newLocation = new Location(rnd.Next(numRows), rnd.Next(numColumns));
                }
                while (cells[newLocation].OccupiedBy != null);
                cells[newLocation] = cells[newLocation] with { OccupiedBy = p.Value };
            }

            Interlocked.Increment(ref isGameStarted);
        }
    }

    public Cell GetCell(int row, int col) => cells[new Location(row, col)];

    public string JoinPlayer(string playerName)
    {
        if (Interlocked.Read(ref isGameStarted) == 0)
        {
            var id = Interlocked.Increment(ref number);
            var token = Guid.NewGuid().ToString();
            players.AddOrUpdate(id, new Player { Id = id, Name = playerName, Token = token}, (key, value) => value);
            GameStateChanged?.Invoke(this, EventArgs.Empty);
            return token;
        }
        else
        {
            throw new GameAlreadyStartedException();
        }
    }

    public IEnumerable<Player> GetPlayers() =>
        players.Select(p => p.Value)
            .OrderByDescending(s => s.Score);

    public void Move(string playerToken, Direction direction)
    {
        var player = players.FirstOrDefault(kvp => kvp.Value.Token == playerToken).Value;
        var cell = cells.FirstOrDefault(kvp => kvp.Value.OccupiedBy?.Token == playerToken).Value;
        var currentLocation = cell.Location;
        Location newLocation = direction switch
        {
            Direction.Up => currentLocation with { Row = currentLocation.Row - 1 },
            Direction.Down => currentLocation with { Row = currentLocation.Row + 1 },
            Direction.Left => currentLocation with { Column = currentLocation.Column - 1 },
            Direction.Right => currentLocation with { Column = currentLocation.Column + 1 },
            _ => throw new DirectionNotRecognizedException()
        };

        if (newLocation.Row >= 0 && newLocation.Row < MaxRows &&
            newLocation.Column >= 0 && newLocation.Column < MaxCols &&
            cells[newLocation].OccupiedBy == null)
        {
            var origDestinationCell = cells[newLocation];
            if(origDestinationCell.IsPillAvailable)
            {
                player.Score++;
            }
            var newDestinationCell = origDestinationCell with { OccupiedBy = player, IsPillAvailable = false };

            var origSourceCell = cells[currentLocation];
            var newSourceCell = origSourceCell with { OccupiedBy = null };

            log.LogInformation("Moving {playerName} from {oldLocation} to {newLocation} ({ateNewPill})", player.Name, currentLocation, newLocation, origDestinationCell.IsPillAvailable);

            cells.TryUpdate(newLocation, newDestinationCell, origDestinationCell);
            cells.TryUpdate(currentLocation, newSourceCell, origSourceCell);

            GameStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
