// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Globalization;
using Verse.Sound;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace XenotypeSpawnControl.GUIExtensions;

// ReSharper disable once InconsistentNaming
public static class Listing_StandardExtensions
{
	public static void XenotypeChanceDisplay<T>(this Listing_Standard listing, XenotypeChances<T> xenotypeChancesSet,
		XenotypeChance xenotypeChance, bool showName) where T : Def
	{
		var isAbsolute = xenotypeChance.IsAbsolute;
		(float Value, string String) absoluteChance = (xenotypeChance.Value, xenotypeChance.ChanceString);
		(float Value, string String) weightedChance = (xenotypeChance.Weight, xenotypeChance.WeightString);
		
		// remember previous text alignment
		var previousAlignment = Text.Anchor;
		Text.Anchor = TextAnchor.MiddleRight;

		if (isAbsolute)
		{
			var percentageSize = Text.CalcSize("%");
			
			// draw percentage Label
			Widgets.Label(new(0f, listing.curY, listing.ColumnWidth, percentageSize.y), "%");
			
			// draw percentage string
			var textFieldSize = Text.CalcSize("77.77)");
			var textFieldResult
				= Widgets.TextField(
					new(listing.ColumnWidth - textFieldSize.x - percentageSize.x, listing.curY, textFieldSize.x,
						textFieldSize.y), absoluteChance.String);
			
			// sanitize entered percentage
			if (textFieldResult.NullOrEmpty() || textFieldResult == ".")
			{
				absoluteChance.Value = 0f;
				absoluteChance.String = textFieldResult;
			}
			else if (float.TryParse(textFieldResult, NumberStyles.Float, CultureInfo.InvariantCulture,
				out var enteredValue))
			{
				switch (enteredValue)
				{
					case < 0f:
						absoluteChance.Value = 0f;
						absoluteChance.String = "0";
						break;
					
					case >= 100f:
						absoluteChance.Value = 1f;
						absoluteChance.String = "100";
						break;
					
					default:
					{
						// truncate string if it contains a decimal and decimal is longer than one digit
						var decimalPointIndex = textFieldResult.IndexOf('.');
						if (decimalPointIndex != -1 && decimalPointIndex < textFieldResult.Length - 1)
						{
							absoluteChance.String = textFieldResult[..(decimalPointIndex + 2)];
							absoluteChance.Value = float.Parse(absoluteChance.String, CultureInfo.InvariantCulture) / 100f;
						}
						else
						{
							absoluteChance.Value = enteredValue / 100f;
							absoluteChance.String = textFieldResult;
						}

						break;
					}
				}
			}
		}
		else
		{
			// draw percentage Label
			var percentageSize = Text.CalcSize("|77.7%");
			Widgets.Label(new(0f, listing.curY, listing.ColumnWidth, percentageSize.y), absoluteChance.String + "%");
			
			// draw weight text box
			var textFieldSize = Text.CalcSize("77.77)");
			textFieldSize.x = Math.Max(textFieldSize.x, Text.CalcSize(weightedChance.String + ")").x);
			var textFieldResult
				= Widgets.TextField(
					new(listing.ColumnWidth - textFieldSize.x - percentageSize.x, listing.curY, textFieldSize.x,
						textFieldSize.y), weightedChance.String);
			
			// sanitize entered value
			InterpretWeightString(ref weightedChance, textFieldResult);
		}

		// draw xenotype label
		Text.Anchor = previousAlignment;
		var xenotype = xenotypeChance.Xenotype;
		var label = xenotype.DisplayLabel;
		if (showName)
			label += $" ({xenotype.Name})";
		
		listing.Label(label, tooltip: xenotype.Tooltip);

		// draw a slider with a checkbox to switch chance mode next to it
		var checkBoxSize = Widgets.CheckboxSize;
		var previousColumnWidth = listing.ColumnWidth;
		listing.ColumnWidth = checkBoxSize;
		listing.curX += previousColumnWidth - checkBoxSize;
		
		// cannot create checkbox as widget, or we wouldn't be able to have a tooltip
		listing.CheckboxLabeled(string.Empty, ref isAbsolute, Strings.Translated.IndividualAbsoluteModeToggleTooltip);
		listing.curX -= previousColumnWidth - checkBoxSize;
		listing.ColumnWidth = previousColumnWidth;

		listing.curY -= checkBoxSize;
		listing.ColumnWidth -= checkBoxSize;
		
		// FORKED: own slider, so slider mods like BetterSliders don't draw their input field over ours
		var sliderResult = PlainSlider(listing.GetRect(22f), absoluteChance.Value, 0f, 1f, xenotypeChance);
		listing.Gap(listing.verticalSpacing);
		if (isAbsolute)
			absoluteChance.Value = sliderResult;
		
		listing.ColumnWidth += checkBoxSize;
		listing.Gap(2);

		// apply changes
		var isAbsoluteModified = xenotypeChance.IsAbsolute != isAbsolute;
		var chanceValueModified = isAbsolute && xenotypeChance.Value != absoluteChance.Value;
		var chanceStringModified = xenotypeChance.ChanceString != absoluteChance.String;
		var weightModified = !isAbsolute && xenotypeChance.Weight != weightedChance.Value;
		var weightStringModified = xenotypeChance.WeightString != weightedChance.String;
		
		if (isAbsoluteModified)
			xenotypeChancesSet.SetIsAbsoluteForXenotypeChance(xenotypeChance, isAbsolute);
		
		if (chanceValueModified)
			xenotypeChance.Value = absoluteChance.Value;
		
		if (weightModified)
			xenotypeChance.Weight = weightedChance.Value;
		
		if (chanceValueModified || weightModified)
			xenotypeChancesSet.SetXenotypeChance(xenotypeChance);
		
		if (chanceStringModified)
			xenotypeChance.ChanceString = absoluteChance.String;
		
		if (weightStringModified)
			xenotypeChance.WeightString = weightedChance.String;
	}

	private static object? _draggedSlider;
	private static readonly Color _sliderRailColor = new(1f, 1f, 1f, 0.35f);
	private static readonly Color _sliderHandleColor = new(0.85f, 0.85f, 0.85f);

	/// <summary>
	/// FORKED: Minimal horizontal slider drawn without Widgets.HorizontalSlider, which other mods patch.
	/// <paramref name="key"/> identifies the slider while it is dragged.
	/// </summary>
	public static float PlainSlider(Rect rect, float value, float min, float max, object key)
	{
		const float handleSize = 12f;

		var railRect = new Rect(rect.x + (handleSize / 2f), rect.center.y - 1f, rect.width - handleSize, 2f);
		var color = GUI.color;
		GUI.color = _sliderRailColor;
		GUI.DrawTexture(railRect, BaseContent.WhiteTex);

		var position = Mathf.InverseLerp(min, max, value);
		var handleRect = new Rect(railRect.x + (position * railRect.width) - (handleSize / 2f),
			rect.center.y - (handleSize / 2f), handleSize, handleSize);

		GUI.color = _draggedSlider == key || Mouse.IsOver(rect) ? Color.white : _sliderHandleColor;
		GUI.DrawTexture(handleRect, BaseContent.WhiteTex);
		GUI.color = color;

		var currentEvent = Event.current;
		if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && Mouse.IsOver(rect))
		{
			_draggedSlider = key;
			currentEvent.Use();
		}

		if (_draggedSlider != key)
			return value;

		if (currentEvent.rawType == EventType.MouseUp || !Input.GetMouseButton(0))
		{
			_draggedSlider = null;
			if (currentEvent.type == EventType.MouseUp)
				currentEvent.Use();
		}

		var newValue = Mathf.Lerp(min, max,
			Mathf.InverseLerp(railRect.x, railRect.xMax, currentEvent.mousePosition.x));

		// one decimal of a percent, like the text field
		return Mathf.Round(newValue * 1000f) / 1000f;
	}

	// TODO: move this to an appropriate place and make it look nice
	public static void InterpretWeightString(ref (float Value, string String) weight, string text)
	{
		if (text.NullOrEmpty() || text == ".")
		{
			weight.Value = 0f;
			weight.String = text;
		}
		else if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var enteredValue))
		{
			if (enteredValue < 0f)
			{
				weight.Value = 0f;
				weight.String = "0";
			}
			else
			{
				// do not truncate weights, might cause issues
				weight.Value = enteredValue;
				weight.String = text;
			}
		}
	}

	public static bool SelectionButton(this Listing_Standard listing, string label, bool active, string? tooltip = null)
	{
		//remember to remove text contraction from column width and add downwards margin to get accurate sized rect
		var rect = listing.GetRect(Text.CalcHeight(label, listing.ColumnWidth - 33f) + 15f);
		
		//contract the rect a bit so they aren't pressed flush against the borders
		rect = rect.ContractedBy(4f);
		
		if (Mouse.IsOver(rect))
			TooltipHandler.TipRegion(rect, tooltip);
		
		Widgets.DrawOptionBackground(rect, active);
		
		if (Widgets.ButtonInvisible(rect))
		{
			SoundDefOf.Click.PlayOneShotOnCamera();
			active = true;
		}

		//leave some space around the text
		rect.xMin += 20f;
		rect.xMax -= 5f;
		Widgets.Label(rect, label);
		
		return active;
	}

	public static (bool Clicked, string Text) TextBoxButton(this Listing_Standard listing, string label, string text)
	{
		var textFieldSize = Text.CalcSize("77.77)");
		textFieldSize.x = Math.Max(textFieldSize.x, Text.CalcSize(text + ")").x);

		var listingInitialY = listing.curY;

		// add a small margin as well
		var buttonMargin = 2;
		listing.ColumnWidth -= textFieldSize.x + buttonMargin;

		var buttonResult = listing.ButtonText(label);
		listing.ColumnWidth += textFieldSize.x + buttonMargin;

		// subtract the empty space below the button, the CloseButtonMargin might not be the correct button margin, but it works
		var buttonSize = listing.curY - listingInitialY - Widgets.CloseButtonMargin;

		textFieldSize.y = buttonSize;
		text = Widgets.TextField(
			new(listing.ColumnWidth - textFieldSize.x, listingInitialY, textFieldSize.x, textFieldSize.y), text);

		return (buttonResult, text);
	}
}