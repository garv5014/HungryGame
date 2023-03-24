﻿using System.Security.Cryptography.X509Certificates;

namespace HungryGame;

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
    private readonly List<Player> players = new();
    private readonly Dictionary<Location, Cell> cells = new();
    private readonly Queue<int> pillValues = new();
    private readonly Dictionary<Location, int> specialPointValues = new();
    private readonly List<Player> playersThatMovedThisGame = new();

    private int number = 0;
    private long gameStateValue = 0;
    private readonly IConfiguration config;
    private readonly ILogger<GameLogic> log;
    private readonly IRandomService random;
    private readonly Counters _counters;

    public int MaxRows { get; private set; } = 0;
    public int MaxCols { get; private set; } = 0;
    public event EventHandler? GameStateChanged;

    public GameLogic(IConfiguration config, ILogger<GameLogic> log, IRandomService random, Counters counters)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.log = log;
        this.random = random;
        _counters = counters;
    }

    public DateTime lastStateChange;
    public TimeSpan stateChangeFrequency;

    private void raiseStateChange()
    {
        if (lastStateChange + stateChangeFrequency < DateTime.Now)
        {
            GameStateChanged?.Invoke(this, EventArgs.Empty);
            lastStateChange = DateTime.Now;
        }
    }

    public bool IsGameStarted => Interlocked.Read(ref gameStateValue) != 0;
    public GameState CurrentGameState => (GameState)Interlocked.Read(ref gameStateValue);
    public bool IsGameOver => Interlocked.Read(ref gameStateValue) == 3;
    public DateTime? GameEndsOn { get; private set; }
    public TimeSpan? TimeLimit { get; private set; }
    public TimeSpan? TimeRemaining => GameEndsOn.HasValue ? GameEndsOn.Value - DateTime.Now : null;
    private Timer gameTimer;

    public void StartGame(NewGameInfo gameInfo)
    {
        if (gameInfo.SecretCode != config["SECRET_CODE"] || Interlocked.Read(ref gameStateValue) != 0)
        {
            return;
        }

        MaxRows = gameInfo.NumRows;
        MaxCols = gameInfo.NumColumns;

        if (gameInfo.IsTimed && gameInfo.TimeLimitInMinutes.HasValue)
        {
            var minutes = gameInfo.TimeLimitInMinutes.Value;
            TimeLimit = TimeSpan.FromMinutes(minutes);
            GameEndsOn = DateTime.Now.Add(TimeLimit.Value);
            gameTimer = new Timer(gameOverCallback, null, TimeLimit.Value, Timeout.InfiniteTimeSpan);
        }

        initializeGame();
    }

    private void gameOverCallback(object? state)
    {
        log.LogInformation($"Timer ran out.  Game over.");
        Interlocked.Exchange(ref gameStateValue, 3);

        Thread.Sleep(TimeSpan.FromSeconds(5));

        resetGame();
        if (TimeLimit.HasValue)
        {
            GameEndsOn = DateTime.Now.Add(TimeLimit.Value);
            gameTimer = new Timer(gameOverCallback, null, TimeLimit.Value, Timeout.InfiniteTimeSpan);
        }


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

            var playersThatNeverMoved = players.Except(playersThatMovedThisGame);
            if (playersThatNeverMoved.Any())
            {
                players.RemoveAll(p => playersThatNeverMoved.Contains(p));
            }
            playersThatMovedThisGame.Clear();

            cells.Clear();
            foreach (var location in from r in Enumerable.Range(0, MaxRows)
                                     from c in Enumerable.Range(0, MaxCols)
                                     select new Location(r, c))
            {
                cells.TryAdd(location, new Cell(location, true, null));
            }

            foreach (var player in players)
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

            if (players.Count > 20 || pillValues.Count > 10_000)
                stateChangeFrequency = TimeSpan.FromMilliseconds(750);
            else
                stateChangeFrequency = TimeSpan.FromMilliseconds(250);

            Interlocked.Increment(ref gameStateValue);
        }

        raiseStateChange();
    }

    public void ResetGame(string secretCode)
    {
        if (secretCode != config["SECRET_CODE"] || Interlocked.Read(ref gameStateValue) == 0)
        {
            return;
        }

        resetGame();
    }

    private void resetGame()
    {
        Interlocked.Exchange(ref gameStateValue, 0);

        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
        {
            foreach (var p in players)
            {
                p.Score = 0;
            }
        }

        raiseStateChange();
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
            players.Add(joinedPlayer);

            markPlayerAsActive(joinedPlayer);

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

        raiseStateChange();
        return token;
    }

    private bool gameAlreadyInProgress => Interlocked.Read(ref gameStateValue) != 0;

    public IEnumerable<Player> GetPlayersByScoreDescending() =>
        players.OrderByDescending(p => p.Score);

    public MoveResult? Move(string playerToken, Direction direction)
    {
        if (string.IsNullOrWhiteSpace(playerToken))
            throw new ArgumentNullException(nameof(playerToken));

        playerToken = playerToken.Replace("\"", "");

        Player player;
        Cell cell;
        MoveResult moveResult;

        lock (lockForPlayersCellsPillValuesAndSpecialPontValues)
        {
            player = players.FirstOrDefault(p => p.Token == playerToken);
            cell = cells.FirstOrDefault(kvp => kvp.Value.OccupiedBy?.Token == playerToken).Value;

            if (player == null)
            {
                throw new PlayerNotFoundException();
            }

            markPlayerAsActive(player);

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

            void UpdateHorizontalCounter()
            {
                _counters.TotalHorizontalMovesPerPlayer.WithLabels(player.Token).Inc();
            }

            void UpdateVerticalCounter()
            {
                _counters.TotalVerticalMovesPerPlayer.WithLabels(player.Token).Inc();
            }
            Location newLocation;
            switch (direction)
            {
                case Direction.Up:
                    newLocation = currentLocation with { Row = currentLocation.Row - 1 };
                    UpdateHorizontalCounter();
                    break;
                case Direction.Down:
                    newLocation = currentLocation with { Row = currentLocation.Row + 1 };
                    UpdateHorizontalCounter();
                    break;
                case Direction.Left:
                    newLocation = currentLocation with { Column = currentLocation.Column - 1 };
                    UpdateVerticalCounter();
                    break;
                case Direction.Right:
                    newLocation = currentLocation with { Column = currentLocation.Column + 1 };
                    UpdateVerticalCounter();
                    break;
                default:
                    throw new DirectionNotRecognizedException();
            };

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
        raiseStateChange();
        return moveResult;
    }

    private void markPlayerAsActive(Player player)
    {
        if (!playersThatMovedThisGame.Contains(player))
            playersThatMovedThisGame.Add(player);
    }

    private MoveResult movePlayer(Player player, Location currentLocation, Location newLocation)
    {
        bool ateAPill = false;
        var origDestinationCell = cells[newLocation];

        if (origDestinationCell.IsPillAvailable)
        {
            var scoreIncrement = getPointValue(newLocation);
            player.Score += scoreIncrement;
            _counters.TotalScorePerPlayer.WithLabels(player.Token).Inc(scoreIncrement);
            _counters.TotalPillsEatenPerPlayer.WithLabels(player.Token).Inc();
            ateAPill = true;
        }
        var newDestinationCell = origDestinationCell with { OccupiedBy = player, IsPillAvailable = false };

        var origSourceCell = cells[currentLocation];
        var newSourceCell = origSourceCell with { OccupiedBy = null };
        _counters.TotalMovesPerPlayer.WithLabels(player.Token).Inc();
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

        _counters.TotalAttacksPerPlayer.WithLabels(currentPlayer.Token).Inc();
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
            var playerCount = cells.Count(c => c.Value.OccupiedBy != null);
            if (playerCount <= 1)
            {
                Interlocked.Exchange(ref gameStateValue, 3);//game over
                log.LogInformation("Only 1 player left, not going to battle mode - game over.");
            }
            else
            {
                Interlocked.Increment(ref gameStateValue);
                log.LogInformation("No more pills available, changing game state to {gameState}", CurrentGameState);
            }
        }
    }
}
