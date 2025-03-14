using ChatUiT2.Interfaces;
using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class StreamUpdatedEventHandler : INotificationHandler<StreamUpdatedEvent>
{
    private readonly IUserService _userService;

    public StreamUpdatedEventHandler(IUserService userService)
    {
        _userService = userService;
    }

    public Task Handle(StreamUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _userService.StreamUpdated();
        return Task.CompletedTask;
    }
}
