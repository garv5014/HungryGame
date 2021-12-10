namespace HungryHippos;

public class Player
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Token { get; init; }
    public int Score { get; set; } = 0;
}

public class RedactedPlayer
{
    public RedactedPlayer(Player p)
    {
        Id = p.Id;
        Name = p.Name;
        Score = p.Score;
    }

    public int Id { get; }
    public string? Name { get; }
    public int Score { get; }
}