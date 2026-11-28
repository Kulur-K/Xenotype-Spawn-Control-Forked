// Copyright (c) 2026 Kulur
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

/// <summary>
/// FORKED: Edits of existing xenotype defs (vanilla and mods). Shared by all saves, stored as a diff against the
/// loaded def, and applied to the def in memory. Only pawns generated afterwards are affected.
/// </summary>
public class XenotypeDefEdit : IExposable
{
	public List<string> addedGenes = [];
	public List<string> removedGenes = [];
	public string? label;
	public string? description;
	public bool hasInheritable;
	public bool inheritable;

	public bool IsEmpty
		=> addedGenes.Count == 0 && removedGenes.Count == 0 && label == null && description == null
			&& !hasInheritable;

	public void ExposeData()
	{
		Scribe_Collections.Look(ref addedGenes, "addedGenes", LookMode.Value);
		Scribe_Collections.Look(ref removedGenes, "removedGenes", LookMode.Value);
		Scribe_Values.Look(ref label, "label");
		Scribe_Values.Look(ref description, "description");
		Scribe_Values.Look(ref hasInheritable, "hasInheritable");
		Scribe_Values.Look(ref inheritable, "inheritable");

		addedGenes ??= [];
		removedGenes ??= [];
	}
}

public static class XenotypeDefEditDatabase
{
	private static Dictionary<string, XenotypeDefEdit> _edits = new();
	private static readonly Dictionary<XenotypeDef, Original> _originals = new();
	private static bool _initialized;

	private sealed class Original
	{
		public readonly List<GeneDef> Genes;
		public readonly string? Label;
		public readonly string? Description;
		public readonly string? DescriptionShort;
		public readonly bool Inheritable;

		public Original(XenotypeDef def)
		{
			Genes = [..def.genes ?? []];
			Label = def.label;
			Description = def.description;
			DescriptionShort = def.descriptionShort;
			Inheritable = def.inheritable;
		}
	}

	public static IEnumerable<XenotypeDef> EditableDefs
		=> DefDatabase<XenotypeDef>.AllDefsListForReading
			.Where(static def => def != XenotypeDefOf.Baseliner)
			.OrderBy(static def => def.label ?? def.defName);

	public static void ExposeData()
	{
		Scribe_Collections.Look(ref _edits, "XenotypeDefEdits", LookMode.Value, LookMode.Deep);
		_edits ??= new();
		_edits.RemoveAll(static pair => pair.Value is null || pair.Value.IsEmpty);
	}

	/// <summary>Called once on startup, after all defs are loaded and patched.</summary>
	public static void Initialize()
	{
		if (_initialized)
			return;

		_initialized = true;

		foreach (var def in DefDatabase<XenotypeDef>.AllDefsListForReading)
			_originals[def] = new(def);

		foreach (var defName in _edits.Keys)
		{
			if (DefDatabase<XenotypeDef>.GetNamedSilentFail(defName) is { } def)
				Apply(def);
			else
				Log.Warning($"[Xenotype Spawn Control FORKED] Edited xenotype '{defName}' is not loaded. The edit is kept, but skipped.");
		}
	}

	public static bool IsEdited(XenotypeDef def) => _edits.ContainsKey(def.defName);

	public static string OriginalDescription(XenotypeDef def) => GetOriginal(def).Description ?? "";

	public static void SetGenesAndLabel(XenotypeDef def, IEnumerable<GeneDef> genes, string label, bool inheritable)
	{
		var original = GetOriginal(def);
		var edit = GetOrCreateEdit(def);
		var newGenes = genes.ToList();

		edit.removedGenes = original.Genes.Where(gene => !newGenes.Contains(gene)).Select(static gene => gene.defName)
			.ToList();
		edit.addedGenes = newGenes.Where(gene => !original.Genes.Contains(gene)).Select(static gene => gene.defName)
			.ToList();
		edit.label = label == (original.Label ?? def.defName) ? null : label;
		edit.hasInheritable = inheritable != original.Inheritable;
		edit.inheritable = inheritable;

		Commit(def, edit);
	}

	public static void SetDescription(XenotypeDef def, string description)
	{
		var original = GetOriginal(def);
		var edit = GetOrCreateEdit(def);
		edit.description = description == (original.Description ?? "") ? null : description;
		Commit(def, edit);
	}

	public static void Reset(XenotypeDef def)
	{
		_edits.Remove(def.defName);
		Apply(def);
		XenotypeDiversityMod.Settings.Write();
	}

	private static void Commit(XenotypeDef def, XenotypeDefEdit edit)
	{
		if (edit.IsEmpty)
			_edits.Remove(def.defName);
		else
			_edits[def.defName] = edit;

		Apply(def);
		XenotypeDiversityMod.Settings.Write();
	}

	private static XenotypeDefEdit GetOrCreateEdit(XenotypeDef def)
		=> _edits.TryGetValue(def.defName, out var edit) ? edit : new();

	private static Original GetOriginal(XenotypeDef def)
	{
		if (!_originals.TryGetValue(def, out var original))
			_originals[def] = original = new(def);

		return original;
	}

	private static void Apply(XenotypeDef def)
	{
		var original = GetOriginal(def);

		// Keep the same list instance, other code may hold a reference to it
		def.genes ??= [];
		def.genes.Clear();
		def.genes.AddRange(original.Genes);
		def.label = original.Label;
		def.description = original.Description;
		def.descriptionShort = original.DescriptionShort;
		def.inheritable = original.Inheritable;

		if (_edits.TryGetValue(def.defName, out var edit))
		{
			List<string>? missingGenes = null;

			foreach (var geneName in edit.removedGenes)
				def.genes.RemoveAll(gene => gene.defName == geneName);

			foreach (var geneName in edit.addedGenes)
			{
				if (DefDatabase<GeneDef>.GetNamedSilentFail(
					BackCompatibility.BackCompatibleDefName(typeof(GeneDef), geneName)) is not { } gene)
				{
					(missingGenes ??= []).Add(geneName);
					continue;
				}

				if (!def.genes.Contains(gene))
					def.genes.Add(gene);
			}

			if (edit.label != null)
				def.label = edit.label;

			if (edit.description != null)
			{
				def.description = edit.description;
				def.descriptionShort = null;
			}

			if (edit.hasInheritable)
				def.inheritable = edit.inheritable;

			if (missingGenes != null)
			{
				Log.Warning($"[Xenotype Spawn Control FORKED] Edited xenotype '{def.defName}' adds genes that are not loaded. They are skipped: {
					string.Join(", ", missingGenes)}");
			}
		}

		def.ClearCachedData();
	}
}
