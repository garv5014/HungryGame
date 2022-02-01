namespace HungryHippos;

public enum GameState
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
    private readonly Random random = new();
    public int Next(int maxValue) => random.Next(maxValue);
}

public class GameLogic
{
    private readonly object lockForPlayersCellsPillValuesAndSpecialPontValues = new();
    private readonly Dictionary<int, Player> players = new();
    private readonly Dictionary<Location, Cell> cells = new();
    private readonly Queue<int> pillValues = new();
    private readonly Dictionary<Location, int> specialPointValues = new();

    private int number = 0;
    private long gameStateValue = 0;
    private readonly IConfiguration config;
    private readonly ILogger<GameLogic> log;
    private readonly IRandomService random;

    public int MaxRows { get; private set; } = 0;
    public int MaxCols { get; private set; } = 0;
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
        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
        {
            if (players.Count > MaxRows * MaxCols)
            {
                throw new TooManyPlayersToStartGameException("too many players");
            }

            cells.Clear();
            foreach (var location in from r in Enumerable.Range(0, MaxRows)
                                     from c in Enumerable.Range(0, MaxCols)
                                     select new Location(r, c))
            {
                cells.TryAdd(location, new Cell(location, true, null));
            }

            foreach (var player in players.Select(i => i.Value))
            {
                var newLocation = new Location(random.Next(MaxRows), random.Next(MaxCols));
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
                cells[newLocation] = cells[newLocation] with { OccupiedBy = player, IsPillAvailable = false };
                player.Score = 0;
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
        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
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

        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
        {
            var id = Interlocked.Increment(ref number);
            log.LogDebug("Got lock; new user will be ID# {id}", id);

            var joinedPlayer = new Player { Id = id, Name = playerName, Token = token };
            players.TryAdd(id, joinedPlayer);

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
                cells[newLocation] = newCell;
            }
        }

        GameStateChanged?.Invoke(this, EventArgs.Empty);
        return token;
    }

    private bool gameAlreadyInProgress => Interlocked.Read(ref gameStateValue) != 0;

    public IEnumerable<Player> GetPlayersByScoreDescending() =>
        players.Select(p => p.Value)
            .OrderByDescending(s => s.Score);

    public MoveResult? Move(string playerToken, Direction direction)
    {
        if (string.IsNullOrWhiteSpace(playerToken))
            throw new ArgumentNullException(nameof(playerToken));

        playerToken = playerToken.Replace("\"", "");

        Player player;
        Cell cell;
        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
        {
            player = players.FirstOrDefault(kvp => kvp.Value.Token == playerToken).Value;
            cell = cells.FirstOrDefault(kvp => kvp.Value.OccupiedBy?.Token == playerToken).Value;
        }

        if (player == null)
        {
            throw new PlayerNotFoundException();
        }

        var currentPlayer = cell?.OccupiedBy;
        if (cell == null || currentPlayer == null)
        {
            throw new InvalidMoveException("Player is not currently on the board");
        }

        var currentLocation = cell.Location;

        if (CurrentGameState != GameState.Eating && CurrentGameState != GameState.Battle)
        {
            return new MoveResult(currentLocation, false);
        }

        var newLocation = direction switch
        {
            Direction.Up => currentLocation with { Row = currentLocation.Row - 1 },
            Direction.Down => currentLocation with { Row = currentLocation.Row + 1 },
            Direction.Left => currentLocation with { Column = currentLocation.Column - 1 },
            Direction.Right => currentLocation with { Column = currentLocation.Column + 1 },
            _ => throw new DirectionNotRecognizedException()
        };

        MoveResult moveResult;
        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
        {
            if (!cells.ContainsKey(newLocation))
            {
                moveResult = new MoveResult(currentLocation, false);
            }
            else
            {
                Player? otherPlayer = cells[newLocation].OccupiedBy;
                if (otherPlayer == null)
                {
                    moveResult = movePlayer(player, currentLocation, newLocation);
                }
                else if (CurrentGameState == GameState.Battle)
                {
                    moveResult = attack(currentPlayer, currentLocation, newLocation, otherPlayer);
                }
                else
                {
                    moveResult = new MoveResult(currentLocation, false);
                }
            }
        }
        GameStateChanged?.Invoke(this, EventArgs.Empty);
        return moveResult;
    }

    private MoveResult movePlayer(Player player, Location currentLocation, Location newLocation)
    {
        bool ateAPill = false;
        var origDestinationCell = cells[newLocation];
        if (origDestinationCell.IsPillAvailable)
        {
            player.Score += getPointValue(newLocation);
            ateAPill = true;
        }
        var newDestinationCell = origDestinationCell with { OccupiedBy = player, IsPillAvailable = false };

        var origSourceCell = cells[currentLocation];
        var newSourceCell = origSourceCell with { OccupiedBy = null };

        log.LogInformation("Moving {playerName} from {oldLocation} to {newLocation} ({ateNewPill})", player.Name, currentLocation, newLocation, origDestinationCell.IsPillAvailable);

        cells[newLocation] = newDestinationCell;
        cells[currentLocation] = newSourceCell;

        changeToBattleModeIfNoMorePillsAvailable();

        return new MoveResult(newLocation, ateAPill);
    }

    private MoveResult attack(Player currentPlayer, Location currentLocation, Location newLocation, Player otherPlayer)
    {
        //decrease the health of both players by the min health of the players
        var minHealth = Math.Min(currentPlayer.Score, otherPlayer.Score);
        log.LogInformation("Player {currentPlayer} attacking {otherPlayer}", currentPlayer, otherPlayer);

        currentPlayer.Score -= minHealth;
        otherPlayer.Score -= minHealth;
        log.LogInformation("new scores: {currentPlayerScore}, {otherPlayerScore}", currentPlayer.Score, otherPlayer.Score);

        if (removePlayerIfDead(currentPlayer) || removePlayerIfDead(otherPlayer))
        {
            specialPointValues.TryAdd(newLocation, (int)Math.Round(minHealth / 2.0, 0));
            checkForWinner();
        }

        return new MoveResult(currentLocation, false);
    }

    private int getPointValue(Location newLocation)
    {
        int pointValue = 0;

        if (specialPointValues.ContainsKey(newLocation))
        {
            pointValue = specialPointValues[newLocation];
            specialPointValues.Remove(newLocation);
        }
        else
        {
            pointValue = pillValues.Dequeue();
        }

        return pointValue;
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
        var updatedCell = origCell.Value with { OccupiedBy = null, IsPillAvailable = true };
        cells[origCell.Key] = updatedCell;
        return true;
    }

    public IEnumerable<RedactedCell> GetBoardState()
    {
        if (CurrentGameState == GameState.Joining)
            return new RedactedCell[] { };

        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
        {
            return cells.Values.Select(c => new RedactedCell(c));
        }
    }

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
}
