using BepInEx;
using BepInEx.Configuration;
using Common;
using HarmonyLib;
using System.IO;
using System.Timers;
using BepInEx.Logging;

namespace SaS2IndicatorsColorChanger;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
public class Plugin : BepInEx.NetLauncher.Common.BasePlugin
{
    // Static properties to hold marker colors (readable from other classes)
    public static Color MainPlayerMarkerColor { get; private set; } = new Color(1f, 0.5f, 0.4f, 1f);   // default orange
    public static Color CoopPlayerMarkerColor { get; private set; } = new Color(0.4f, 0.5f, 1f, 1f); // default blue

    private ConfigEntry<string> _mainPlayerColorConfig;
    private ConfigEntry<string> _coopPlayerColorConfig;

    private FileSystemWatcher _configWatcher;
    private Timer _debounceTimer;
    public static Plugin Main;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoOptimization | System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void Load()
    {
        Main = this;

        // Main player marker color config
        _mainPlayerColorConfig = Config.Bind(
            "Colors",
            "MainPlayerMarkerColor",
            "0.4,0.5,1,1",
            "Color for the main player marker (R,G,B,A) each in range 0-1."
        );

        // Coop player marker color config
        _coopPlayerColorConfig = Config.Bind(
            "Colors",
            "CoopPlayerMarkerColor",
            "1,0.5,0.4,1",
            "Color for the coop player marker (R,G,B,A) each in range 0-1."
        );

        // Initial parse
        UpdateColorsFromConfig();

        // Setup file watcher to detect external changes to the config file
        var configFilePath = Config.ConfigFilePath;
        var configDirectory = Path.GetDirectoryName(configFilePath);
        var configFileName = Path.GetFileName(configFilePath);

        if (!string.IsNullOrEmpty(configDirectory))
        {
            _configWatcher = new FileSystemWatcher(configDirectory, configFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += OnConfigFileChanged;

            // Debounce timer to avoid multiple rapid events
            _debounceTimer = new Timer(500) { AutoReset = false };
            _debounceTimer.Elapsed += (_, _) =>
            {
                // Reload the config file and update colors
                Config.Reload();
                UpdateColorsFromConfig();
            };
        }
        else
        {
            Log.LogWarning("Could not determine config file directory; file watching disabled.");
        }

        var harmony = new Harmony(PluginInfo.PluginGuid);
        harmony.PatchAll();

        Log.LogInfo($"{PluginInfo.PluginName} v{PluginInfo.PluginVersion} loaded.");
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // Restart the debounce timer
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private void UpdateColorsFromConfig()
    {
        // Parse main player color
        var mainParts = _mainPlayerColorConfig.Value.Split(',');
        if (mainParts.Length == 4 &&
            float.TryParse(mainParts[0], out var rMain) &&
            float.TryParse(mainParts[1], out var gMain) &&
            float.TryParse(mainParts[2], out var bMain) &&
            float.TryParse(mainParts[3], out var aMain))
        {
            MainPlayerMarkerColor = new Color(rMain, gMain, bMain, aMain);
            Log.LogInfo($"MainPlayerMarkerColor updated to: R={rMain} G={gMain} B={bMain} A={aMain}");
        }
        else
        {
            Log.LogWarning($"Failed to parse MainPlayerMarkerColor config value '{_mainPlayerColorConfig.Value}'. Keeping previous color.");
        }

        // Parse coop player color
        var coopParts = _coopPlayerColorConfig.Value.Split(',');
        if (coopParts.Length == 4 &&
            float.TryParse(coopParts[0], out var rCoop) &&
            float.TryParse(coopParts[1], out var gCoop) &&
            float.TryParse(coopParts[2], out var bCoop) &&
            float.TryParse(coopParts[3], out var aCoop))
        {
            CoopPlayerMarkerColor = new Color(rCoop, gCoop, bCoop, aCoop);
            Log.LogInfo($"CoopPlayerMarkerColor updated to: R={rCoop} G={gCoop} B={bCoop} A={aCoop}");
        }
        else
        {
            Log.LogWarning($"Failed to parse CoopPlayerMarkerColor config value '{_coopPlayerColorConfig.Value}'. Keeping previous color.");
        }
    }

    public override bool Unload()
    {
        // Clean up file watcher and timer
        _configWatcher?.Dispose();
        _debounceTimer?.Dispose();
        return base.Unload();
    }
}