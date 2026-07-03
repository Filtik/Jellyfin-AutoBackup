using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AutoBackup.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool IsInitialized { get; set; } = false;
    public List<BackupScheduleConfig> Schedules { get; set; } = [];
    public List<RetentionRule> RetentionRules { get; set; } = [];
}
