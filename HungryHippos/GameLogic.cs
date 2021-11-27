using System.Collections.Concurrent;

namespace HungryHippos;

public interface IRandomService
{
    public int Next(int maxValue);
}

public class SystemRandomService : IRandomService
{
    private Random random = new();
    public int Next(int maxValue) => random.Next(maxValue);
}

public class GameLogic
{
    private int number = 0;
    private readonly ConcurrentDictionary<int, Player> players = new();
    private readonly ConcurrentDictionary<Location, Cell> cells = new();
    private long isGameStarted = 0;
    private readonly IConfiguration config;
    private readonly ILogger<GameLogic> log;
    private readonly IRandomService random;
    private readonly object lockObject = new();
    public int MaxRows { get; private set; } = 0;
    public int MaxCols { get; private set; } = 0;
    private readonly ConcurrentQueue<int> pillValues = new();
    public event EventHandler GameStateChanged;

    public GameLogic(IConfiguration config, ILogger<GameLogic> log, IRandomService random)
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
            if(players.Count > MaxRows * MaxCols)
            {
                throw new TooManyPlayersToStartGameException("too many players");
            }

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
                Location newLocation = new Location(random.Next(MaxRows), random.Next(MaxCols));
                bool addToRowIfConflict = true;
                while (cells[newLocation].OccupiedBy != null)
                {
                    var newRow = newLocation.Row;
                    var newCol = newLocation.Column;
                    if (addToRowIfConflict)
                        newRow++;
                    else
                        newCol++;

                    if (newRow >= MaxRows)
                        newRow = 0;

                    if (newCol >= MaxCols)
                        newCol = 0;

                    newLocation = new Location(newRow, newCol);
                    addToRowIfConflict = !addToRowIfConflict;
                }
                cells[newLocation] = cells[newLocation] with { OccupiedBy = p.Value };
                p.Value.Score = 0;
            }

            pillValues.Clear();
            for (int i = 1; i <= MaxRows * MaxCols; i++)
            {
                pillValues.Enqueue(i);
            }

            Interlocked.Increment(ref isGameStarted);
        }

        GameStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetGame(string secretCode)
    {
        if (secretCode != config["SECRET_CODE"] || Interlocked.Read(ref isGameStarted) == 0)
        {
            return;
        }

        Interlocked.Exchange(ref isGameStarted, 0);
        lock (lockObject)
        {
            foreach (var p in players)
            {
                p.Value.Score = 0;
            }
        }
        GameStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Cell GetCell(int row, int col) => cells[new Location(row, col)];

    public string JoinPlayer(string playerName)
    {
        var token = Guid.NewGuid().ToString();
        log.LogInformation("{playerName} wants to join (will be {token})", playerName, token);

        lock (lockObject)
        {
            var id = Interlocked.Increment(ref number);
            log.LogDebug("Got lock; new user will be ID# {id}", id);

            var joinedPlayer = players.AddOrUpdate(id, new Player { Id = id, Name = playerName, Token = token }, (key, value) => value);

            if (gameAlreadyInProgress)
            {
                var availableSpaces = cells.Where(c => c.Value.OccupiedBy == null).ToList();
                if(availableSpaces.Any() is false)
                {
                    throw new NoAvailableSpaceException("there is no available space");
                }
                var randomIndex = random.Next(availableSpaces.Count);
                var newLocation = availableSpaces[randomIndex].Key;
                var origCell = cells[newLocation];
                var newCell = origCell with { OccupiedBy = joinedPlayer, IsPillAvailable = false };
                cells.TryUpdate(newLocation, newCell, origCell);
            }

            GameStateChanged?.Invoke(this, EventArgs.Empty);
        }
        return token;
    }

    private bool gameAlreadyInProgress => Interlocked.Read(ref isGameStarted) != 0;

    public IEnumerable<Player> GetPlayersByScoreDescending() =>
        players.Select(p => p.Value)
            .OrderByDescending(s => s.Score);

    public MoveResult Move(string playerToken, Direction direction)
    {
        playerToken = playerToken.Replace("\"", "");
        if (Interlocked.Read(ref isGameStarted) == 0)
            return null;

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
            bool ateAPill = false;
            lock (lockObject)
            {
                var origDestinationCell = cells[newLocation];
                if (origDestinationCell.IsPillAvailable && pillValues.TryDequeue(out int pointValue))
                {
                    player.Score += pointValue;
                    ateAPill = true;
                }
                var newDestinationCell = origDestinationCell with { OccupiedBy = player, IsPillAvailable = false };

                var origSourceCell = cells[currentLocation];
                var newSourceCell = origSourceCell with { OccupiedBy = null };

                log.LogInformation("Moving {playerName} from {oldLocation} to {newLocation} ({ateNewPill})", player.Name, currentLocation, newLocation, origDestinationCell.IsPillAvailable);

                cells.TryUpdate(newLocation, newDestinationCell, origDestinationCell);
                cells.TryUpdate(currentLocation, newSourceCell, origSourceCell);
            }

            GameStateChanged?.Invoke(this, EventArgs.Empty);
            return new MoveResult(newLocation, ateAPill);
        }
        else
        {
            return null;
        }
    }

    private bool isInBoard(Location l) => l.Row >= 0 && l.Row < MaxRows && l.Column >= 0 && l.Column < MaxCols;
}
