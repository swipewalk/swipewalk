using Swipewalk.Core.Coverage;
using Swipewalk.Core.Model;
using Swipewalk.Core.Reports;
using Swipewalk.Engine;

namespace Swipewalk.Cli;

/// <summary>
/// <c>swipewalk guide &lt;run&gt;</c>: walks a saved run's screens and asks the numbered guided-check
/// questions from <see cref="GuidedStepsCatalog"/>, saving each answer immediately to guided-answers.json (see
/// <see cref="GuidedAnswerStore"/>) so the session can be interrupted and resumed by running the command
/// again. This command only reads a saved run and writes the answers sidecar next to it -- it doesn't touch a
/// device -- so it isn't subject to the real-device verification rule, only to the usual test-runner build/test
/// checks.
/// </summary>
internal static class GuidedCheckCli
{
    /// <summary>
    /// Set only by this command's own scripted-input tests (<c>GuidedCheckCliTests</c>), which must redirect
    /// stdin to feed canned answers -- indistinguishable, from <see cref="Console.IsInputRedirected"/> alone,
    /// from a real non-interactive/CI run piping input by accident. Not a documented CLI option: a real person
    /// or script has no reason to set it, and Usage never mentions it.
    /// </summary>
    private const string AllowRedirectedInputForTests = "SWIPEWALK_GUIDE_TEST_ALLOW_REDIRECTED_INPUT";

    public static async Task<int> RunAsync(string[] args)
    {
        if (Console.IsInputRedirected && Environment.GetEnvironmentVariable(AllowRedirectedInputForTests) != "1")
        {
            Console.Error.WriteLine(
                "swipewalk guide needs an interactive terminal to ask its questions. Use the desktop app's " +
                "Guided checks page instead, or run this in a terminal (not piped/redirected input).");
            return 1;
        }

        var (target, options) = ParseArgs(args);
        if (target is null)
        {
            Console.Error.WriteLine("Usage: swipewalk guide <run-id-or-folder> [--screen <name>] [--criterion <number>] [--history <dir>]");
            return 1;
        }

        var history = new RunHistory(options.GetValueOrDefault("history"));
        var run = history.Resolve(target);
        if (run is null)
        {
            Console.Error.WriteLine($"No saved run found for '{target}'. See: swipewalk history.");
            return 1;
        }
        var report = RunHistory.Load(run);
        if (report is null)
        {
            Console.Error.WriteLine($"Could not read results.json for '{target}' (missing or damaged).");
            return 1;
        }

        // RunHistory.Load already merges guided-answers.json into report.GuidedAnswers (see its own remarks),
        // so report.ScreensWithNoGuidedAnswers/ScreenCoverage/Contradictions below reflect what's on disk.
        var store = GuidedAnswerStore.Load(run.Folder) ?? GuidedAnswerStore.Empty;
        var answers = report.GuidedAnswers.ToList();

        var screens = report.Screens.AsEnumerable();
        if (options.GetValueOrDefault("screen") is { } screenFilter)
            screens = screens.Where(s => s.ScreenName.Contains(screenFilter, StringComparison.OrdinalIgnoreCase));
        var screenList = screens.ToList();
        if (screenList.Count == 0)
        {
            Console.Error.WriteLine(options.ContainsKey("screen")
                ? $"No scanned screen matches '{options["screen"]}'."
                : "This run has no scanned screens.");
            return 1;
        }

        var criterionFilter = options.GetValueOrDefault("criterion");

        foreach (var line in GuidedStepsCatalog.BeforeYouStart)
            Console.WriteLine($"- {line}");
        Console.WriteLine();

        var noAnswersYet = report.ScreensWithNoGuidedAnswers.Select(s => s.ScreenId).ToHashSet();
        if (!options.ContainsKey("screen") && noAnswersYet.Count > 0)
            Console.WriteLine(GuidedChecksDisplay.ScreensWithNoGuidedAnswers(noAnswersYet.Count, report.Screens.Count));

        var assistiveTechnology = AskAssistiveTechnology();

        foreach (var screen in screenList)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {screen.ScreenName} ({screen.Platform}) ===");

            var coverage = ScreenCoverageBuilder.Build(report.Screens, answers)
                .Where(r => r.ScreenId == screen.ScreenId)
                .Where(r => r.Status is ScreenCriterionStatus.GuidedChecked or ScreenCriterionStatus.PartlyCheckedAutomatically or ScreenCriterionStatus.NotTested)
                .Where(r => criterionFilter is null || r.Number == criterionFilter)
                .Where(r => criterionFilter is not null || GuidedStepsCatalog.For(r.Number) is not null)
                // A criterion already answered in an earlier session is skipped by default when resuming (no
                // --criterion given), so running the command again picks up where it left off rather than
                // re-asking everything; --criterion always shows it, for a deliberate re-answer.
                .Where(r => criterionFilter is not null || r.Answer is null)
                .ToList();

            if (coverage.Count == 0)
            {
                Console.WriteLine("Nothing to ask on this screen (no matching criteria, or everything here is confirmed not applicable).");
                continue;
            }

            foreach (var row in coverage)
            {
                var newAnswer = AskCriterion(screen, row, assistiveTechnology);
                if (newAnswer is null)
                    continue; // "skip for now": nothing recorded
                answers.Add(newAnswer);
                store = store.With(newAnswer);
                await store.SaveAsync(run.Folder);

                if (newAnswer.Result == GuidedAnswerResult.Pass)
                    Console.WriteLine(GuidedChecksDisplay.TesterFacingPass(string.Join("; ", newAnswer.Evidence.Select(e => e.Description))));

                var contradiction = ContradictionChecker.Find(report.Screens, answers)
                    .FirstOrDefault(c => c.ScreenId == screen.ScreenId && c.CriterionNumber == row.Number && c.Answer == newAnswer);
                if (contradiction is not null)
                    Console.WriteLine($"** {contradiction.Description}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Done. Answers are saved in this run's folder (guided-answers.json), not shared -- run `swipewalk guide` again any time to add more.");
        return 0;
    }

    private static string AskAssistiveTechnology()
    {
        Console.Write("Which are you testing with? [T]alkBack / [V]oiceOver / [H]ardware keyboard / [N]one > ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();
        return choice switch
        {
            "T" => "TalkBack",
            "V" => "VoiceOver",
            "H" => "Hardware keyboard",
            _ => "None",
        };
    }

    private static GuidedAnswer? AskCriterion(ScreenResult screen, ScreenCriterionReport row, string assistiveTechnology)
    {
        var step = GuidedStepsCatalog.For(row.Number);
        Console.WriteLine();
        var sourceUrl = row.SourceUrls.Count > 0 ? $" -- {row.SourceUrls[0]}" : "";
        Console.WriteLine($"{row.Number} {row.Name} ({row.Level}){sourceUrl}");
        if (row.AutomatedSummary is not null)
            Console.WriteLine($"  {row.AutomatedSummary}");
        if (row.CapturedEvidenceSummary is not null && row.CapturedEvidenceSource is { } evidenceSource)
            Console.WriteLine($"  Captured evidence ({Swipewalk.Core.ScreenReader.ScreenReaderCaptureComparer.ToolLabel(evidenceSource)}): {row.CapturedEvidenceSummary}");
        foreach (var proposal in row.ProposedButNotConfirmed)
            Console.WriteLine($"  Swipewalk suggests this may not apply here: {proposal.Reason}");
        if (row.Answer is not null)
        {
            var existing = row.Answer.Result == GuidedAnswerResult.Pass
                ? GuidedChecksDisplay.ReportFacingPass(row.Number, screen.ScreenName, string.Join("; ", row.Answer.Evidence.Select(e => e.Description)))
                : $"Already recorded: {GuidedChecksDisplay.ResultWord(row.Answer.Result)} on {row.Answer.AnsweredAt:yyyy-MM-dd} by {row.Answer.Tester}.";
            Console.WriteLine($"  {existing}");
            if (screen.RescannedAt is { } rescannedAt && row.Answer.AnsweredAt < rescannedAt)
                Console.WriteLine($"  {GuidedChecksDisplay.AnsweredBeforeRescan(rescannedAt)}");
        }

        if (step is not null)
        {
            Console.WriteLine($"  {step.RecordPrompt}");
            // "None"/an unrecognized answer falls back to the platform's own screen reader, not always
            // TalkBack -- an iOS run showing "Turn on TalkBack" would be actively wrong on an iPhone.
            var platformDefault = screen.Platform == Platform.iOS ? step.VoiceOverSteps : step.TalkBackSteps;
            var steps = assistiveTechnology switch
            {
                "TalkBack" => step.TalkBackSteps,
                "VoiceOver" => step.VoiceOverSteps,
                "Hardware keyboard" => step.HardwareKeyboardSteps ?? platformDefault,
                _ => platformDefault,
            };
            for (var i = 0; i < steps.Count; i++)
                Console.WriteLine($"  {i + 1}. {steps[i]}");
        }
        else
        {
            var howToCheck = CoverageCatalog.All.FirstOrDefault(c => c.Criterion.Number == row.Number)?.Note;
            Console.WriteLine(howToCheck is null
                ? "  (No step-by-step script yet for this criterion.)"
                : $"  No step-by-step script yet for this criterion. How to check it by hand: {howToCheck}");
        }

        var askedAt = DateTimeOffset.UtcNow;
        Console.Write("Result? [p]ass / [f]ail / [i]nconclusive / [c]onfirm not applicable / [s]kip for now > ");
        var choice = Console.ReadLine()?.Trim().ToLowerInvariant();

        GuidedAnswerResult result;
        string? notApplicableReason = null;
        IReadOnlyList<GuidedEvidence> evidence = [];
        switch (choice)
        {
            case "p":
                result = GuidedAnswerResult.Pass;
                string description;
                do
                {
                    Console.Write(GuidedChecksDisplay.EvidencePrompt);
                    description = Console.ReadLine()?.Trim() ?? "";
                } while (string.IsNullOrEmpty(description));
                evidence = [new GuidedEvidence(description)];
                break;
            case "f":
                result = GuidedAnswerResult.Fail;
                break;
            case "i":
                result = GuidedAnswerResult.Inconclusive;
                break;
            case "c":
                result = GuidedAnswerResult.ConfirmedNotApplicable;
                var suggested = row.ProposedButNotConfirmed.Count > 0 ? row.ProposedButNotConfirmed[0].Reason : null;
                var prompt = suggested is null
                    ? GuidedChecksDisplay.NotApplicableReasonPrompt
                    : $"{GuidedChecksDisplay.NotApplicableReasonPrompt}(Enter to accept: \"{suggested}\") ";
                Console.Write(prompt);
                var reasonInput = Console.ReadLine()?.Trim();
                notApplicableReason = string.IsNullOrEmpty(reasonInput) ? suggested : reasonInput;
                while (string.IsNullOrWhiteSpace(notApplicableReason))
                {
                    Console.Write(GuidedChecksDisplay.NotApplicableReasonPrompt);
                    notApplicableReason = Console.ReadLine()?.Trim();
                }
                break;
            default:
                return null; // "skip for now" (or any unrecognized input): nothing recorded
        }

        var fastPassConfirmed = false;
        if (result == GuidedAnswerResult.Pass && DateTimeOffset.UtcNow - askedAt < TimeSpan.FromSeconds(5))
        {
            Console.Write($"{GuidedChecksDisplay.FastPassPrompt} [y/n] > ");
            if (Console.ReadLine()?.Trim().ToLowerInvariant() != "y")
            {
                Console.WriteLine("Not saved.");
                return null;
            }
            fastPassConfirmed = true;
        }

        Console.Write("Note (optional, Enter to skip) > ");
        var note = Console.ReadLine()?.Trim();

        Console.Write("Tester name/initials (Enter for \"me\") > ");
        var tester = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(tester))
            tester = "me";

        return new GuidedAnswer(
            ScreenId: screen.ScreenId,
            CriterionNumber: row.Number,
            Result: result,
            Note: string.IsNullOrEmpty(note) ? null : note,
            NotApplicableReason: notApplicableReason,
            FastPassConfirmed: fastPassConfirmed,
            Tester: tester,
            AnsweredAt: DateTimeOffset.Now,
            Device: screen.Device?.Model ?? screen.Device?.Name,
            AssistiveTechnology: assistiveTechnology,
            AssistiveTechnologyVersion: null,
            Evidence: evidence);
    }

    private static (string? Target, Dictionary<string, string> Options) ParseArgs(string[] args)
    {
        string? target = null;
        var options = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                options[args[i][2..]] = hasValue ? args[++i] : "true";
            }
            else if (target is null)
            {
                target = args[i];
            }
        }
        return (target, options);
    }
}
