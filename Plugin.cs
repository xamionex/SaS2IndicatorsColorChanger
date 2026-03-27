using BepInEx;
using BepInEx.Configuration;
using Common;
using HarmonyLib;
using System.IO;
using System.Timers;

namespace SaSReverseColorMarker;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
public class Plugin : BepInEx.NetLauncher.Common.BasePlugin
{
    // Static properties to hold marker colors (readable from other classes)
    public static Color MainPlayerMarkerColor { get; private set; } = new Color(1f, 0.5f, 0.4f, 1f);   // default orange
    public static Color CoopPlayerMarkerColor { get; private set; } = new Color(0.4f, 0.5f, 1f, 1f); // default blue

    private ConfigEntry<string> mainPlayerColorConfig;
    private ConfigEntry<string> coopPlayerColorConfig;

    private FileSystemWatcher configWatcher;
    private Timer debounceTimer;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoOptimization | System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void Load()
    {
        // Main player marker color config
        mainPlayerColorConfig = Config.Bind(
            "Colors",
            "MainPlayerMarkerColor",
            "1,0.5,0.4,1",
            "Color for the main player marker (R,G,B,A) each in range 0-1."
        );

        // Coop player marker color config
        coopPlayerColorConfig = Config.Bind(
            "Colors",
            "CoopPlayerMarkerColor",
            "0.4,0.5,1,1",
            "Color for the coop player marker (R,G,B,A) each in range 0-1."
        );

        // Initial parse
        UpdateColorsFromConfig();

        // Setup file watcher to detect external changes to the config file
        string configFilePath = Config.ConfigFilePath;
        string configDirectory = Path.GetDirectoryName(configFilePath);
        string configFileName = Path.GetFileName(configFilePath);

        if (!string.IsNullOrEmpty(configDirectory))
        {
            configWatcher = new FileSystemWatcher(configDirectory, configFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            configWatcher.Changed += OnConfigFileChanged;

            // Debounce timer to avoid multiple rapid events
            debounceTimer = new Timer(500) { AutoReset = false };
            debounceTimer.Elapsed += (sender, args) =>
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
        debounceTimer?.Stop();
        debounceTimer?.Start();
    }

    private void UpdateColorsFromConfig()
    {
        // Parse main player color
        string[] mainParts = mainPlayerColorConfig.Value.Split(',');
        if (mainParts.Length == 4 &&
            float.TryParse(mainParts[0], out float rMain) &&
            float.TryParse(mainParts[1], out float gMain) &&
            float.TryParse(mainParts[2], out float bMain) &&
            float.TryParse(mainParts[3], out float aMain))
        {
            MainPlayerMarkerColor = new Color(rMain, gMain, bMain, aMain);
            Log.LogInfo($"MainPlayerMarkerColor updated to: R={rMain} G={gMain} B={bMain} A={aMain}");
        }
        else
        {
            Log.LogWarning($"Failed to parse MainPlayerMarkerColor config value '{mainPlayerColorConfig.Value}'. Keeping previous color.");
        }

        // Parse coop player color
        string[] coopParts = coopPlayerColorConfig.Value.Split(',');
        if (coopParts.Length == 4 &&
            float.TryParse(coopParts[0], out float rCoop) &&
            float.TryParse(coopParts[1], out float gCoop) &&
            float.TryParse(coopParts[2], out float bCoop) &&
            float.TryParse(coopParts[3], out float aCoop))
        {
            CoopPlayerMarkerColor = new Color(rCoop, gCoop, bCoop, aCoop);
            Log.LogInfo($"CoopPlayerMarkerColor updated to: R={rCoop} G={gCoop} B={bCoop} A={aCoop}");
        }
        else
        {
            Log.LogWarning($"Failed to parse CoopPlayerMarkerColor config value '{coopPlayerColorConfig.Value}'. Keeping previous color.");
        }
    }

    public override bool Unload()
    {
        // Clean up file watcher and timer
        configWatcher?.Dispose();
        debounceTimer?.Dispose();
        return base.Unload();
    }
}