namespace HungryHippos;

public record MoveRequest(string Direction, string UserToken);
public record Cell(Location Location, bool IsPillAvailable, Player? OccupiedBy);
public record Location(int Row, int Column);
public enum Direction { Up, Down, Left, Right };