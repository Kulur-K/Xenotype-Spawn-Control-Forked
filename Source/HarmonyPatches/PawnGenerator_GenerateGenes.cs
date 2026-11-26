// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using JetBrains.Annotations;

namespace XenotypeSpawnControl.HarmonyPatches;

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GenerateGenes))]
// ReSharper disable once InconsistentNaming
public static class PawnGenerator_GenerateGenes
{
	[HarmonyPrefix]
	[UsedImplicitly]
	public static void Prefix(Pawn? pawn, ref XenotypeDef? xenotype, ref PawnGenerationRequest request)
	{
		if (pawn?.genes is null)
			return;

		if (xenotype is null)
			Log.Error($"Tried to generate genes for null xenotype on pawn {pawn}");

		if (xenotype != XenotypeDefOf.Baseliner || request.ForcedCustomXenotype != null
			|| (request.ForcedXenotype is { } forcedXenotype && forcedXenotype != XenotypeDefOf.Baseliner))
		{
			return;
		}

		// FORKED: requests that restrict the allowed xenotypes (quests etc.) are left alone
		if (request.AllowedXenotypes is { Count: > 0 })
			return;

		if (TryGetCustomXenotype(ref request, out var customXenotype))
			request.ForcedCustomXenotype = customXenotype;
	}

	/// <summary>
	/// FORKED: Custom xenotypes are rolled only once, using the most specific config that has any custom
	/// xenotype chance assigned (pawnkind, then memes, then faction).
	/// This method only runs after vanilla already picked Baseliner, so the configured chance gets divided by
	/// vanilla's Baseliner chance. That way the overall spawn rate of custom xenotypes matches the configured value,
	/// instead of being reduced by vanilla's Baseliner chance and compounded by rolling once per config level.
	/// </summary>
	private static bool TryGetCustomXenotype(ref PawnGenerationRequest request,
		[NotNullWhen(true)] out CustomXenotype? result)
	{
		result = null;

		if (!TryGetMostSpecificCustomChances(ref request, out var getXenotype, out var customChanceSum))
			return false;

		var vanillaBaselinerChance = GetVanillaBaselinerChance(ref request);
		if (vanillaBaselinerChance <= 0f)
			return false;

		if (!Rand.Chance(Mathf.Clamp01(customChanceSum / vanillaBaselinerChance)))
			return false;

		result = getXenotype();
		return result != null;
	}

	private static bool TryGetMostSpecificCustomChances(ref PawnGenerationRequest request,
		[NotNullWhen(true)] out Func<CustomXenotype?>? getXenotype, out float customChanceSum)
	{
		if (request.KindDef is { } pawnKindDef
			&& TryGetCustomChances<PawnKindDef>(pawnKindDef.defName, out getXenotype, out customChanceSum))
		{
			return true;
		}

		if (request.Faction is { } faction)
		{
			if (faction.ideos?.PrimaryIdeo?.memes is { } memes)
			{
				// later memes used to overwrite earlier ones, so they keep priority
				for (var i = memes.Count - 1; i >= 0; i--)
				{
					if (TryGetCustomChances<MemeDef>(memes[i].defName, out getXenotype, out customChanceSum))
						return true;
				}
			}

			if (TryGetCustomChances<FactionDef>(faction.def.defName, out getXenotype, out customChanceSum))
				return true;
		}

		getXenotype = null;
		customChanceSum = 0f;
		return false;
	}

	private static bool TryGetCustomChances<T>(string defName, [NotNullWhen(true)] out Func<CustomXenotype?>? getXenotype,
		out float customChanceSum) where T : Def
	{
		var xenotypeChances = XenotypeChanceDatabase<T>.For(defName);
		customChanceSum = xenotypeChances.GetCustomXenotypeChanceValueSum();

		if (customChanceSum <= 0f)
		{
			getXenotype = null;
			return false;
		}

		getXenotype = () => PickCustomXenotype(xenotypeChances);
		return true;
	}

	internal static CustomXenotype? PickCustomXenotype<T>(XenotypeChances<T> xenotypeChances) where T : Def
	{
		if (!xenotypeChances.CustomXenotypeChances.TryRandomElementByWeight(static chance => chance.RawValue,
			out var randomResult))
		{
			return null;
		}

		return randomResult.Xenotype is ModifiableXenotype.Generated xenotypeGenerator
			? xenotypeGenerator.GenerateXenotype(xenotypeChances).Copy()
			: randomResult.Xenotype.CustomXenotype ?? throw new("Tried setting null xenotype");
	}

	private static float GetVanillaBaselinerChance(ref PawnGenerationRequest request)
	{
		if (request.KindDef is not { } kindDef)
			return 1f;

		var faction = request.Faction;
		var chances = PawnGenerator.XenotypesAvailableFor(kindDef, faction?.def, faction);
		return chances.TryGetValue(XenotypeDefOf.Baseliner, out var baselinerChance) ? baselinerChance : 0f;
	}
}
