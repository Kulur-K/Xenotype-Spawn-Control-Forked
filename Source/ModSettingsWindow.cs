// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;
using XenotypeSpawnControl.GUIExtensions;
// ReSharper disable StringCompareToIsCultureSpecific

namespace XenotypeSpawnControl;

public static class ModSettingsWindow
{
	public static readonly Tab[] Tabs =
	[
		new Tab<FactionDef>
		{
			Title = Strings.Translated.Factions,
			Defs = DefDatabase<FactionDef>.AllDefsListForReading.Where(static faction => faction.humanlikeFaction)
		},
		new Tab<MemeDef>
		{
			RequirementsMet = ModsConfig.IdeologyActive,
			Title = Strings.Translated.Memes,
			Defs = DefDatabase<MemeDef>.AllDefsListForReading
		},
		new Tab<PawnKindDef>
		{
			Title = Strings.Translated.PawnKinds,
			Defs = DefDatabase<PawnKindDef>.AllDefsListForReading.Where(static p => p.RaceProps is { Humanlike: true })
		},
		new EditorTab
		{
			Title = Strings.Translated.Editor
		},
		new ExistingXenotypesTab
		{
			Title = Strings.Translated.ExistingXenotypes
		}
	];

	public static void DoWindowContents(Rect inRect)
	{
		if (Event.current.type == EventType.Layout)
			return;
		
		var windowRect = Find.WindowStack.currentlyDrawnWindow.windowRect;
		if (Widgets.ButtonText(
			new(windowRect.width - Window.CloseButSize.x - (Window.StandardMargin * 2f),
				windowRect.height - Window.FooterRowHeight - Window.StandardMargin, Window.CloseButSize.x,
				Window.CloseButSize.y),
			Strings.Translated.ResetAll))
		{
			Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Strings.Translated.ConfirmReset,
				XenotypeChanceDatabases.ResetAll, destructive: true));
		}

		inRect.yMin += 30f;

		Widgets.DrawMenuSection(inRect);
		TabDrawer.DrawTabs(inRect, _tabList);

		inRect = inRect.ContractedBy(20f);

		inRect.width -= 200f;

		/*var titleListing = new Listing_Standard();
		titleListing.Begin(inRect);

		// searchText = titleListing.TextEntry(searchText);
		titleListing.End();*/

		inRect.width += 200f;
		// inRect.yMin += 60f;
		
		try
		{
			_currentTab.DoWindowContents(inRect);
		}
		catch (Exception ex)
		{
			Log.Error($"{ex}");
		}
	}

	static ModSettingsWindow()
	{
		var tabs = Tabs;
		for (var i = 0; i < tabs.Length; i++)
		{
			var tab = tabs[i];
			if (tab.RequirementsMet)
				_tabList.Add(new(tab, static () => ref _currentTab));
		}
	}

	public abstract class Tab
	{
		public string Title { get; init; }
		
		public bool RequirementsMet { get; init; } = true;
		
		public abstract void DoWindowContents(Rect inRect);

		protected Tab() => Title ??= GetType().Name;
	}

	public class Tab<T> : Tab where T : Def
	{
		public IEnumerable<T> Defs { get; init; } = DefDatabase<T>.AllDefs;

		public IEnumerable<ModifiableXenotype> Xenotypes { get; set; }
			= ModifiableXenotypeDatabase.AllValues.Values;

		public Listing_Standard Listing { get; } = new() { verticalSpacing = 4f };

		private string? _currentlySelectedDefName;
		private readonly ScrollViewStatus _defScrollViewStatus = new();
		private readonly ScrollViewStatus _xenotypeScrollViewStatus = new();

		public override void DoWindowContents(Rect inRect)
		{
			var leftHalfRect = inRect.LeftHalf();
			var rightHalfRect = inRect.RightHalf();
			leftHalfRect.width -= 20f;
			ListDefs(leftHalfRect);
			ListXenotypes(rightHalfRect);
		}

		public void ListDefs(Rect outRect)
		{
			using var listingScope
				= new ScrollableListingScope(outRect, _defScrollViewStatus, Listing);
			
			var listing = listingScope.Listing;

			// FORKED: grouped by vanilla and each mod, like the xenotype lists
			_groupedDefs ??= BuildGroupedDefs();

			string? currentGroup = null;
			foreach (var (label, defName, group) in _groupedDefs)
			{
				if (group != currentGroup)
				{
					currentGroup = group;
					XenotypeGroups.DrawHeader(listing, group);
				}

				if (listing.SelectionButton(label, _currentlySelectedDefName == defName))
					_currentlySelectedDefName = defName;
			}

			if (typeof(T) != typeof(FactionDef))
				return;

			listing.Gap(8f);

			if (listing.SelectionButton(Strings.Translated.NoFaction,
				_currentlySelectedDefName == Strings.NoFactionKey))
			{
				_currentlySelectedDefName = Strings.NoFactionKey;
			}
		}

		private List<(string Label, string DefName, string Group)>? _groupedDefs;

		private List<(string Label, string DefName, string Group)> BuildGroupedDefs()
		{
			var defs = Defs.Select(static def => (def.LabelCap.Resolve(), def.defName)).ToList();
			ReplaceEmptyLabels(defs);

			var groups = Defs.ToDictionary(static def => def.defName, static def => XenotypeGroups.GroupOf(def));

			defs.Sort((a, b)
				=> XenotypeGroups.Compare(groups[a.defName], groups[b.defName]) is var groupResult && groupResult != 0
					? groupResult
					: string.CompareOrdinal(a.Item1, b.Item1) is var labelResult && labelResult != 0
						? labelResult
						: string.CompareOrdinal(a.defName, b.defName));

			var result = new List<(string, string, string)>(defs.Count);
			foreach (var (defLabel, defName) in defs)
			{
				var label = defLabel;
				if (!defName.EqualsIgnoreCase(label)
					&& defs.Exists(otherDef => otherDef.Item1.EqualsIgnoreCase(label) && otherDef.defName != defName))
				{
					label += $" ({defName})";
				}

				result.Add((label, defName, XenotypeGroups.HeaderFor(groups[defName])!));
			}

			return result;
		}

		private static void ReplaceEmptyLabels(List<(string label, string defName)> defs)
		{
			for (var i = 0; i < defs.Count; i++)
			{
				var def = defs[i];
				ref var label = ref def.label;
				if (!string.IsNullOrWhiteSpace(label))
					continue;

				label = def.defName;
				defs[i] = def;
			}
		}

		public void ListXenotypes(Rect outRect)
		{
			if (_currentlySelectedDefName is null)
				return;

			using var listingScope
				= new ScrollableListingScope(outRect, _xenotypeScrollViewStatus, new() { verticalSpacing = 4f });
			
			var listing = listingScope.Listing;
			var xenotypeChances = XenotypeChanceDatabase<T>.For(_currentlySelectedDefName);

			TemplateButtons(listing, xenotypeChances);
			listing.Gap(6f);

			// first show all INACTIVE xenotypes
			foreach (var unloadedXenotypeKeyValuePair in xenotypeChances.UnloadedXenotypes.OrderBy(static unloaded
				=> unloaded.Key))
			{
				InactiveLabel(listing, unloadedXenotypeKeyValuePair);
			}

			var allowedXenotypes = xenotypeChances.AllAllowedXenotypeChances
				.Select(static xenotypeChance => (xenotypeChance.Xenotype, xenotypeChance)).ToList();

			// FORKED: sort into groups: generated and baseliner first, then vanilla, each mod, and custom xenotypes
			allowedXenotypes.Sort(static (aKey, bKey) =>
			{
				var a = aKey.Xenotype;
				var b = bKey.Xenotype;

				var groupResult = XenotypeGroups.Compare(a, b);
				if (groupResult != 0)
					return groupResult;

				var bIsBaseliner = b.Def == XenotypeDefOf.Baseliner;

				return a.Def == XenotypeDefOf.Baseliner
					? bIsBaseliner ? CompareLabelsAndNames(a, b) : -1
					: bIsBaseliner
						? 1
						: a is ModifiableXenotype.Generated == b is ModifiableXenotype.Generated
							? a is ModifiableXenotype.Random != b is ModifiableXenotype.Random
								? a is ModifiableXenotype.Random ? -1 : 1
								: CompareLabelsAndNames(a, b)
							: a is ModifiableXenotype.Generated
								? -1
								: 1;
			});

			// then show all configurable xenotypes
			string? currentGroup = null;
			foreach (var (xenotype, xenotypeChance) in allowedXenotypes)
			{
				var group = XenotypeGroups.HeaderFor(xenotype);
				if (group != currentGroup)
				{
					currentGroup = group;
					if (group != null)
						XenotypeGroups.DrawHeader(listing, group);
				}

				var label = xenotype.DisplayLabel;

				listing.XenotypeChanceDisplay(xenotypeChances, xenotypeChance, (label != xenotype.Name
						|| (xenotype.Def?.LabelCap.Resolve() is [_, ..] defLabel && !defLabel.EqualsIgnoreCase(label)))
					&& allowedXenotypes.Exists(other
						=> other.xenotypeChance != xenotypeChance
						&& other.Xenotype.DisplayLabel.EqualsIgnoreCase(label)));
			}

			listing.GapLine();

			var architeCheckboxResult = xenotypeChances.AllowArchiteXenotypes;
			listing.CheckboxLabeled(Strings.Translated.AllowArchiteXenotypes, ref architeCheckboxResult);
			xenotypeChances.AllowArchiteXenotypes = architeCheckboxResult;

			listing.Gap(6f);
			XenotypeChancesTypeButtons(listing, xenotypeChances);
			SetAllWeightsButton(listing, xenotypeChances);

			// hint to notify user about the total chance of weighted xenotypes
			// seems useless
			// if (!xenotypeChances.OnlyAbsoluteChancesAllowed)
			// {
			// 	listing.Label($"{Strings.Translated.WeightDistributionHint} {
			// 		(xenotypeChances.GetRawWeightedDistributionChance() / 10f).ToString("##0.#",
			// 			CultureInfo.InvariantCulture)}%");
			// }
			
			listing.Gap(6f);

			if (listing.ButtonText(Strings.Translated.Reset))
				xenotypeChances.Reset();
		}

		private static int CompareLabelsAndNames(ModifiableXenotype a, ModifiableXenotype b)
			=> string.CompareOrdinal(a.DisplayLabel, b.DisplayLabel) is var labelResult && labelResult != 0
				? labelResult
				: string.CompareOrdinal(a.Name, b.Name) is var nameResult && nameResult != 0
					? nameResult
					: string.CompareOrdinal(a.Def?.label, b.Def?.label);

		private string? _selectedTemplate = XenotypeChanceDatabases.Templates.Keys.FirstOrDefault();
		
		private void TemplateButtons(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			// TODO: use Widgets with listing.GetRect
			var previousWidth = listing.ColumnWidth;
			var previousX = listing.curX;
			var previousY = listing.curY;

			try
			{
				var templates = XenotypeChanceDatabases.Templates;
				var templateNames = templates.Keys;
				
				listing.ColumnWidth /= 2f;
				
				listing.curX += listing.ColumnWidth / 2f;
				
				// quick access for last used template, or first, if none have been used before
				if (_selectedTemplate is null || !templates.ContainsKey(_selectedTemplate))
				{
					_selectedTemplate = templateNames.FirstOrDefault();
				}

				if (_selectedTemplate is null)
				{
					var widthBeforeLabel = listing.ColumnWidth;
					listing.ColumnWidth += listing.ColumnWidth / 2f;
					listing.ButtonText(Strings.Translated.NoTemplateHint);
					listing.ColumnWidth = widthBeforeLabel;
				}
				else
				{
					if (listing.ButtonText(_selectedTemplate))
					{
						var templateOptions = new List<FloatMenuOption>();
						
						foreach (var templateName in templateNames)
						{
							var currentTemplateName = templateName;
							templateOptions.Add(new(templateName, () => _selectedTemplate = currentTemplateName));
						}
						
						Find.WindowStack.Add(new FloatMenu(templateOptions));
					}

					listing.curY = previousY;
					listing.curX += listing.ColumnWidth;
					listing.ColumnWidth = previousWidth / 4f;
					if (listing.ButtonText(Strings.Translated.Apply))
						xenotypeChances.ApplyTemplate(templates[_selectedTemplate]);
				}
			
				// template menu
				listing.curY = previousY;
				listing.curX = previousX;
				// listing.curY = listingTemplateY;
				listing.ColumnWidth = previousWidth / 4f;
			
				if (listing.ButtonText(Strings.Translated.Edit))
				{
					var templateDialog = new Dialog_Templates<T>(xenotypeChances);

					templateDialog.OnClosed = () =>
					{
						if (templateDialog.AppliedOrSavedTemplateName is not null)
							_selectedTemplate = templateDialog.AppliedOrSavedTemplateName;
					};
				
					Find.WindowStack.Add(templateDialog);
				}
			}
			finally
			{
				listing.curX = previousX;
				listing.ColumnWidth = previousWidth;
			}
		}

		private static void XenotypeChancesTypeButtons(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			var onlyAbsolute = xenotypeChances.OnlyAbsoluteChancesAllowed;
			var onlyWeighted = xenotypeChances.OnlyWeightedChancesAllowed;

			var drawDoubleButton = !onlyAbsolute && !onlyWeighted;
			var listingButtonY = listing.curY;
			
			if (drawDoubleButton)
				listing.ColumnWidth /= 2f;
			
			if (!onlyAbsolute && listing.ButtonText(Strings.Translated.AbsoluteToggle))
				xenotypeChances.SetIsAbsoluteForAllowed(true);
			
			if (drawDoubleButton)
			{
				listing.curX += listing.ColumnWidth;
				listing.curY = listingButtonY;
			}
			
			if (!onlyWeighted && listing.ButtonText(Strings.Translated.WeightedToggle))
				xenotypeChances.SetIsAbsoluteForAllowed(false);

			if (!drawDoubleButton)
				return;

			listing.curX -= listing.ColumnWidth;
			listing.ColumnWidth *= 2;
		}

		private (float Value, string String) _allWeightSetterValue = (1, "1");

		private void SetAllWeightsButton(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			var weightsButtonResult = listing.TextBoxButton(Strings.Translated.WeightSetterButton, _allWeightSetterValue.String);
			Listing_StandardExtensions.InterpretWeightString(ref _allWeightSetterValue, weightsButtonResult.Text);
			
			if (weightsButtonResult.Clicked)
				xenotypeChances.SetWeightForAllowedWeightedXenotypes(_allWeightSetterValue.Value);
		}

		private static void InactiveLabel(Listing_Standard listing,
			KeyValuePair<string, XenotypeChanceConfig> xenotypeNameConfigPair)
		{
			var inactiveRect = listing.GetRect(Text.CalcHeight($"{Strings.Translated.Inactive}: ", listing.ColumnWidth)
				+ Listing_Standard.PinnableActionHeight
				+ (listing.verticalSpacing * 2f));
			
			var currentColor = GUI.color;
			GUI.color = Color.red with { a = 0.3f };
			GUI.DrawTexture(inactiveRect, BaseContent.WhiteTex);
			GUI.color = currentColor;
			
			if (Widgets.CloseButtonFor(inactiveRect))
				ModifiableXenotypeDatabase.RemoveCustomXenotype(xenotypeNameConfigPair.Key);

			listing.curY -= inactiveRect.height;

			var inactiveLabel = $"{Strings.Translated.Inactive}: {xenotypeNameConfigPair.Key}, {
				xenotypeNameConfigPair.Value.RawChanceValue / 10m}%";
			
			if (!xenotypeNameConfigPair.Value.IsAbsolute)
				inactiveLabel += $", {Strings.Translated.Weight}: {xenotypeNameConfigPair.Value.Weight}";

			listing.Label(inactiveLabel);
			listing.Gap(Listing_Standard.PinnableActionHeight + listing.verticalSpacing);
		}
	}

	public class EditorTab : Tab
	{
		private ScrollViewStatus _scrollViewStatus = new();
		public Listing_Standard Listing { get; } = new() { verticalSpacing = 4f };

		public override void DoWindowContents(Rect inRect)
		{
			using var listingScope = new ScrollableListingScope(inRect, _scrollViewStatus, Listing);

			foreach (var customXenotype in ModifiableXenotypeDatabase.CustomXenotypes)
			{
				if (customXenotype.Value is ModifiableXenotype.Generated)
					continue;

				var rect = Listing.GetRect(
					Math.Max(Text.CalcHeight(customXenotype.Key, Listing.ColumnWidth * 0.5f), 30f)
					+ Listing.verticalSpacing);
				
				Listing.curY -= rect.height;
				if (Widgets.CloseButtonFor(rect))
					OpenDeletionDialog(customXenotype.Value);

				Listing.columnWidthInt -= Widgets.CloseButtonSize + (Widgets.CloseButtonMargin * 2f);
				if (Listing.ButtonTextLabeled(customXenotype.Key.CapitalizeFirst(), Strings.Translated.Edit))
					Find.WindowStack.Add((Dialog_CreateXenotype?)CreateXenotypeDialogFor(customXenotype));
				
				Listing.columnWidthInt += Widgets.CloseButtonSize + (Widgets.CloseButtonMargin * 2f);
			}

			if (Listing.ButtonTextLabeled(Strings.Translated.MakeYourOwn, "+"))
				Find.WindowStack.Add(new Dialog_CreateXenotype(HarmonyPatches.StartingPawnUtility.MAGIC_NUMBER, null));
		}

		public static void OpenDeletionDialog(ModifiableXenotype xenotype)
			=> Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Strings.ConfirmDelete.Translate(xenotype.Name),
				()
					=> ModifiableXenotypeDatabase.DeleteCustomXenotype(xenotype.Name), destructive: true));

		public static Dialog_CreateXenotype CreateXenotypeDialogFor(
			KeyValuePair<string, ModifiableXenotype> customXenotype)
		{
			var createXenotypeDialog = new Dialog_CreateXenotype(HarmonyPatches.StartingPawnUtility.MAGIC_NUMBER, null)
			{
				xenotypeName = customXenotype.Key
			};
			
			createXenotypeDialog.selectedGenes.Clear();
			createXenotypeDialog.selectedGenes.AddRange(customXenotype.Value.CustomXenotype!.genes);
			createXenotypeDialog.inheritable = customXenotype.Value.CustomXenotype.inheritable;
			createXenotypeDialog.OnGenesChanged();
			
			createXenotypeDialog.ignoreRestrictions
				= createXenotypeDialog.selectedGenes.Any(static def => def.biostatArc > 0)
				|| !createXenotypeDialog.WithinAcceptableBiostatLimits(false);
			
			return createXenotypeDialog;
		}
	}

	/// <summary>FORKED: list of existing xenotype defs, with buttons to edit their genes, name and description</summary>
	public class ExistingXenotypesTab : Tab
	{
		private readonly ScrollViewStatus _scrollViewStatus = new();
		private string _searchText = "";
		private List<XenotypeDef>? _editableDefs;
		public Listing_Standard Listing { get; } = new() { verticalSpacing = 4f };

		public override void DoWindowContents(Rect inRect)
		{
			const float rowHeight = 32f;
			const float buttonWidth = 110f;

			Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 24f), Strings.Translated.ExistingXenotypesHint);
			inRect.yMin += 28f;

			var searchLabelWidth = Text.CalcSize(Strings.Translated.Search).x + 8f;
			Widgets.Label(new Rect(inRect.x, inRect.y + 4f, searchLabelWidth, 24f), Strings.Translated.Search);
			_searchText = Widgets.TextField(new(inRect.x + searchLabelWidth, inRect.y, 300f, 30f), _searchText);
			inRect.yMin += 36f;

			using var listingScope = new ScrollableListingScope(inRect, _scrollViewStatus, Listing);

			_editableDefs ??= XenotypeDefEditDatabase.EditableDefs;
			string? currentGroup = null;

			foreach (var def in _editableDefs)
			{
				if (_searchText.Length > 0
					&& (def.label ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0
					&& def.defName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				var group = XenotypeGroups.HeaderFor(def);
				if (group != currentGroup)
				{
					currentGroup = group;
					if (group != null)
						XenotypeGroups.DrawHeader(Listing, group);
				}

				var rowRect = Listing.GetRect(rowHeight);
				var edited = XenotypeDefEditDatabase.IsEdited(def);

				if (edited)
					Widgets.DrawHighlightSelected(rowRect);
				else if (Mouse.IsOver(rowRect))
					Widgets.DrawHighlight(rowRect);

				var color = GUI.color;
				GUI.color = XenotypeDef.IconColor;
				GUI.DrawTexture(new(rowRect.x, rowRect.y + 1f, rowHeight - 2f, rowHeight - 2f), def.Icon);
				GUI.color = color;

				var buttonCount = edited ? 3 : 2;
				var labelRect = new Rect(rowRect.x + rowHeight + 6f, rowRect.y,
					rowRect.width - rowHeight - 6f - ((buttonWidth + 4f) * 3), rowHeight);

				var label = $"{def.LabelCap} ({def.defName})";
				if (edited)
					label += $" [{Strings.Translated.Edited}]";

				var anchor = Text.Anchor;
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(labelRect, label.Truncate(labelRect.width));
				Text.Anchor = anchor;
				TooltipHandler.TipRegion(labelRect, $"{def.LabelCap}\n{def.modContentPack?.Name ?? ""}\n\n{def.description}");

				var buttonX = rowRect.xMax - ((buttonWidth + 4f) * buttonCount);
				var genesButtonRect = new Rect(buttonX, rowRect.y + 2f, buttonWidth, rowHeight - 4f);
				if (def == XenotypeDefOf.Baseliner)
					TooltipHandler.TipRegion(genesButtonRect, Strings.Translated.BaselinerEditHint);

				if (Widgets.ButtonText(genesButtonRect, Strings.Translated.Genes))
					Find.WindowStack.Add(new Dialog_EditXenotypeDef(def));

				buttonX += buttonWidth + 4f;
				if (Widgets.ButtonText(new(buttonX, rowRect.y + 2f, buttonWidth, rowHeight - 4f),
					Strings.Translated.Description))
				{
					Find.WindowStack.Add(new Dialog_EditXenotypeDescription(def));
				}

				if (!edited)
					continue;

				buttonX += buttonWidth + 4f;
				if (Widgets.ButtonText(new(buttonX, rowRect.y + 2f, buttonWidth, rowHeight - 4f),
					Strings.Translated.ResetEdit))
				{
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						Strings.Translated.ConfirmResetEdit + "\n\n" + def.LabelCap,
						() => XenotypeDefEditDatabase.Reset(def), destructive: true));
				}
			}
		}
	}

	private static Tab _currentTab = Tabs.First();
	private static readonly List<TabRecord> _tabList = [];

	public class TabRecord : Verse.TabRecord
	{
		public Tab Tab { get; }
		public AccessTools.FieldRef<Tab> CurrentTab { get; }
		public TabRecord(Tab tab, AccessTools.FieldRef<Tab> currentTab) : base(tab.Title, null, null)
		{
			Tab = tab;
			CurrentTab = currentTab;
			clickedAction = () => CurrentTab() = Tab;
			selectedGetter = () => CurrentTab() == Tab;
		}
	}
}
