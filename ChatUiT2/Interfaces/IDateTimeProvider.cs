namespace ChatUiT2.Interfaces;
public interface IDateTimeProvider
{
    public DateTime UtcNow { get; }
    public DateTime Now { get; }
    public DateTime Today { get; }
    public DateTimeOffset OffsetNow { get; }
    public DateTimeOffset OffsetUtcNow { get; }
}
