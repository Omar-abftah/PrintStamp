namespace PrintStamp.Core;

public static class Retention
{
    public const int Months = 2;
    public static DateTime CutoffUtc() => DateTime.UtcNow.AddMonths(-Months);
}