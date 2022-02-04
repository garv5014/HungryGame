namespace foolhearty;

public interface IPlayerLogic
{
    Task PlayAsync(CancellationTokenSource cancellationTokenSource);
    Task JoinGameAsync();
}
