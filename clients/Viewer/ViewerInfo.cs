namespace Viewer;

public class ViewerInfo
{
    public bool IsGameStarted { get; private set; }
    public string CurrentGameState { get; private set; }
    public DateTime? GameEndsOn { get; private set; }
    public TimeSpan TimeRemaining => (GameEndsOn ?? DateTime.Now) - DateTime.Now;
    public int MaxRows { get; private set; }
    public int MaxColumns { get; private set; }
    public Cell GetCell(int row, int col)
    {
        throw new NotImplementedException();
    }

    public List<PlayerInfo> GetPlayersByScoreDescending()
    {
        throw new NotImplementedException();
    }
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