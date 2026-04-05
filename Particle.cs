using System.Reflection;
using HarmonyLib;
using ProjectMage.player;
using ProjectMage.gamestate;
using Common;
using ProjectMage.particles;
using ProjectMage.texturesheet;
using Vector2 = Common.Vector2;

namespace SaS2IndicatorsColorChanger;

[HarmonyPatch(typeof(PlayerStats))]
internal static class ParticlePatch
{
    private static readonly FieldInfo PlayerField;
    private static readonly FieldInfo DroppedXpFrameField;
    private static readonly MethodInfo IsLocalCoopModeMethod;

    static ParticlePatch()
    {
        // Get private fields of PlayerStats
        PlayerField = AccessTools.Field(typeof(PlayerStats), "p");
        DroppedXpFrameField = AccessTools.Field(typeof(PlayerStats), "droppedXPFrame");

        // Get internal static method PlayerMgr.IsLocalCoopMode()
        IsLocalCoopModeMethod = AccessTools.Method(typeof(PlayerMgr), "IsLocalCoopMode");
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerStats.DrawXPPile))]
    public static bool DrawXpPilePatch(PlayerStats __instance)
    {
        // Access private fields
        var player = (ProjectMage.player.Player)PlayerField.GetValue(__instance);
        var droppedXpFrame = (float)DroppedXpFrameField.GetValue(__instance);

        // Check if XP pile is in current area (public field)
        if (__instance.droppedXPArea != GameSessionMgr.gameSession.currentArea)
            return false;

        var screenLoc = ScrollManager.GetScreenLoc(__instance.droppedXPVec, 0);
        if (screenLoc.X <= -100f || screenLoc.Y <= -100f ||
            screenLoc.X >= ScrollManager.screenSize.X + 100f ||
            screenLoc.Y >= ScrollManager.screenSize.Y + 100f)
            return false;

        // Determine if this is the coop player
        var isCoopPlayer = false;
        try
        {
            var localCoopMode = (bool)IsLocalCoopModeMethod.Invoke(null, null);
            if (localCoopMode)
            {
                var mainPlayerIdx = GameSessionMgr.gameSession.mainPlayerIdx;
                isCoopPlayer = player.ID == 1 - mainPlayerIdx;
            }
        }
        catch
        {
            // Fallback: assume not coop if reflection fails
            isCoopPlayer = false;
        }

        var saltColor = isCoopPlayer ? Plugin.CoopPlayerMarkerColor : Plugin.MainPlayerMarkerColor;
        var r = saltColor.R / 255f;
        var g = saltColor.G / 255f;
        var b = saltColor.B / 255f;

        for (var i = 0; i < 4; i++)
        {
            float num;
            for (num = droppedXpFrame + i * 0.25f; num > 1f; num -= 1f) { }
            var num2 = num;
            if (num2 > 0.5f)
                num2 = 1f - num2;
            if (droppedXpFrame < 1f)
                num2 *= droppedXpFrame;

            var alphaScale = num2 * 4f;

            Textures.tex[ParticleManager.spritesTexIdx].Draw(
                screenLoc,
                9 + i,
                new Vector2(1f, 0.5f) * ScrollManager.cannedDepth[0] * num,
                0f,
                r, g, b,
                alphaScale
            );
        }

        return false;
    }
}