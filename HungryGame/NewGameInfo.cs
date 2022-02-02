namespace HungryGame;
public class NewGameInfo
{
    public int NumRows { get; set; } = 15;
    public int NumColumns { get; set; } = 15;
    public string SecretCode { get; set; } = string.Empty;
    public string CellIcon { get; set; } = "🌯";
    public bool IsTimed { get; set; }
    public int? TimeLimitInMinutes { get; set; }
}
