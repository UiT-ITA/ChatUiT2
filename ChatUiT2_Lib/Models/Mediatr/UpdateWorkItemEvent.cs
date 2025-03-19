using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class UpdateWorkItemEvent : INotification
{
    public WorkItemChat? Chat { get; set; }
}
