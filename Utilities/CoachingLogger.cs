namespace OptishotV1DOTNET.Utilities;

/// <summary>
/// Writes coaching events to a plain text log file in the app's data directory.
/// Format: header on session start, one line per event, footer on session end.
/// File location on Android: /data/data/com.optishot.v1dotnet/files/coaching_log.txt
/// </summary>
/// 
/*
 C:\adb\platform-tools-latest-windows\platform-tools\adb -s adb-jb9tib6tbuqot4if-5gCFtV._adb-tls-connect._tcp shell run-as com.optishot.v1dotnet cat files/coaching_log.txt
*/
public static class CoachingLogger
{
    private static string LogPath =>
        Path.Combine(FileSystem.AppDataDirectory, "coaching_log.txt");

    private static readonly object _lock = new();

    /// <summary>Call when the camera session starts.</summary>
    public static void SessionStarted()
    {
        var header = $"log {DateTime.Now:dd/MM/yyyy HH:mm:ss}----------\n";
        lock (_lock)
            File.AppendAllText(LogPath, header);
    }

    /// <summary>Call when a coaching tip is shown to the user.</summary>
    public static void TipShown(string category, string message)
    {
        var line = $"  [{DateTime.Now:HH:mm:ss}] [{category}] {message}\n";
        lock (_lock)
            File.AppendAllText(LogPath, line);
    }

    /// <summary>Call when a lighting condition changes (even if no tip is shown).</summary>
    public static void LightingChanged(string condition, float mean, float stdDev)
    {
        var line = $"  [{DateTime.Now:HH:mm:ss}] [LIGHT] {condition} (mean={mean:F2}, stdDev={stdDev:F2})\n";
        lock (_lock)
            File.AppendAllText(LogPath, line);
    }

    /// <summary>Call when the camera session ends.</summary>
    public static void SessionEnded()
    {
        var footer = $"----------------\n\n";
        lock (_lock)
            File.AppendAllText(LogPath, footer);
    }

    /// <summary>Returns the full log file path so you can share it.</summary>
    public static string GetLogPath() => LogPath;

    // Para puxar o log para o PC (correr no terminal com adb ligado):
    // 
}
