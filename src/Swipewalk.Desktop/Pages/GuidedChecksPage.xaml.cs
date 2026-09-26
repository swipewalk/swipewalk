using Microsoft.Maui.Controls.Shapes;
using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Desktop.Controls;
using Swipewalk.Desktop.Services;
using Swipewalk.Engine;

namespace Swipewalk.Desktop.Pages;

/// <summary>
/// The same guided-check walkthrough as `swipewalk guide`, one screen at a time: suggests which WCAG criteria
/// might not apply here, shows what automation already found, and records a tester's Pass/Fail/Inconclusive/
/// not-applicable answer with the required evidence/reason, saving to guided-answers.json as you go. Navigate
/// with ?folder=&lt;run folder&gt;, the same convention as <see cref="ReportPage"/>.
/// </summary>
[QueryProperty(nameof(Folder), "folder")]
public partial class GuidedChecksPage : ContentPage
{
	private string _folder = "";
	private ScanReport? _report;
	private GuidedAnswerStore _store = GuidedAnswerStore.Empty;
	private List<GuidedAnswer> _answers = [];
	private string _assistiveTechnology = "None";
	private string? _lastTester;

	public GuidedChecksPage()
	{
		InitializeComponent();
		AppMenus.Attach(this);
		foreach (var line in GuidedStepsCatalog.BeforeYouStart)
			BeforeYouStartList.Children.Add(new Label { Text = "• " + line, FontSize = Fonts.Size("FontCaption") });

		AssistiveTechnologyPicker.Items = ["TalkBack", "VoiceOver", "Hardware keyboard", "None"];
		AssistiveTechnologyPicker.SelectedIndex = 3;
		AssistiveTechnologyPicker.SelectedIndexChanged += (_, _) =>
		{
			_assistiveTechnology = AssistiveTechnologyPicker.SelectedIndex >= 0
				? AssistiveTechnologyPicker.Items[AssistiveTechnologyPicker.SelectedIndex] : "None";
			RenderScreen();
		};
		Fonts.Changed += RenderScreen;
	}

	public string Folder
	{
		get => _folder;
		set
		{
			_folder = Uri.UnescapeDataString(value);
			Load();
		}
	}

	private void Load()
	{
		TitleLabel.Text = $"Guided checks — {System.IO.Path.GetFileName(_folder)}";
		var resultsPath = System.IO.Path.Combine(_folder, "results.json");
		_report = File.Exists(resultsPath) ? JsonReport.Deserialize(File.ReadAllText(resultsPath)) : null;
		if (_report is null)
		{
			SummaryLabel.Text = "Could not read this run's results.json.";
			return;
		}

		_store = GuidedAnswerStore.Load(_folder) ?? GuidedAnswerStore.Empty;
		_answers = _store.Answers.ToList();
		// guided-answers.json is a separate sidecar file (see GuidedAnswerStore's remarks), so a freshly
		// deserialized ScanReport never has this set on its own; without this, every computed property that
		// reads it (ScreensWithNoGuidedAnswers, ScreenCoverage, Contradictions) would silently see zero answers.
		_report = _report with { GuidedAnswers = _answers };

		var selected = _report.Screens.Count > 0 ? 0 : -1;
		RefreshHeader(selected);
		RenderScreen();
	}

	/// <summary>Recomputes the gap-check line and the screen picker's own labels/counts -- called after Load
	/// and after every save, since a saved answer can change both.</summary>
	private void RefreshHeader(int selectScreenIndex)
	{
		var noAnswers = _report!.ScreensWithNoGuidedAnswers;
		GapLabel.IsVisible = noAnswers.Count > 0;
		if (noAnswers.Count > 0)
			GapLabel.Text = GuidedChecksDisplay.ScreensWithNoGuidedAnswers(noAnswers.Count, _report.Screens.Count);

		ScreenPicker.Items = [.. _report.Screens.Select(ScreenLabel)];
		ScreenPicker.SelectedIndex = selectScreenIndex;
	}

	/// <summary>"Screen name (N not yet checked)" -- the closest native equivalent to the per-screen "Guided
	/// checks (N to review)" button the design describes for a report row: this app's report is an embedded
	/// HTML WebView (see <see cref="ReportPage"/>), so screens are chosen here instead of from a native row there.</summary>
	private string ScreenLabel(ScreenResult screen)
	{
		var toCheck = ScreenCoverageBuilder.Build(_report!.Screens, _answers)
			.Count(r => r.ScreenId == screen.ScreenId && r.Status is ScreenCriterionStatus.NotTested or ScreenCriterionStatus.PartlyCheckedAutomatically);
		return $"{screen.ScreenName} ({toCheck} not yet checked)";
	}

	private void OnScreenChanged(object? sender, EventArgs e) => RenderScreen();

	private void OnToggleBeforeYouStart(object? sender, EventArgs e)
	{
		BeforeYouStartPanel.IsVisible = !BeforeYouStartPanel.IsVisible;
		ToggleBeforeYouStart.Text = BeforeYouStartPanel.IsVisible ? "Hide before-you-start notes" : "Show before-you-start notes";
	}

	private void RenderScreen()
	{
		CriteriaList.Children.Clear();
		if (_report is null || ScreenPicker.SelectedIndex < 0 || ScreenPicker.SelectedIndex >= _report.Screens.Count)
			return;

		var screen = _report.Screens[ScreenPicker.SelectedIndex];
		var allRows = ScreenCoverageBuilder.Build(_report.Screens, _answers).Where(r => r.ScreenId == screen.ScreenId).ToList();
		var actionable = allRows
			.Where(r => r.Status is ScreenCriterionStatus.GuidedChecked or ScreenCriterionStatus.PartlyCheckedAutomatically or ScreenCriterionStatus.NotTested)
			.ToList();
		var notApplicableCount = allRows.Count(r => r.Status == ScreenCriterionStatus.NotApplicableHere);

		SummaryLabel.Text = $"{actionable.Count} applicable check(s) on this screen, {notApplicableCount} confirmed not applicable.";
		if (actionable.Count == 0)
		{
			CriteriaList.Children.Add(new Label { Text = "Nothing to check on this screen right now.", FontSize = Fonts.Size("FontBody") });
			return;
		}
		foreach (var row in actionable)
			CriteriaList.Children.Add(BuildCard(screen, row));
	}

	private View BuildCard(ScreenResult screen, ScreenCriterionReport row)
	{
		var container = new VerticalStackLayout { Spacing = 6 };
		var header = new Label { Text = $"{row.Number} {row.Name} ({row.Level})", FontAttributes = FontAttributes.Bold, FontSize = Fonts.Size("FontSubheading") };
		SemanticProperties.SetHeadingLevel(header, SemanticHeadingLevel.Level2);
		container.Children.Add(header);
		if (row.SourceUrls.Count > 0)
			container.Children.Add(new Label { Text = row.SourceUrls[0], FontSize = Fonts.Size("FontCaption") });
		if (row.AutomatedSummary is not null)
			container.Children.Add(new Label { Text = row.AutomatedSummary, FontSize = Fonts.Size("FontBody") });
		foreach (var proposal in row.ProposedButNotConfirmed)
			container.Children.Add(new Label
			{
				Text = $"Swipewalk suggests this may not apply here: {proposal.Reason}",
				FontSize = Fonts.Size("FontCaption"),
				TextColor = AppState.ThemeColor("Review"),
			});

		var step = GuidedStepsCatalog.For(row.Number);
		if (step is not null)
		{
			container.Children.Add(new Label { Text = step.RecordPrompt, FontAttributes = FontAttributes.Italic, FontSize = Fonts.Size("FontBody") });
			// "None"/an unrecognized choice falls back to the SCREEN's own platform reader, not always
			// TalkBack -- an iOS run showing "Turn on TalkBack" would be actively wrong on an iPhone.
			var platformDefault = screen.Platform == Swipewalk.Core.Model.Platform.iOS ? step.VoiceOverSteps : step.TalkBackSteps;
			var steps = _assistiveTechnology switch
			{
				"TalkBack" => step.TalkBackSteps,
				"VoiceOver" => step.VoiceOverSteps,
				"Hardware keyboard" => step.HardwareKeyboardSteps ?? platformDefault,
				_ => platformDefault,
			};
			for (var i = 0; i < steps.Count; i++)
				container.Children.Add(new Label { Text = $"{i + 1}. {steps[i]}", FontSize = Fonts.Size("FontCaption") });
		}
		else
		{
			var howToCheck = CoverageCatalog.All.FirstOrDefault(c => c.Criterion.Number == row.Number)?.Note;
			container.Children.Add(new Label
			{
				Text = howToCheck is null
					? "No step-by-step script yet for this criterion."
					: $"No step-by-step script yet for this criterion. How to check it by hand: {howToCheck}",
				FontSize = Fonts.Size("FontCaption"),
			});
		}

		if (row.Answer is not null)
		{
			var text = row.Answer.Result == GuidedAnswerResult.Pass
				? GuidedChecksDisplay.ReportFacingPass(row.Number, screen.ScreenName, string.Join("; ", row.Answer.Evidence.Select(e => e.Description)))
				: $"Recorded: {GuidedChecksDisplay.ResultWord(row.Answer.Result)} on {row.Answer.AnsweredAt:yyyy-MM-dd} by {row.Answer.Tester}.";
			container.Children.Add(new Label { Text = text, FontSize = Fonts.Size("FontCaption"), FontAttributes = FontAttributes.Italic });
			if (screen.RescannedAt is { } rescannedAt && row.Answer.AnsweredAt < rescannedAt)
				container.Children.Add(new Label { Text = GuidedChecksDisplay.AnsweredBeforeRescan(rescannedAt), FontSize = Fonts.Size("FontCaption"), TextColor = AppState.ThemeColor("Review") });
		}
		if (row.Contradicted)
		{
			var contradiction = ContradictionChecker.Find(_report!.Screens, _answers)
				.FirstOrDefault(c => c.ScreenId == screen.ScreenId && c.CriterionNumber == row.Number);
			if (contradiction is not null)
				container.Children.Add(new Label { Text = contradiction.Description, FontSize = Fonts.Size("FontCaption"), TextColor = AppState.ThemeColor("Issue") });
		}

		// Every per-card control's accessible name includes the criterion number: with many criterion cards on
		// one screen, a screen reader tabbing through would otherwise hear several identically-named "Evidence"/
		// "Save"/"Note" controls with no way to tell which criterion each belongs to.
		var resultPicker = new SelectButton { Items = ["Pass", "Fail", "Inconclusive", "Confirm not applicable"], SelectedIndex = -1, HeightRequest = 32 };
		SemanticProperties.SetDescription(resultPicker, $"Result for {row.Number}");
		var evidenceEntry = new Entry { Placeholder = "Evidence (required for pass): what you saw or heard", IsVisible = false };
		// The Description below is what a screen reader announces instead of the placeholder text, so it
		// repeats "required for pass" rather than just "Evidence" -- otherwise that detail is only ever seen,
		// never heard.
		SemanticProperties.SetDescription(evidenceEntry, $"Evidence for {row.Number}, required for pass: what you saw or heard");
		var reasonEntry = new Entry
		{
			Placeholder = "Reason this does not apply (required)",
			IsVisible = false,
			Text = row.ProposedButNotConfirmed.Count > 0 ? row.ProposedButNotConfirmed[0].Reason : null,
		};
		SemanticProperties.SetDescription(reasonEntry, $"Reason {row.Number} does not apply here, required");
		var noteEntry = new Entry { Placeholder = "Note (optional)" };
		SemanticProperties.SetDescription(noteEntry, $"Note for {row.Number}");
		var testerEntry = new Entry { Placeholder = "Tester name or initials", Text = _lastTester ?? "me" };
		SemanticProperties.SetDescription(testerEntry, $"Tester name or initials for {row.Number}");
		var saveButton = new Button { Text = "Save", IsEnabled = false };
		// A Description, not just a Hint: the button's own accessible NAME must be unique per criterion (a Hint
		// alone is announced as a secondary detail, not something a screen reader user can search/target by).
		SemanticProperties.SetDescription(saveButton, $"Save the answer for {row.Number}");
		SemanticProperties.SetHint(saveButton, $"Saves the guided-check answer for {row.Number} on {screen.ScreenName}");

		void UpdateEnabled()
		{
			evidenceEntry.IsVisible = resultPicker.SelectedIndex == 0;
			reasonEntry.IsVisible = resultPicker.SelectedIndex == 3;
			saveButton.IsEnabled = resultPicker.SelectedIndex switch
			{
				0 => !string.IsNullOrWhiteSpace(evidenceEntry.Text),
				3 => !string.IsNullOrWhiteSpace(reasonEntry.Text),
				1 or 2 => true,
				_ => false,
			};
		}
		resultPicker.SelectedIndexChanged += (_, _) => UpdateEnabled();
		evidenceEntry.TextChanged += (_, _) => UpdateEnabled();
		reasonEntry.TextChanged += (_, _) => UpdateEnabled();

		var shownAt = DateTimeOffset.UtcNow;
		saveButton.Clicked += async (_, _) => await SaveAsync(screen, row, resultPicker, evidenceEntry, reasonEntry, noteEntry, testerEntry, shownAt);

		container.Children.Add(resultPicker);
		container.Children.Add(evidenceEntry);
		container.Children.Add(reasonEntry);
		container.Children.Add(noteEntry);
		container.Children.Add(testerEntry);
		container.Children.Add(saveButton);

		return new Border
		{
			Padding = new Thickness(14, 10),
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Content = container,
		};
	}

	private async Task SaveAsync(
		ScreenResult screen, ScreenCriterionReport row, SelectButton resultPicker, Entry evidenceEntry,
		Entry reasonEntry, Entry noteEntry, Entry testerEntry, DateTimeOffset shownAt)
	{
		GuidedAnswerResult? result = resultPicker.SelectedIndex switch
		{
			0 => GuidedAnswerResult.Pass,
			1 => GuidedAnswerResult.Fail,
			2 => GuidedAnswerResult.Inconclusive,
			3 => GuidedAnswerResult.ConfirmedNotApplicable,
			_ => null,
		};
		if (result is null)
			return;

		var fastPassConfirmed = false;
		if (result == GuidedAnswerResult.Pass && DateTimeOffset.UtcNow - shownAt < TimeSpan.FromSeconds(5))
		{
			if (!await DisplayAlertAsync("Guided checks", GuidedChecksDisplay.FastPassPrompt, "Yes, save it", "Cancel"))
				return;
			fastPassConfirmed = true;
		}

		var tester = string.IsNullOrWhiteSpace(testerEntry.Text) ? "me" : testerEntry.Text.Trim();
		_lastTester = tester;
		var evidenceText = evidenceEntry.Text?.Trim() ?? "";

		var answer = new GuidedAnswer(
			ScreenId: screen.ScreenId,
			CriterionNumber: row.Number,
			Result: result.Value,
			Note: string.IsNullOrWhiteSpace(noteEntry.Text) ? null : noteEntry.Text.Trim(),
			NotApplicableReason: result == GuidedAnswerResult.ConfirmedNotApplicable ? reasonEntry.Text?.Trim() : null,
			FastPassConfirmed: fastPassConfirmed,
			Tester: tester,
			AnsweredAt: DateTimeOffset.Now,
			Device: screen.Device?.Model ?? screen.Device?.Name,
			AssistiveTechnology: _assistiveTechnology,
			AssistiveTechnologyVersion: null,
			Evidence: result == GuidedAnswerResult.Pass ? [new GuidedEvidence(evidenceText)] : []);

		_answers.Add(answer);
		_store = _store.With(answer);
		await _store.SaveAsync(_folder);

		var contradiction = ContradictionChecker.Find(_report!.Screens, _answers)
			.FirstOrDefault(c => c.ScreenId == screen.ScreenId && c.CriterionNumber == row.Number && c.Answer == answer);
		string? confirmation = null;
		if (contradiction is not null)
		{
			SemanticScreenReader.Announce(contradiction.Description);
			await DisplayAlertAsync("Guided checks", contradiction.Description, "OK");
		}
		else
		{
			// Every result gets its own confirmation, not just Pass: a confirmed-not-applicable answer removes
			// its card from the actionable list once RenderScreen below rebuilds it, so without this a screen
			// reader user (and a sighted one glancing away) would have no positive confirmation the answer
			// actually saved, only silence.
			confirmation = answer.Result switch
			{
				GuidedAnswerResult.Pass => GuidedChecksDisplay.TesterFacingPass(evidenceText),
				GuidedAnswerResult.ConfirmedNotApplicable => $"You confirmed {row.Number} does not apply on this screen: '{answer.NotApplicableReason}'.",
				GuidedAnswerResult.Fail => $"You recorded a fail for {row.Number}.",
				GuidedAnswerResult.Inconclusive => $"You recorded an inconclusive result for {row.Number}.",
				_ => null,
			};
			if (confirmation is not null)
				SemanticScreenReader.Announce(confirmation);
		}

		RefreshHeader(ScreenPicker.SelectedIndex);
		RenderScreen();
		// After RenderScreen (which overwrites SummaryLabel with the recomputed counts): show the confirmation
		// visibly too, not just to a screen reader, so a sighted tester sees positive feedback that the save
		// went through -- otherwise a confirmed-not-applicable answer's card just silently disappears.
		if (confirmation is not null)
			SummaryLabel.Text = $"{confirmation} {SummaryLabel.Text}";
	}
}
