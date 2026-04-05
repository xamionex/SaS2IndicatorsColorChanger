using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common;
using HarmonyLib;
using ProjectMage.gamestate;
using ProjectMage.particles;
using ProjectMage.particles.particles.debris;
using ProjectMage.player;
using ProjectMage.texturesheet;

namespace SaS2IndicatorsColorChanger;

internal static class DroppedSalt
{
    private static readonly FieldInfo PlayerField;
    private static readonly FieldInfo DroppedXpFrameField;
    private static readonly MethodInfo IsLocalCoopModeMethod;

    static DroppedSalt()
    {
        PlayerField = AccessTools.Field(typeof(PlayerStats), "p");
        DroppedXpFrameField = AccessTools.Field(typeof(PlayerStats), "droppedXPFrame");
        IsLocalCoopModeMethod = AccessTools.Method(typeof(PlayerMgr), "IsLocalCoopMode");
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerStats.DrawXPPile))]
    public static bool DrawXpPilePatch(PlayerStats __instance)
    {
        var player = (Player)PlayerField.GetValue(__instance);
        var droppedXpFrame = (float)DroppedXpFrameField.GetValue(__instance);

        if (__instance.droppedXPArea != GameSessionMgr.gameSession.currentArea)
            return false;

        var screenLoc = ScrollManager.GetScreenLoc(__instance.droppedXPVec, 0);
        if (screenLoc.X <= -100f || screenLoc.Y <= -100f ||
            screenLoc.X >= ScrollManager.screenSize.X + 100f ||
            screenLoc.Y >= ScrollManager.screenSize.Y + 100f)
            return false;

        var color = GetPlayerColor(player.ID);
        var r = color.R;
        var g = color.G;
        var b = color.B;

        for (var i = 0; i < 4; i++)
        {
            float num;
            for (num = droppedXpFrame + i * 0.25f; num > 1f; num -= 1f) { }
            var num2 = num;
            if (num2 > 0.5f) num2 = 1f - num2;
            if (droppedXpFrame < 1f) num2 *= droppedXpFrame;

            Textures.tex[ParticleManager.spritesTexIdx].Draw(
                screenLoc,
                9 + i,
                new Vector2(1f, 0.5f) * ScrollManager.cannedDepth[0] * num,
                0f,
                r, g, b,
                num2 * 4f
            );
        }
        return false;
    }

    private static Color GetPlayerColor(int playerId)
    {
        try
        {
            if (playerId < 0) playerId = GameSessionMgr.gameSession?.mainPlayerIdx ?? 0;

            var isCoopMode = false;
            if (IsLocalCoopModeMethod != null)
                isCoopMode = (bool)IsLocalCoopModeMethod.Invoke(null, null);

            if (isCoopMode)
            {
                var mainIdx = GameSessionMgr.gameSession?.mainPlayerIdx ?? 0;
                return playerId != mainIdx ? Plugin.CoopPlayerMarkerColor : Plugin.MainPlayerMarkerColor;
            }
        }
        catch
        {
            // ignored
        }

        return Plugin.MainPlayerMarkerColor;
    }

    public static float GetCorrectedColor(float originalValue, Particle p, int component)
    {
        // Target XP-related effects (Basic, Shimmer, Glow Ray, Pickup Glow)
        if (p == null || p.aux > 3) return originalValue;

        var color = GetPlayerColor(p.owner);
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;

        // Use a 0.75x multiplier for the shimmer (aux 1) to keep visual depth
        var multiplier = p.aux == 1 ? 0.75f : 1.0f;

        return component switch
        {
            0 => r * multiplier,
            1 => g * multiplier,
            2 => b * multiplier,
            _ => originalValue
        };
    }

    [HarmonyPatch(typeof(XPWisp), nameof(XPWisp.Draw))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> XpWispDrawTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        int localR = -1, localG = -1, localB = -1;

        // Step 1: Locate the indices for the internal R, G, B variables (initial 0.5, 0.6, 1.0)
        for (var i = 0; i < codes.Count - 5; i++)
        {
            const float tolerance = 0.01f;
            if (codes[i].opcode != OpCodes.Ldc_R4 || !(Math.Abs((float)codes[i].operand - 0.5f) < tolerance) ||
                codes[i + 2].opcode != OpCodes.Ldc_R4 || !(Math.Abs((float)codes[i + 2].operand - 0.6f) < tolerance) ||
                codes[i + 4].opcode != OpCodes.Ldc_R4 ||
                !(Math.Abs((float)codes[i + 4].operand - 1.0f) < tolerance)) continue;

            localR = GetIdx(codes[i + 1]);
            localG = GetIdx(codes[i + 3]);
            localB = GetIdx(codes[i + 5]);
            break;
        }

        foreach (var instr in codes)
        {
            yield return instr;

            var component = -1;
            // Check if this instruction is loading one of our color variables
            if (instr.IsLdloc()) 
            {
                var currentIdx = GetIdx(instr);
                if (currentIdx == localR) component = 0;
                else if (currentIdx == localG) component = 1;
                else if (currentIdx == localB) component = 2;
            }

            if (component == -1) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_1); // Load 'Particle p'
            yield return new CodeInstruction(OpCodes.Ldc_I4, component);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DroppedSalt), nameof(GetCorrectedColor)));
        }
    }

    private static int GetIdx(CodeInstruction instr)
    {
        // Handle macro opcodes (0-3)
        if (instr.opcode == OpCodes.Ldloc_0 || instr.opcode == OpCodes.Stloc_0) return 0;
        if (instr.opcode == OpCodes.Ldloc_1 || instr.opcode == OpCodes.Stloc_1) return 1;
        if (instr.opcode == OpCodes.Ldloc_2 || instr.opcode == OpCodes.Stloc_2) return 2;
        if (instr.opcode == OpCodes.Ldloc_3 || instr.opcode == OpCodes.Stloc_3) return 3;

        // Handle standard/short opcodes with operands
        return instr.operand switch
        {
            null => -1,
            sbyte sb => sb,
            byte b => b,
            int i => i,
            LocalBuilder lb => lb.LocalIndex,
            _ => -1
        };
    }
}