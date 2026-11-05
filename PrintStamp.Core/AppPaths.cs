namespace PrintStamp.Core;

public class AppPaths
{
    // PRINTSTAMP_HOME overrides everything (handy for testing)
    public static string Root =>
        Environment.GetEnvironmentVariable("PRINTSTAMP_HOME")
        ?? (OperatingSystem.IsWindows()
            ? @"C:\PrintStamp"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrintStamp"));

    public static string Queue => Path.Combine(Root, "queue");
    public static string Done  => Path.Combine(Root, "done");
    public static string Db    => Path.Combine(Root, "data", "printstamp.db");
    public static string Work   => Path.Combine(Root, "work");
    public static string Failed => Path.Combine(Root, "failed");
    public static string Printed => Path.Combine(Root, "printed");
}