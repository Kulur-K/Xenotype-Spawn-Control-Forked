// Copyright (c) 2026 Kulur
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

/// <summary>
/// FORKED: The vanilla xenotype editor, used to change the genes, name and inheritance of an existing xenotype def.
/// Accepting writes an edit instead of saving a custom xenotype file.
/// </summary>
public class Dialog_EditXenotypeDef : Dialog_CreateXenotype
{
	private readonly XenotypeDef _def;

	public Dialog_EditXenotypeDef(XenotypeDef def) : base(HarmonyPatches.StartingPawnUtility.MAGIC_NUMBER, null)
	{
		_def = def;
		xenotypeName = def.label ?? def.defName;

		// keep the existing name when genes change. It can still be changed on purpose by typing a new one.
		xenotypeNameLocked = true;

		selectedGenes.Clear();
		selectedGenes.AddRange(def.genes ?? []);
		inheritable = def.inheritable;
		OnGenesChanged();

		ignoreRestrictions = selectedGenes.Any(static gene => gene.biostatArc > 0)
			|| !WithinAcceptableBiostatLimits(false);
	}

	protected override string Header => Strings.Translated.EditXenotypeHeader + ": " + _def.LabelCap;

	protected override bool CanAccept()
	{
		if (string.IsNullOrWhiteSpace(xenotypeName))
		{
			Messages.Message(Strings.Translated.NameEmpty, MessageTypeDefOf.RejectInput, false);
			return false;
		}

		return true;
	}

	protected override void Accept()
	{
		XenotypeDefEditDatabase.SetGenesAndLabel(_def, selectedGenes, xenotypeName.Trim(), inheritable);
		Close();
	}
}

/// <summary>FORKED: Simple text editor for the description of an existing xenotype def.</summary>
public class Dialog_EditXenotypeDescription : Window
{
	private readonly XenotypeDef _def;
	private string _text;
	private Vector2 _scrollPosition;

	public Dialog_EditXenotypeDescription(XenotypeDef def)
	{
		_def = def;
		_text = def.description ?? "";
		doCloseX = true;
		forcePause = true;
		absorbInputAroundWindow = true;
		closeOnClickedOutside = false;
	}

	public override Vector2 InitialSize => new(720f, 560f);

	public override void DoWindowContents(Rect inRect)
	{
		Text.Font = GameFont.Medium;
		Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f),
			Strings.Translated.EditDescriptionHeader + ": " + _def.LabelCap);
		Text.Font = GameFont.Small;

		const float buttonHeight = 35f;
		var textRect = new Rect(inRect.x, inRect.y + 45f, inRect.width,
			inRect.height - 45f - buttonHeight - 10f);
		var viewRect = new Rect(0f, 0f, textRect.width - 16f,
			Mathf.Max(Text.CalcHeight(_text, textRect.width - 26f) + 20f, textRect.height));
		Widgets.BeginScrollView(textRect, ref _scrollPosition, viewRect);
		_text = Widgets.TextArea(viewRect, _text);
		Widgets.EndScrollView();

		var buttonWidth = (inRect.width - 20f) / 3f;
		var buttonY = inRect.yMax - buttonHeight;

		if (Widgets.ButtonText(new(inRect.x, buttonY, buttonWidth, buttonHeight), Strings.Translated.RestoreOriginal))
			_text = XenotypeDefEditDatabase.OriginalDescription(_def);

		if (Widgets.ButtonText(new(inRect.x + buttonWidth + 10f, buttonY, buttonWidth, buttonHeight),
			"CancelButton".Translate()))
		{
			Close();
		}

		if (Widgets.ButtonText(new(inRect.xMax - buttonWidth, buttonY, buttonWidth, buttonHeight),
			"Accept".Translate()))
		{
			XenotypeDefEditDatabase.SetDescription(_def, _text);
			Close();
		}
	}
}
