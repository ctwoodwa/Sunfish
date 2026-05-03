// Resolve the ApplicationId ambiguity between this package's
// Models.ApplicationId and System.ApplicationId in favour of the
// domain type. System.ApplicationId is rarely used in any modern .NET
// code path (it's a legacy ClickOnce primitive), so the explicit alias
// is safe and keeps service signatures clean.
global using ApplicationId = Sunfish.Blocks.PropertyLeasingPipeline.Models.ApplicationId;
