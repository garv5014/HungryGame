using Prometheus;

namespace HungryGame;

public class Counters
{
    public Counter TotalMovesPerPlayer { get; set; } = Metrics.CreateCounter("moves_per_player_total", "Total number of moves per player", new CounterConfiguration()
    {
        LabelNames = new[] { "player" }
    });

    public Counter TotalScorePerPlayer { get; set; } = Metrics.CreateCounter("score_per_player_total", "Total score per player", new CounterConfiguration()
    {
        LabelNames = new[] { "player" }
    });

    public Counter TotalPillsEatenPerPlayer { get; set; } = Metrics.CreateCounter("pills_eaten_per_player_total", "Total number of pills eaten per player", new CounterConfiguration()
    {
        LabelNames = new[] { "player" }
    });

    public Counter TotalAttacksPerPlayer { get; set; } = Metrics.CreateCounter("attacks_per_player_total", "Total number of attacks per player", new CounterConfiguration()
    {
        LabelNames = new[] { "player" }
    });

    public Counter TotalHorizontalMovesPerPlayer { get; set; } = Metrics.CreateCounter("horizontal_moves_per_player_total", "Total number of horizontal moves per player", new CounterConfiguration()
    {
        LabelNames = new[] { "player" }
    });

    public Counter TotalVerticalMovesPerPlayer { get; set; } = Metrics.CreateCounter("vertical_moves_per_player_total", "Total number of vertical moves per player", new CounterConfiguration()
    {
        LabelNames = new[] { "player" }
    });

    public Counter TotalAttacksPerGame { get; set; } = Metrics.CreateCounter("attacks_per_game_total", "Total number attacks per game");

    public Counter TotalKillsPerPlayer { get; set; } = Metrics.CreateCounter("kills_per_player_total", "Total number of kills per player", new CounterConfiguration()
    {
        LabelNames = new[] { "player" }
    });
}
