namespace ChatUiT2.Tools;

public static class DateTimeTools
{
    public static DateTime ClearMilliseconds(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Kind);
    }

    public static DateTime GetTimestamp()
    {
        return DateTime.UtcNow.ClearMilliseconds();
    }
}
