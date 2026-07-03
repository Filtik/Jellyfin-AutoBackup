namespace Jellyfin.Plugin.AutoBackup.Configuration;

public class RetentionRule
{
    public bool BackupDatabase  { get; set; } = true;
    public bool BackupMetadata  { get; set; } = false;
    public bool BackupSubtitles { get; set; } = false;
    public bool BackupTrickplay { get; set; } = false;
    public int  KeepLastN       { get; set; } = 7;
}
