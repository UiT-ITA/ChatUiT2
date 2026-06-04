using ChatUiT2.Messaging;

namespace ChatUiT2.Models.Mediatr;

public class UpdateWorkItemEvent : INotification
{
    public WorkItemChat? Chat { get; set; }
}
