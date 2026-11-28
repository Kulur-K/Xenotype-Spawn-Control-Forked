// Copyright (c) 2026 Kulur
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

/// <summary>FORKED: Groups xenotypes in the settings lists by vanilla, each mod and custom xenotypes</summary>
public static class XenotypeGroups
{
	private const int TOP = 0, VANILLA = 1, MOD = 2, CUSTOM = 3;

	private static readonly Color _headerColor = new(0.85f, 0.85f, 0.55f);
	private static readonly Color _lineColor = new(1f, 1f, 1f, 0.3f);

	private static (int Order, string? ModName) GroupOf(ModifiableXenotype xenotype)
		=> xenotype is ModifiableXenotype.Generated || xenotype.Def == XenotypeDefOf.Baseliner
			? (TOP, null)
			: xenotype.Def is { } def
				? GroupOf(def)
				: (CUSTOM, null);

	public static (int Order, string? ModName) GroupOf(XenotypeDef def)
		=> def.modContentPack is { IsOfficialMod: false } mod ? (MOD, mod.Name) : (VANILLA, null);

	public static int Compare(ModifiableXenotype a, ModifiableXenotype b) => Compare(GroupOf(a), GroupOf(b));

	public static int Compare(XenotypeDef a, XenotypeDef b) => Compare(GroupOf(a), GroupOf(b));

	private static int Compare((int Order, string? ModName) a, (int Order, string? ModName) b)
		=> a.Order != b.Order
			? a.Order.CompareTo(b.Order)
			: string.Compare(a.ModName, b.ModName, StringComparison.OrdinalIgnoreCase);

	public static string? HeaderFor(ModifiableXenotype xenotype) => HeaderFor(GroupOf(xenotype));

	public static string? HeaderFor(XenotypeDef def) => HeaderFor(GroupOf(def));

	private static string? HeaderFor((int Order, string? ModName) group)
		=> group.Order switch
		{
			VANILLA => Strings.Translated.GroupVanilla,
			MOD => group.ModName,
			CUSTOM => Strings.Translated.GroupCustom,
			_ => null
		};

	public static void DrawHeader(Listing_Standard listing, string header)
	{
		listing.Gap(8f);
		var rect = listing.GetRect(Text.LineHeight);

		var color = GUI.color;
		GUI.color = _headerColor;
		Widgets.Label(rect, header);
		GUI.color = _lineColor;
		Widgets.DrawLineHorizontal(rect.x, rect.yMax, rect.width);
		GUI.color = color;

		listing.Gap(4f);
	}
}
