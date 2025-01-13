namespace UiT.ChatUiT2.MaintenanceFunctions.Tools;

/// <summary>
/// Tools methods for debugging
/// </summary>
public static class DebugTools
{
    /// <summary>
    /// IsDebug is true if the code is running in debug mode
    /// </summary>
#if DEBUG
    public const bool IsDebug = true;
#else
    public const bool IsDebug = false;
#endif
}
