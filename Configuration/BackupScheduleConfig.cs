namespace Jellyfin.Plugin.AutoBackup.Configuration;

public class BackupScheduleConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "Schedule";
    public bool IsEnabled { get; set; } = true;

    public bool BackupDatabase { get; set; } = true;
    public bool BackupMetadata { get; set; } = false;
    public bool BackupSubtitles { get; set; } = false;
    public bool BackupTrickplay { get; set; } = false;

    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;

    /// <summary>Used when ScheduleType is EveryNHours.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Used when ScheduleType is EveryNWeeks.</summary>
    public int IntervalWeeks { get; set; } = 1;

    /// <summary>Used when ScheduleType is Weekly, EveryNWeeks, or MonthlyOnWeekday.</summary>
    public DayOfWeek WeekDay { get; set; } = DayOfWeek.Sunday;

    /// <summary>Used when ScheduleType is MonthlyOnDay (1–28).</summary>
    public int DayOfMonth { get; set; } = 1;

    /// <summary>Used when ScheduleType is MonthlyOnWeekday.</summary>
    public WeekOfMonth WeekOfMonth { get; set; } = WeekOfMonth.First;

    /// <summary>Hour of day (0–23) for the trigger.</summary>
    public int TriggerHour { get; set; } = 2;

    /// <summary>Minute of hour — only 0, 15, 30 or 45 are valid (matches 15-min master task tick).</summary>
    public int TriggerMinute { get; set; } = 0;

    /// <summary>Persisted after each successful run to calculate next due time.</summary>
    public DateTime LastRunUtc { get; set; } = DateTime.MinValue;
}
