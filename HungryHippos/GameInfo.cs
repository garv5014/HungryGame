using System.Collections.Concurrent;

namespace HungryHippos;

public interface IRandomService
{
    public int Next(int maxValue);
}

public class SystemRandomService : IRandomService
{
    [ThreadStatic]
    private static Random random = new ();
    public int Next(int maxValue) => random.Next(maxValue);
}

public class GameInfo
{
    private int number = 0;
    private readonly ConcurrentDictionary<int, Player> players = new();
    private readonly ConcurrentDictionary<Location, Cell> cells = new();
    private long isGameStarted = 0;
    private readonly IConfiguration config;
    private readonly ILogger<GameInfo> log;
    private readonly IRandomService random;
    private readonly object lockObject = new();
    public int MaxRows { get; private set; } = 0;
    public int MaxCols { get; private set; } = 0;
    private readonly ConcurrentQueue<int> pillValues = new();
    public event EventHandler GameStateChanged;

    public GameInfo(IConfiguration config, ILogger<GameInfo> log, IRandomService random)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.log = log;
        this.random = random;
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


        initializeGame();
    }

    private void initializeGame()
    {
        lock (lockObject)
        {
            cells.Clear();
            for (int r = 0; r < MaxRows; r++)
            {
                for (int c = 0; c < MaxCols; c++)
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
                    newLocation = new Location(random.Next(MaxRows), random.Next(MaxCols));
                }
                while (cells[newLocation].OccupiedBy != null);
                cells[newLocation] = cells[newLocation] with { OccupiedBy = p.Value };
                p.Value.Score = 0;
            }

            pillValues.Clear();
            for(int i = 1; i <= MaxRows * MaxCols; i++)
            {
                pillValues.Enqueue(i);
            }

            Interlocked.Increment(ref isGameStarted);
        }

        GameStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetGame(string secretCode)
    {
        if(secretCode != config["SECRET_CODE"] || Interlocked.Read(ref isGameStarted) == 0)
        {
            return;
        }

        Interlocked.Exchange(ref isGameStarted, 0);
        lock(lockObject)
        {
            foreach(var p in players)
            {
                p.Value.Score = 0;
            }
        }
        GameStateChanged?.Invoke(this, EventArgs.Empty);
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
        playerToken = playerToken.Replace("\"", "");
        if (Interlocked.Read(ref isGameStarted) == 0)
            return;

        var player = players.FirstOrDefault(kvp => kvp.Value.Token == playerToken).Value;
        if (player == null)
        {
            throw new PlayerNotFoundException();
        }

        var cell = cells.FirstOrDefault(kvp => kvp.Value.OccupiedBy?.Token == playerToken).Value;

        var currentLocation = cell.Location;
        var newLocation = direction switch
        {
            Direction.Up => currentLocation with { Row = currentLocation.Row - 1 },
            Direction.Down => currentLocation with { Row = currentLocation.Row + 1 },
            Direction.Left => currentLocation with { Column = currentLocation.Column - 1 },
            Direction.Right => currentLocation with { Column = currentLocation.Column + 1 },
            _ => throw new DirectionNotRecognizedException()
        };

        if (isInBoard(newLocation) && cells[newLocation].OccupiedBy == null)
        {
            lock (lockObject)
            {
                var origDestinationCell = cells[newLocation];
                if (origDestinationCell.IsPillAvailable && pillValues.TryDequeue(out int pointValue))
                {
                    player.Score += pointValue;
                }
                var newDestinationCell = origDestinationCell with { OccupiedBy = player, IsPillAvailable = false };

                var origSourceCell = cells[currentLocation];
                var newSourceCell = origSourceCell with { OccupiedBy = null };

                log.LogInformation("Moving {playerName} from {oldLocation} to {newLocation} ({ateNewPill})", player.Name, currentLocation, newLocation, origDestinationCell.IsPillAvailable);

                cells.TryUpdate(newLocation, newDestinationCell, origDestinationCell);
                cells.TryUpdate(currentLocation, newSourceCell, origSourceCell);
            }

            GameStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool isInBoard(Location l) => l.Row >= 0 && l.Row < MaxRows && l.Column >= 0 && l.Column < MaxCols;
}
