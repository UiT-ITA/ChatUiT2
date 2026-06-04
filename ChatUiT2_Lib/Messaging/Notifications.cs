using Microsoft.Extensions.DependencyInjection;

namespace ChatUiT2.Messaging;

/// <summary>
/// Marker interface for an in-process notification (publish/subscribe message).
/// </summary>
public interface INotification { }

/// <summary>
/// Handles a published <typeparamref name="TNotification"/>. Handlers are resolved
/// from the current DI scope at publish time, which keeps the publishing service
/// decoupled from the handler's dependencies (e.g. breaking the
/// UserService &lt;-&gt; ChatService circular dependency).
/// </summary>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes notifications to all registered handlers. Lightweight in-house
/// replacement for MediatR's publish pipeline (notifications were the only
/// MediatR feature in use).
/// </summary>
public interface IPublisher
{
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

/// <inheritdoc />
public class Publisher : IPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public Publisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        foreach (var handler in _serviceProvider.GetServices<INotificationHandler<TNotification>>())
        {
            await handler.Handle(notification, cancellationToken);
        }
    }
}
