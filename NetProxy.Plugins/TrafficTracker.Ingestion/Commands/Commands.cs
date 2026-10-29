namespace NetProxy.TrafficTracker.Ingestion.Commands
{
    /// <summary>
    /// A command: a request to change something, which is handled by exactly one handler. What the ingestion service does with a message
    /// from the queue is a command, so that receiving a message (which is about the queue) is kept apart from changing the database (which
    /// is about the schema).
    /// </summary>
    public interface ICommand
    {
    }

    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        Task HandleAsync(TCommand command, CancellationToken cancellationToken);
    }

    public interface ICommandDispatcher
    {
        Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;
    }

    internal sealed class CommandDispatcher(IServiceProvider services) : ICommandDispatcher
    {
        public Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
            => services.GetRequiredService<ICommandHandler<TCommand>>().HandleAsync(command, cancellationToken);
    }
}
