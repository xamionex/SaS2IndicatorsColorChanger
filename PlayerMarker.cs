using System;
using System.Reflection;
using Common;
using HarmonyLib;
using Menumancer.UIFormat;
using ProjectMage;
using ProjectMage.character;
using ProjectMage.gamestate;
using ProjectMage.gamestate.arenastate;
using ProjectMage.gamestate.outro;
using ProjectMage.player;
using ProjectMage.player.menu;

namespace SaS2IndicatorsColorChanger;

[HarmonyPatch]
internal class PlayerMarker
{
    private static readonly MethodInfo GetCharacterMethod;
    private static readonly MethodInfo GetMainPlayerMethod;

    static PlayerMarker()
    {
        GetCharacterMethod = typeof(Player).GetMethod("GetCharacter", BindingFlags.NonPublic | BindingFlags.Instance);
        GetMainPlayerMethod = typeof(PlayerMgr).GetMethod("GetMainPlayer", BindingFlags.NonPublic | BindingFlags.Static);
    }

    private static Character GetCharacter(Player player)
    {
        return (Character)GetCharacterMethod.Invoke(player, null);
    }

    private static Player GetMainPlayer()
    {
        return (Player)GetMainPlayerMethod.Invoke(null, null);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerMenu), "DrawCoopMarker")]
    public static bool DrawCoopMarkerPrefix(PlayerMenu __instance, float scale)
    {
        if (GameState.state != 1 || (IntroManager.active && IntroManager.phase < 2) || ArenaStateMgr.active ||
            OutroManager.active)
        {
            return false;
        }

        var isCoopPlayer = __instance.player != GetMainPlayer();
        float num = Game1.Instance.GraphicsDevice.Viewport.Width;
        float num2 = Game1.Instance.GraphicsDevice.Viewport.Height;
        var vector = new Vector2(num, num2);
        var character = GetCharacter(__instance.player);
        var vector2 = num2 / ScrollManager.screenSize.Y *
                      ScrollManager.GetScreenLoc(character.loc + new Vector2(0f, -70f), 0);
        var num3 = (float)(120.0 * (num2 / 1080.0));
        var num5 = num - num3;
        var num7 = num2 - num3;
        if (vector2.X > (double)num5 || vector2.X < (double)num3 || vector2.Y > (double)num7 ||
            vector2.Y < (double)num3)
        {
            var angle = Trig.GetAngle(vector2, vector / 2f);
            var vector3 = vector2 - vector / 2f;
            var num8 = num / 2f - num3;
            var num9 = num2 / 2f - num3;
            if (vector3.X > (double)num8)
            {
                vector3 *= num8 / vector3.X;
            }

            if (vector3.X < -(double)num8)
            {
                vector3 *= -num8 / vector3.X;
            }

            if (vector3.Y > (double)num9)
            {
                vector3 *= num9 / vector3.Y;
            }

            if (vector3.Y < -(double)num9)
            {
                vector3 *= -num9 / vector3.Y;
            }

            // Use config colors
            var markerColor = isCoopPlayer ? Plugin.CoopPlayerMarkerColor : Plugin.MainPlayerMarkerColor;
            SpriteTools.sprite.Draw(UIRender.interfaceTex, vector / 2f + vector3,
                new Rectangle(128, 512, 64, 64),
                markerColor, angle,
                new Vector2(32f, 32f), (float)(num2 / 1080.0 * 0.75), SpriteEffects.None, 0f);
            if (GetCharacter(__instance.player).dyingFrame <= 0.0)
            {
                return false;
            }

            SpriteTools.sprite.Draw(UIRender.interfaceTex,
                vector / 2f + vector3 - new Vector2((float)(8.5 + 6.5 * Math.Cos(angle + 3.141592653589793)),
                    (float)(6.0 + 4.0 * Math.Sin(angle + 3.141592653589793))),
                UIRender.GetIconRect(60), new Color(0f, 0f, 0f, 1f), 0f, new Vector2(32f, 32f),
                (float)(num2 / 1080.0 * 0.25), SpriteEffects.None, 0f);
            return false;
        }

        // On-screen marker: same color logic
        var markerColorOnScreen = isCoopPlayer ? Plugin.CoopPlayerMarkerColor : Plugin.MainPlayerMarkerColor;
        SpriteTools.sprite.Draw(UIRender.interfaceTex,
            num2 / ScrollManager.screenSize.Y *
            ScrollManager.GetScreenLoc(__instance.player.markerDrawLoc + new Vector2(0f, -140f), 0),
            new Rectangle(1792, 834, 128, 126),
            markerColorOnScreen, 0f,
            new Vector2(64f, 128f), scale * 0.6f, SpriteEffects.None, 0f);
        if (GetCharacter(__instance.player).dyingFrame <= 0.0)
        {
            return false;
        }

        SpriteTools.sprite.Draw(UIRender.interfaceTex,
            num2 / ScrollManager.screenSize.Y * ScrollManager.GetScreenLoc(
                __instance.player.markerDrawLoc + new Vector2(0f, -140f) - new Vector2(10f, 42f), 0),
            UIRender.GetIconRect(60), new Color(0f, 0f, 0f, 1f), 0f, new Vector2(32f, 32f),
            (float)(num2 / 1080.0 * 0.25), SpriteEffects.None, 0f);
        return false;
    }
}