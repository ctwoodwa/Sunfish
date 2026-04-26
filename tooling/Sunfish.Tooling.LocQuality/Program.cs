// Plan 3 Task 2.1: CLI scaffold for `sunfish-locquality`.
//
// This file sets up the System.CommandLine root + subcommands. Handlers are
// intentionally stubbed with a clear "not implemented" exit so that follow-on
// Plan 3 tasks (2.4 generate-drafts / MADLAD, 3.x post-edit flagger) can drop
// implementations in without reshaping the command tree.
//
// Subcommands registered here (per Wave 2 Cluster B brief):
//   sunfish-locquality validate <path>
//   sunfish-locquality quality-check <path>
//   sunfish-locquality --help
//   sunfish-locquality --version
//
// Exit codes (sysexits.h-aligned, mirrors Sunfish.Tooling.LocExtraction):
//   0   — success
//   1   — validation / quality-check failure
//   64  — usage error (unknown subcommand, bad args)
//   70  — not implemented yet (scaffold placeholder) — sysexits.h EX_SOFTWARE
//
// MADLAD MT runtime wiring is DEFERRED to a user-driven task — requires
// GPU / Apple-Silicon validation that subagents cannot perform.

using System.CommandLine;

namespace Sunfish.Tooling.LocQuality;

internal static class Program
{
    // sysexits.h-aligned exit codes to give CI a stable contract.
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;
    internal const int ExitUsage = 64;
    internal const int ExitNotImplemented = 70;

    // Single source of truth for the version string emitted by --version.
    // Bump alongside the .csproj <Version> when implementations land.
    internal const string ToolVersion = "v0.1.0-scaffold";
    internal const string ToolName = "Sunfish.Tooling.LocQuality";

    public static async Task<int> Main(string[] args)
    {
        // System.CommandLine 2.0.0-beta4 surfaces --version automatically when
        // <Version> is set in the .csproj, but Wave 2 Cluster B brief specifies
        // an exact version string ("Sunfish.Tooling.LocQuality v0.1.0-scaffold")
        // so we intercept --version before InvokeAsync to honour the contract.
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine($"{ToolName} {ToolVersion}");
            return ExitSuccess;
        }

        var root = BuildRootCommand();
        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    internal static RootCommand BuildRootCommand()
    {
        var root = new RootCommand(
            "Sunfish translator-quality CLI — validates translator-context completeness on XLIFF " +
            "inputs and (deferred) drives MADLAD-400-MT pre-publish draft generation. " +
            "See docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md.");

        root.AddCommand(BuildValidateCommand());
        root.AddCommand(BuildQualityCheckCommand());

        return root;
    }

    // ---------------------------------------------------------------------
    // `sunfish-locquality validate <path>`
    //
    // Will validate translator-context completeness (notes, screenshots,
    // glossary references, ICU placeholder coverage) on an XLIFF 2.0 file or
    // directory tree. Implementation lands in Plan 3 Task 2.x.
    // ---------------------------------------------------------------------
    private static Command BuildValidateCommand()
    {
        var pathArg = new Argument<string>(
            name: "path",
            description: "Path to an XLIFF 2.0 file or a directory containing XLIFF 2.0 files to validate.");

        var cmd = new Command("validate",
            "Validate translator-context completeness on the given XLIFF input. " +
            "Stub today; implementation lands in Plan 3 Task 2.x.")
        {
            pathArg,
        };

        cmd.SetHandler(
            (path) =>
            {
                Console.WriteLine(
                    "[scaffold-stub] LocQuality validate not yet implemented; " +
                    $"tracked at Plan 3 task 2.x. (received path={path})");
                return Task.FromResult(ExitSuccess);
            },
            pathArg);

        return cmd;
    }

    // ---------------------------------------------------------------------
    // `sunfish-locquality quality-check <path>`
    //
    // Will run the post-edit quality heuristic flagger and / or invoke the
    // MADLAD draft-generation pipeline against the given input. Implementation
    // lands in Plan 3 Task 3.x; the MADLAD wiring itself is the deferred
    // user-driven piece.
    // ---------------------------------------------------------------------
    private static Command BuildQualityCheckCommand()
    {
        var pathArg = new Argument<string>(
            name: "path",
            description: "Path to an XLIFF 2.0 file or a directory containing translator output to quality-check.");

        var cmd = new Command("quality-check",
            "Run translator-quality heuristics against the given XLIFF input and (eventually) " +
            "compare against MADLAD draft output. Stub today; will integrate MADLAD draft " +
            "generation per Plan 3 Task 3.x.")
        {
            pathArg,
        };

        cmd.SetHandler(
            (path) =>
            {
                Console.WriteLine(
                    "[scaffold-stub] LocQuality quality-check not yet implemented; " +
                    $"will integrate MADLAD draft generation per Plan 3 task 3.x. (received path={path})");
                return Task.FromResult(ExitSuccess);
            },
            pathArg);

        return cmd;
    }
}
