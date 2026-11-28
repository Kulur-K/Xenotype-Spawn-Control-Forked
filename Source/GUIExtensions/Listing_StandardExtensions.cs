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
		
		// FORKED: clearer layout. Row 1: label, mode button, value field and resulting percentage.
		// Row 2: slider, only active for absolute chances.
		const float rowHeight = 26f;
		const float modeButtonWidth = 64f;
		const float gap = 4f;

		var xenotype = xenotypeChance.Xenotype;
		var label = xenotype.DisplayLabel;
		if (showName)
			label += $" ({xenotype.Name})";

		var topRect = listing.GetRect(rowHeight);
		var previousAlignment = Text.Anchor;

		var resultLabel = isAbsolute ? "%" : $"= {absoluteChance.String}%";
		var resultWidth = Text.CalcSize(isAbsolute ? "%" : "= 100.0%").x + gap;
		var fieldWidth = Math.Max(Text.CalcSize("77.77)").x,
			Text.CalcSize((isAbsolute ? absoluteChance.String : weightedChance.String) + ")").x);

		var resultRect = new Rect(topRect.xMax - resultWidth, topRect.y, resultWidth, rowHeight);
		var fieldRect = new Rect(resultRect.x - fieldWidth, topRect.y + 1f, fieldWidth, rowHeight - 2f);
		var modeRect = new Rect(fieldRect.x - gap - modeButtonWidth, topRect.y + 1f, modeButtonWidth, rowHeight - 2f);
		var labelRect = new Rect(topRect.x, topRect.y, modeRect.x - gap - topRect.x, rowHeight);

		Text.Anchor = TextAnchor.MiddleLeft;
		Widgets.Label(labelRect, label.Truncate(labelRect.width));
		if (xenotype.Tooltip is { } tooltip)
			TooltipHandler.TipRegion(labelRect, tooltip);

		Text.Anchor = TextAnchor.MiddleRight;
		Widgets.Label(resultRect, resultLabel);
		Text.Anchor = previousAlignment;

		if (Widgets.ButtonText(modeRect, isAbsolute ? Strings.Translated.ModeAbsolute : Strings.Translated.ModeWeight))
			isAbsolute = !isAbsolute;

		TooltipHandler.TipRegion(modeRect, Strings.Translated.IndividualAbsoluteModeToggleTooltip);

		var textFieldResult = Widgets.TextField(fieldRect,
			isAbsolute == xenotypeChance.IsAbsolute
				? isAbsolute ? absoluteChance.String : weightedChance.String
				: xenotypeChance.IsAbsolute ? absoluteChance.String : weightedChance.String);

		// a just switched mode only takes effect on the next frame, the field still showed the old value
		if (isAbsolute == xenotypeChance.IsAbsolute)
		{
			if (isAbsolute)
				InterpretPercentString(ref absoluteChance, textFieldResult);
			else
				InterpretWeightString(ref weightedChance, textFieldResult);
		}

		var sliderRect = listing.GetRect(18f);
		var sliderColor = GUI.color;
		if (!isAbsolute)
			GUI.color = sliderColor with { a = sliderColor.a * 0.4f };

		// own slider, so slider mods like BetterSliders don't draw their input field over ours
		var sliderResult = PlainSlider(sliderRect, absoluteChance.Value, 0f, 1f, xenotypeChance, isAbsolute);
		GUI.color = sliderColor;

		if (isAbsolute)
			absoluteChance.Value = sliderResult;

		listing.Gap(6f);

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
	public static float PlainSlider(Rect rect, float value, float min, float max, object key, bool interactable = true)
	{
		const float handleSize = 12f;

		var railRect = new Rect(rect.x + (handleSize / 2f), rect.center.y - 1f, rect.width - handleSize, 2f);
		var color = GUI.color;
		GUI.color = _sliderRailColor with { a = _sliderRailColor.a * color.a };
		GUI.DrawTexture(railRect, BaseContent.WhiteTex);

		var position = Mathf.InverseLerp(min, max, value);
		var handleRect = new Rect(railRect.x + (position * railRect.width) - (handleSize / 2f),
			rect.center.y - (handleSize / 2f), handleSize, handleSize);

		GUI.color = (_draggedSlider == key || (interactable && Mouse.IsOver(rect)) ? Color.white : _sliderHandleColor)
			with { a = color.a };
		GUI.DrawTexture(handleRect, BaseContent.WhiteTex);
		GUI.color = color;

		var currentEvent = Event.current;
		if (!interactable)
			return value;

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

	private static void InterpretPercentString(ref (float Value, string String) absoluteChance, string text)
	{
		if (text.NullOrEmpty() || text == ".")
		{
			absoluteChance.Value = 0f;
			absoluteChance.String = text;
			return;
		}

		if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var enteredValue))
			return;

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
				var decimalPointIndex = text.IndexOf('.');
				if (decimalPointIndex != -1 && decimalPointIndex < text.Length - 1)
				{
					absoluteChance.String = text[..(decimalPointIndex + 2)];
					absoluteChance.Value = float.Parse(absoluteChance.String, CultureInfo.InvariantCulture) / 100f;
				}
				else
				{
					absoluteChance.Value = enteredValue / 100f;
					absoluteChance.String = text;
				}

				break;
			}
		}
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