; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SUNFISH_WAYFINDER001 | SunfishWayfinder | Warning | AddSunfish*() registration in a project that does not declare any AtlasSchemaDescriptor, SchemaRegistrationAnalyzer (ADR 0065 / W#42 P3b)
