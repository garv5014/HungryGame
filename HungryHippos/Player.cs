namespace HungryHippos;

public class Player
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string Token { get; init; }
    public int Score { get; set; } = 0;
}
