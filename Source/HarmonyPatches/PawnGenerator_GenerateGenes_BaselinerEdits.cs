// Copyright (c) 2026 Kulur
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using JetBrains.Annotations;

namespace XenotypeSpawnControl.HarmonyPatches;

/// <summary>
/// FORKED: Vanilla gives custom xenotype pawns the Baseliner xenotype and adds its genes. Edited Baseliner genes
/// should only reach human baseliners, so the original genes are used for custom xenotypes and other races.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GenerateGenes))]
// ReSharper disable once InconsistentNaming
public static class PawnGenerator_GenerateGenes_BaselinerEdits
{
	[HarmonyPrefix]
	[HarmonyPriority(Priority.Last)] // after the prefix that picks custom xenotypes
	[UsedImplicitly]
	public static void Prefix(Pawn? pawn, XenotypeDef? xenotype, ref PawnGenerationRequest request,
		out List<GeneDef>? __state)
	{
		__state = null;

		if (xenotype != XenotypeDefOf.Baseliner || pawn is null)
			return;

		if (request.ForcedCustomXenotype == null && pawn.def == ThingDefOf.Human)
			return;

		__state = XenotypeDefEditDatabase.SwapInOriginalGenes(xenotype);
	}

	[HarmonyFinalizer]
	[UsedImplicitly]
	public static void Finalizer(XenotypeDef? xenotype, List<GeneDef>? __state)
	{
		if (__state != null && xenotype != null)
			XenotypeDefEditDatabase.RestoreGenes(xenotype, __state);
	}
}
