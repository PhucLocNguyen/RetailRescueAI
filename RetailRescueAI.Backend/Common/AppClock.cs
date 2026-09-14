namespace RetailRescueAI.Backend.Common;

/// <summary>
/// Centralized application clock for RetailRescueAI.
/// All store operations for LifeMart Shinjuku run on Japan Standard Time (JST = UTC+9).
/// </summary>
public static class AppClock
{
    public static DateTime Now => DateTime.UtcNow.AddHours(9);
}

