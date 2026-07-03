using System.IO.Compression;
using System.Text.Json;
using Jellyfin.Plugin.AutoBackup.Configuration;
using Jellyfin.Server.Implementations.SystemBackupService;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.SystemBackupService;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AutoBackup.Services;

public class BackupBackgroundService : BackgroundService
{
    private readonly IBackupService _backupService;
    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<BackupBackgroundService> _logger;

    public BackupBackgroundService(
        IBackupService backupService,
        IApplicationPaths appPaths,
        ILoggerFactory loggerFactory)
    {
        _backupService = backupService;
        _appPaths      = appPaths;
        _logger        = loggerFactory.CreateLogger<BackupBackgroundService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AutoBackup] Background service started.");
        await RunLoopAsync(stoppingToken).ConfigureAwait(false);
        _logger.LogInformation("[AutoBackup] Background service stopped.");
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var secondsUntilNext = (15 - now.Minute % 15) * 60 - now.Second;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(secondsUntilNext), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunDueSchedulesAsync(cancellationToken);
        }

        _logger.LogInformation("[AutoBackup] Background service stopped.");
    }

    private async Task RunDueSchedulesAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
            return;

        var now       = DateTime.UtcNow;
        var slot      = new DateTime(now.Year, now.Month, now.Day, now.Hour,
                                     now.Minute / 15 * 15, 0, DateTimeKind.Utc);
        var localSlot = TimeZoneInfo.ConvertTimeFromUtc(slot, TimeZoneInfo.Local);

        var due = config.Schedules
            .Where(s => s.IsEnabled && IsDue(s, slot, localSlot))
            .ToList();

        if (due.Count == 0)
        {
            _logger.LogDebug("[AutoBackup] No schedules due at {Slot}.", slot);
            return;
        }

        _logger.LogInformation("[AutoBackup] {Count} schedule(s) due at {Slot}.", due.Count, slot);

        foreach (var schedule in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("[AutoBackup] Running schedule '{Label}'.", schedule.Label);

            try
            {
                var options = new BackupOptionsDto
                {
                    Database  = schedule.BackupDatabase,
                    Metadata  = schedule.BackupMetadata,
                    Subtitles = schedule.BackupSubtitles,
                    Trickplay = schedule.BackupTrickplay,
                };

                var result = await _backupService.CreateBackupAsync(options).ConfigureAwait(false);
                _logger.LogInformation("[AutoBackup] Backup complete: {Path}", result.Path);

                schedule.LastRunUtc = slot;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AutoBackup] Schedule '{Label}' failed.", schedule.Label);
            }
            finally
            {
                Plugin.Instance?.SaveConfiguration();
            }
        }

        ApplyRetentionPolicies(config.RetentionRules);
    }

    private void ApplyRetentionPolicies(List<RetentionRule> rules)
    {
        if (rules.Count == 0)
            return;

        var backupDir = Path.Combine(_appPaths.DataPath, "backups");
        if (!Directory.Exists(backupDir))
            return;

        var files = Directory.GetFiles(backupDir, "jellyfin-backup-*.zip");

        // Read options from manifest.json inside each zip
        var manifests = new List<(string Path, DateTime Created, bool Db, bool Meta, bool Subs, bool Tp)>();
        foreach (var file in files)
        {
            try
            {
                using var zip = ZipFile.OpenRead(file);
                var entry = zip.GetEntry("manifest.json");
                if (entry is null) continue;

                using var stream = entry.Open();
                var doc = JsonDocument.Parse(stream);
                var opts = doc.RootElement.GetProperty("Options");

                manifests.Add((
                    file,
                    File.GetCreationTimeUtc(file),
                    opts.GetProperty("Database").GetBoolean(),
                    opts.GetProperty("Metadata").GetBoolean(),
                    opts.GetProperty("Subtitles").GetBoolean(),
                    opts.GetProperty("Trickplay").GetBoolean()
                ));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AutoBackup] Could not read manifest from {File}", file);
            }
        }

        foreach (var rule in rules)
        {
            if (rule.KeepLastN <= 0)
                continue;

            var matching = manifests
                .Where(m => m.Db   == rule.BackupDatabase
                         && m.Meta == rule.BackupMetadata
                         && m.Subs == rule.BackupSubtitles
                         && m.Tp   == rule.BackupTrickplay)
                .OrderByDescending(m => m.Created)
                .ToList();

            foreach (var old in matching.Skip(rule.KeepLastN))
            {
                try
                {
                    File.Delete(old.Path);
                    _logger.LogInformation("[AutoBackup] Retention: deleted {File}", old.Path);
                    manifests.Remove(old);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AutoBackup] Retention: failed to delete {File}", old.Path);
                }
            }
        }
    }

    private static bool IsDue(BackupScheduleConfig s, DateTime slot, DateTime local)
    {
        var last        = s.LastRunUtc;
        var lastLocal   = last.ToLocalTime();
        var triggerTime = new TimeSpan(s.TriggerHour, s.TriggerMinute, 0);

        return s.ScheduleType switch
        {
            // Interval-based: compare elapsed UTC time, no clock-time involved
            ScheduleType.EveryNHours => slot >= last.AddHours(s.IntervalHours),

            // Clock-based: use local time for time-of-day and date comparisons
            ScheduleType.Daily => lastLocal.Date < local.Date
                                  && local.TimeOfDay >= triggerTime,

            ScheduleType.Weekly => slot >= last.AddDays(7)
                                   && local.DayOfWeek == s.WeekDay
                                   && local.TimeOfDay >= triggerTime,

            ScheduleType.EveryNWeeks => slot >= last.AddDays(7 * s.IntervalWeeks)
                                        && local.DayOfWeek == s.WeekDay
                                        && local.TimeOfDay >= triggerTime,

            ScheduleType.MonthlyOnDay => slot >= last.AddMonths(1)
                                         && local.Day == s.DayOfMonth
                                         && local.TimeOfDay >= triggerTime,

            ScheduleType.MonthlyOnWeekday => slot >= last.AddMonths(1)
                                              && local.DayOfWeek == s.WeekDay
                                              && IsCorrectWeekOfMonth(local, s.WeekOfMonth)
                                              && local.TimeOfDay >= triggerTime,
            _ => false
        };
    }

    private static bool IsCorrectWeekOfMonth(DateTime date, WeekOfMonth target)
    {
        if (target == WeekOfMonth.Last)
            return date.AddDays(7).Month != date.Month;

        return (date.Day - 1) / 7 + 1 == (int)target;
    }
}
