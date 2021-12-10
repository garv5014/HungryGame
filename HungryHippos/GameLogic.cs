using System.Collections.Concurrent;

namespace HungryHippos;

public enum GameState : int
{
    Joining = 0,
    Eating = 1,
    Battle = 2,
    GameOver = 3
}

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
    private long gameStateValue = 0;
    private readonly IConfiguration config;
    private readonly ILogger<GameLogic> log;
    private readonly IRandomService random;
    private readonly object lockObject = new();
    public int MaxRows { get; private set; } = 0;
    public int MaxCols { get; private set; } = 0;
    private readonly ConcurrentQueue<int> pillValues = new();
    public event EventHandler? GameStateChanged;

    public GameLogic(IConfiguration config, ILogger<GameLogic> log, IRandomService random)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.log = log;
        this.random = random;
    }

    public bool IsGameStarted => Interlocked.Read(ref gameStateValue) != 0;
    public GameState CurrentGameState => (GameState)Interlocked.Read(ref gameStateValue);
    public bool IsGameOver => Interlocked.Read(ref gameStateValue) == 3;

    public void StartGame(int numRows, int numColumns, string secretCode)
    {
        if (secretCode != config["SECRET_CODE"] || Interlocked.Read(ref gameStateValue) != 0)
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
            if (players.Count > MaxRows * MaxCols)
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
                cells[newLocation] = cells[newLocation] with { OccupiedBy = p.Value, IsPillAvailable = false };
                p.Value.Score = 0;
            }

            pillValues.Clear();
            for (int i = 1; i <= MaxRows * MaxCols; i++)
            {
                pillValues.Enqueue(i);
            }

            Interlocked.Increment(ref gameStateValue);
        }

        GameStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetGame(string secretCode)
    {
        if (secretCode != config["SECRET_CODE"] || Interlocked.Read(ref gameStateValue) == 0)
        {
            return;
        }

        Interlocked.Exchange(ref gameStateValue, 0);
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
                if (availableSpaces.Any() is false)
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

    private bool gameAlreadyInProgress => Interlocked.Read(ref gameStateValue) != 0;

    public IEnumerable<Player> GetPlayersByScoreDescending() =>
        players.Select(p => p.Value)
            .OrderByDescending(s => s.Score);

    public MoveResult Move(string playerToken, Direction direction)
    {
        playerToken = playerToken.Replace("\"", "");

        if (CurrentGameState != GameState.Eating && CurrentGameState != GameState.Battle)
            return null;

        var player = players.FirstOrDefault(kvp => kvp.Value.Token == playerToken).Value;
        if (player == null)
        {
            throw new PlayerNotFoundException();
        }

        var cell = cells.FirstOrDefault(kvp => kvp.Value.OccupiedBy?.Token == playerToken).Value;
        if(cell == null)
        {
            return null;
        }

        var currentLocation = cell.Location;
        var newLocation = direction switch
        {
            Direction.Up => currentLocation with { Row = currentLocation.Row - 1 },
            Direction.Down => currentLocation with { Row = currentLocation.Row + 1 },
            Direction.Left => currentLocation with { Column = currentLocation.Column - 1 },
            Direction.Right => currentLocation with { Column = currentLocation.Column + 1 },
            _ => throw new DirectionNotRecognizedException()
        };

        if (isInBoard(newLocation))
        {
            lock (lockObject)
            {
                if (cells[newLocation].OccupiedBy == null)
                {
                    bool ateAPill = false;
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

                    changeToBattleModeIfNoMorePillsAvailable();

                    GameStateChanged?.Invoke(this, EventArgs.Empty);
                    return new MoveResult(newLocation, ateAPill);
                }
                else if (CurrentGameState == GameState.Battle)
                {
                    //decrease the health of both players by the min health of the players
                    var otherPlayer = cells[newLocation].OccupiedBy;
                    var currentPlayer = cell.OccupiedBy;

                    var minHealth = Math.Min(currentPlayer.Score, otherPlayer.Score);
                    log.LogInformation("Player {currentPlayer} attacking {otherPlayer}", currentPlayer, otherPlayer);

                    currentPlayer.Score -= minHealth;
                    otherPlayer.Score -= minHealth;
                    log.LogInformation("new scores: {currentPlayerScore}, {otherPlayerScore}", currentPlayer.Score, otherPlayer.Score);

                    if (removePlayerIfDead(currentPlayer) || removePlayerIfDead(otherPlayer))
                    {
                        checkForWinner();
                    }

                    GameStateChanged?.Invoke(this, EventArgs.Empty);
                    return new MoveResult(currentLocation, false);
                }
            }
        }

        return null;
    }

    private void checkForWinner()
    {
        int activePlayers = cells.Count(c => c.Value.OccupiedBy != null);
        log.LogInformation("checking for winner: {activePlayers} active players", activePlayers);

        if (activePlayers == 1)
        {
            log.LogInformation("Changing game state from {currentGameState} to {newGameState}", CurrentGameState, (CurrentGameState + 1));
            Interlocked.Increment(ref gameStateValue);
        }
    }

    private bool removePlayerIfDead(Player player)
    {
        if (player == null || player.Score > 0)
            return false;

        log.LogInformation("Removing player from board: {player}", player);
        var origCell = cells.FirstOrDefault(c => c.Value.OccupiedBy == player);
        var updatedCell = origCell.Value with { OccupiedBy = null };
        cells.TryUpdate(origCell.Key, updatedCell, origCell.Value);
        return true;
    }

    public IEnumerable<Cell> GetBoardState() => cells.Values;

    private void changeToBattleModeIfNoMorePillsAvailable()
    {
        if (CurrentGameState != GameState.Eating)
            return;

        var remainingPills = cells.Count(c => c.Value.IsPillAvailable);
        if (remainingPills == 0)
        {
            Interlocked.Increment(ref gameStateValue);
            log.LogInformation("No more pills available, changing game state to {gameState}", CurrentGameState);
        }
    }

    private bool isInBoard(Location l) => l.Row >= 0 && l.Row < MaxRows && l.Column >= 0 && l.Column < MaxCols;
}
