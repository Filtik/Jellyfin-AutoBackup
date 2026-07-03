using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AutoBackup.Controllers;

[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("AutoBackup")]
public class BackupManagementController : ControllerBase
{
    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<BackupManagementController> _logger;

    public BackupManagementController(IApplicationPaths appPaths, ILoggerFactory loggerFactory)
    {
        _appPaths = appPaths;
        _logger   = loggerFactory.CreateLogger<BackupManagementController>();
    }

    [HttpGet("Backups")]
    public IActionResult ListBackups()
    {
        var backupDir = Path.Combine(_appPaths.DataPath, "backups");
        if (!Directory.Exists(backupDir))
            return Ok(Array.Empty<object>());

        var files = Directory.GetFiles(backupDir, "jellyfin-backup-*.zip")
            .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f));

        var result = new List<object>();
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            object? options = null;
            try
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(file);
                var entry = zip.GetEntry("manifest.json");
                if (entry is not null)
                {
                    using var stream = entry.Open();
                    var doc = System.Text.Json.JsonDocument.Parse(stream);
                    if (doc.RootElement.TryGetProperty("Options", out var opts))
                        options = new
                        {
                            Database  = opts.TryGetProperty("Database",  out var db)  && db.GetBoolean(),
                            Metadata  = opts.TryGetProperty("Metadata",  out var md)  && md.GetBoolean(),
                            Subtitles = opts.TryGetProperty("Subtitles", out var sb)  && sb.GetBoolean(),
                            Trickplay = opts.TryGetProperty("Trickplay", out var tp)  && tp.GetBoolean(),
                        };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AutoBackup] Could not read manifest from {File}", file);
            }

            result.Add(new
            {
                Filename    = info.Name,
                DateCreated = info.CreationTimeUtc,
                SizeBytes   = info.Length,
                Options     = options,
            });
        }

        return Ok(result);
    }

    [HttpDelete("Backup/{filename}")]
    public IActionResult DeleteBackup([FromRoute] string filename)
    {
        // Only allow jellyfin-backup-*.zip filenames — no path traversal
        if (!System.Text.RegularExpressions.Regex.IsMatch(filename, @"^jellyfin-backup-[\w\-]+\.zip$"))
            return BadRequest("Invalid filename.");

        var path = Path.Combine(_appPaths.DataPath, "backups", filename);
        if (!System.IO.File.Exists(path))
            return NotFound();

        System.IO.File.Delete(path);
        _logger.LogInformation("[AutoBackup] Deleted backup: {File}", path);
        return NoContent();
    }
}
