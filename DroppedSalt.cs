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

[HarmonyPatch]
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
    [HarmonyPatch(typeof(PlayerStats), "DrawXPPile")]
    // ReSharper disable once InconsistentNaming
    public static bool DrawXpPilePatch(PlayerStats __instance)
    {
        if (__instance.droppedXPArea != GameSessionMgr.gameSession.currentArea)
            return false;

        var screenLoc = ScrollManager.GetScreenLoc(__instance.droppedXPVec, 0);
        if (screenLoc.X <= -100f || screenLoc.Y <= -100f ||
            screenLoc.X >= ScrollManager.screenSize.X + 100f ||
            screenLoc.Y >= ScrollManager.screenSize.Y + 100f)
            return false;

        var player = (Player)PlayerField.GetValue(__instance);
        var droppedXpFrame = (float)DroppedXpFrameField.GetValue(__instance);

        var isCoopMode = false;
        if (IsLocalCoopModeMethod != null)
            isCoopMode = (bool)IsLocalCoopModeMethod.Invoke(null, null);
        
        // coop player = IsLocalCoopMode && ID == 1 - mainPlayerIdx
        var isCoopPlayer = isCoopMode && player.ID == 1 - (GameSessionMgr.gameSession?.mainPlayerIdx ?? 0);
        var color = isCoopPlayer ? Plugin.CoopPlayerMarkerColor : Plugin.MainPlayerMarkerColor;

        //Plugin.Instance?.Log.LogInfo($"[DrawXPPile] playerID={player.ID}, isCoopPlayer={isCoopPlayer}, color=R:{color.R:F3} G:{color.G:F3} B:{color.B:F3}");

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

    public static float GetCorrectedColor(float originalValue, Particle p, int component)
    {
        //Plugin.Instance?.Log.LogInfo($"[GetCorrectedColor] CALLED: orig={originalValue:F3}, p={(p == null ? "NULL" : $"owner={p.owner}, aux={p.aux}")}, component={component}");

        if (p == null || p.aux > 3) return originalValue;

        var isCoopMode = false;
        if (IsLocalCoopModeMethod != null)
            isCoopMode = (bool)IsLocalCoopModeMethod.Invoke(null, null);
        
        // Mirror the game's coop check
        var isCoopPlayer = isCoopMode && p.owner == 1 - (GameSessionMgr.gameSession?.mainPlayerIdx ?? 0);
        var color = isCoopPlayer ? Plugin.CoopPlayerMarkerColor : Plugin.MainPlayerMarkerColor;

        var result = component switch
        {
            0 => color.R,
            1 => color.G,
            2 => color.B,
            _ => originalValue
        };

        var multiplier = p.aux == 1 ? 0.75f : 1.0f;
        //Plugin.Instance?.Log.LogInfo($"[GetCorrectedColor] isCoopPlayer={isCoopPlayer}, comp={component}, orig={originalValue:F3}, result={result:F3}, multiplier={multiplier}, final={result * multiplier:F3}");
        return result * multiplier;
    }

    [HarmonyPatch(typeof(XPWisp), nameof(XPWisp.Draw))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> XpWispDrawTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        int localR = -1, localG = -1, localB = -1;

        //Plugin.Instance?.Log.LogInfo($"[XpWispDrawTranspiler] Starting, total IL instructions: {codes.Count}");

        // Dump all IL so we can see the real constants if the scan fails again
        //for (var d = 0; d < codes.Count; d++)
            //Plugin.Instance?.Log.LogInfo($"[XpWispDrawTranspiler] IL[{d:000}]: {codes[d].opcode} {codes[d].operand}");

        // Try multiple patterns — the old code assumed 0.5/0.6/1.0 but the game actually
        // uses 0.4/0.5/1.0 for main player (confirmed from decompiled DrawXPPile).
        var patterns = new[]
        {
            new[] { 0.4f, 0.5f, 1.0f },  // matches DrawXPPile main-player colors
            new[] { 0.5f, 0.6f, 1.0f },  // original assumption (kept as fallback)
        };

        foreach (var pattern in patterns)
        {
            for (var i = 0; i < codes.Count - 5; i++)
            {
                const float tolerance = 0.01f;
                if (codes[i].opcode != OpCodes.Ldc_R4 || !(Math.Abs((float)codes[i].operand - pattern[0]) < tolerance) ||
                    codes[i + 2].opcode != OpCodes.Ldc_R4 || !(Math.Abs((float)codes[i + 2].operand - pattern[1]) < tolerance) ||
                    codes[i + 4].opcode != OpCodes.Ldc_R4 || !(Math.Abs((float)codes[i + 4].operand - pattern[2]) < tolerance))
                    continue;

                localR = GetIdx(codes[i + 1]);
                localG = GetIdx(codes[i + 3]);
                localB = GetIdx(codes[i + 5]);
                //Plugin.Instance?.Log.LogInfo($"[XpWispDrawTranspiler] FOUND pattern ({pattern[0]},{pattern[1]},{pattern[2]}) at i={i}: localR={localR}, localG={localG}, localB={localB}");
                break;
            }

            if (localR != -1) break;
            //Plugin.Instance?.Log.LogWarning($"[XpWispDrawTranspiler] Pattern ({pattern[0]},{pattern[1]},{pattern[2]}) NOT found");
        }

        //if (localR == -1 || localG == -1 || localB == -1)
            //Plugin.Instance?.Log.LogError("[XpWispDrawTranspiler] FAILED to find R/G/B locals — no injections will occur. Check the IL dump above for actual Ldc_R4 values.");

        //var injectionCount = 0;
        foreach (var instr in codes)
        {
            yield return instr;

            var component = -1;
            if (instr.IsLdloc())
            {
                var currentIdx = GetIdx(instr);
                if (currentIdx == localR) component = 0;
                else if (currentIdx == localG) component = 1;
                else if (currentIdx == localB) component = 2;
            }

            if (component == -1) continue;

            //injectionCount++;
            //Plugin.Instance?.Log.LogInfo($"[XpWispDrawTranspiler] Injecting for component={component} (injection #{injectionCount})");
            yield return new CodeInstruction(OpCodes.Ldarg_1); // Particle p
            yield return new CodeInstruction(OpCodes.Ldc_I4, component);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DroppedSalt), nameof(GetCorrectedColor)));
        }

        //Plugin.Instance?.Log.LogInfo($"[XpWispDrawTranspiler] Done. Total injections: {injectionCount}");
    }

    private static int GetIdx(CodeInstruction instr)
    {
        if (instr.opcode == OpCodes.Ldloc_0 || instr.opcode == OpCodes.Stloc_0) return 0;
        if (instr.opcode == OpCodes.Ldloc_1 || instr.opcode == OpCodes.Stloc_1) return 1;
        if (instr.opcode == OpCodes.Ldloc_2 || instr.opcode == OpCodes.Stloc_2) return 2;
        if (instr.opcode == OpCodes.Ldloc_3 || instr.opcode == OpCodes.Stloc_3) return 3;

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