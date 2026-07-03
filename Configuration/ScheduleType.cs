namespace Jellyfin.Plugin.AutoBackup.Configuration;

public enum ScheduleType
{
    EveryNHours,
    Daily,
    Weekly,
    EveryNWeeks,
    MonthlyOnDay,
    MonthlyOnWeekday,
}

public enum WeekOfMonth
{
    First  = 1,
    Second = 2,
    Third  = 3,
    Fourth = 4,
    Last   = -1,
}
