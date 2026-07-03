using Jellyfin.Plugin.AutoBackup.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AutoBackup;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Auto Backup";
    public override Guid Id => new Guid("1175543c-6d20-4cfa-93a6-1476836df34c");
    public override string Description => "Automated scheduled ZIP backups of Jellyfin data.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name                 = "AutoBackup",
            DisplayName          = "Auto Backup",
            EmbeddedResourcePath = $"{GetType().Namespace}.Pages.config.html",
            EnableInMainMenu     = true,
            MenuSection          = "plugins",
            MenuIcon             = "folder",
        };
    }
}
