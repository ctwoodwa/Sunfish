// Plan 3 Task 1.1: CLI scaffold for `sunfish-loc`.
//
// This file sets up the System.CommandLine root + subcommands. Handlers are
// intentionally stubbed with a clear "not implemented" exit so that follow-on
// tasks (1.2 extract, 1.4 validate-placeholders) can drop implementations in
// without reshaping the command tree.
//
// Subcommands registered here:
//   sunfish-loc extract [--staged] [--input <path>] [--output <path>]
//   sunfish-loc validate-placeholders --input <path>
//   sunfish-loc --help
//
// Exit codes:
//   0   — success
//   1   — validation / extraction failure (block the pre-commit hook)
//   64  — usage error (unknown subcommand, bad args) — per sysexits.h convention
//   70  — not implemented yet (scaffold placeholder) — per sysexits.h EX_SOFTWARE

using System.CommandLine;

namespace Sunfish.Tooling.LocExtraction;

internal static class Program
{
    // sysexits.h-aligned exit codes to give CI / Husky a stable contract.
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;
    internal const int ExitUsage = 64;
    internal const int ExitNotImplemented = 70;

    public static async Task<int> Main(string[] args)
    {
        var root = BuildRootCommand();
        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    internal static RootCommand BuildRootCommand()
    {
        var root = new RootCommand(
            "Sunfish localization CLI — extracts RESX into XLIFF 2.0 drafts and validates ICU placeholder preservation. " +
            "Invoked by the Husky.NET pre-commit hook and by CI on staged .resx edits.");

        root.AddCommand(BuildExtractCommand());
        root.AddCommand(BuildValidatePlaceholdersCommand());

        return root;
    }

    // ---------------------------------------------------------------------
    // `sunfish-loc extract`
    // Implementation lands in Plan 3 Task 1.2 (ResxToXliffDraftExtractor.cs).
    // ---------------------------------------------------------------------
    private static Command BuildExtractCommand()
    {
        var stagedOption = new Option<bool>(
            name: "--staged",
            description: "Operate on RESX files that git has staged for commit (pre-commit hook mode).");

        var inputOption = new Option<FileInfo?>(
            name: "--input",
            description: "Path to a specific RESX file to extract. Mutually exclusive with --staged.");

        var outputOption = new Option<DirectoryInfo?>(
            name: "--output",
            description: "XLIFF output directory. Defaults to localization/xliff/ relative to the repo root.");

        var cmd = new Command("extract",
            "Extract staged or specified .resx files into XLIFF 2.0 drafts. " +
            "Preserves existing translator work (state=\"translated\" / \"final\") and marks removed keys as state=\"obsolete\".")
        {
            stagedOption,
            inputOption,
            outputOption,
        };

        cmd.SetHandler(
            (staged, input, output) =>
            {
                Console.Error.WriteLine(
                    "sunfish-loc extract: not yet implemented (Plan 3 Task 1.2 scaffold). " +
                    $"Received --staged={staged}, --input={input?.FullName ?? "<null>"}, --output={output?.FullName ?? "<null>"}.");
                return Task.FromResult(ExitNotImplemented);
            },
            stagedOption, inputOption, outputOption);

        return cmd;
    }

    // ---------------------------------------------------------------------
    // `sunfish-loc validate-placeholders`
    // Implementation lands in Plan 3 Task 1.4 (PlaceholderValidator.cs).
    // ---------------------------------------------------------------------
    private static Command BuildValidatePlaceholdersCommand()
    {
        var inputOption = new Option<FileInfo?>(
            name: "--input",
            description: "Path to an XLIFF 2.0 file to validate. Required.")
        {
            IsRequired = true,
        };

        var cmd = new Command("validate-placeholders",
            "Validate that ICU placeholders ({name}, {name, plural, ...}, etc.) are preserved in every target unit. " +
            "Fails non-zero if any placeholder is dropped, renamed, or has mismatched braces.")
        {
            inputOption,
        };

        cmd.SetHandler(
            (input) =>
            {
                Console.Error.WriteLine(
                    "sunfish-loc validate-placeholders: not yet implemented (Plan 3 Task 1.4 scaffold). " +
                    $"Received --input={input?.FullName ?? "<null>"}.");
                return Task.FromResult(ExitNotImplemented);
            },
            inputOption);

        return cmd;
    }
}
