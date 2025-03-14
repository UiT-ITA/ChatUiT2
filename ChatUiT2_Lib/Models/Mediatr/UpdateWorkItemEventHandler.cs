using ChatUiT2.Interfaces;
using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class UpdateWorkItemEventHandler : INotificationHandler<UpdateWorkItemEvent>
{
    private readonly IUserService _userService;

    public UpdateWorkItemEventHandler(IUserService userService)
    {
        _userService = userService;
    }

    public Task Handle(UpdateWorkItemEvent notification, CancellationToken cancellationToken)
    {
        if(notification.Chat != null)
        {
            _userService.UpdateWorkItem(notification.Chat);
        }
        return Task.CompletedTask;
    }
}
