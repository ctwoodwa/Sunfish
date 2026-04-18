using System.Text;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Spreadsheets;
using Sunfish.Ingestion.Spreadsheets.Importers;
using Sunfish.Ingestion.Spreadsheets.Tests.Helpers;
using Xunit;

namespace Sunfish.Ingestion.Spreadsheets.Tests;

public class SpreadsheetIngestionPipelineTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static IngestionContext Ctx() =>
        IngestionContext.NewCorrelation("tenant-1", "actor-1");

    private static SpreadsheetIngestionPipeline Pipeline() =>
        new(new CsvRowImporter(), new XlsxRowImporter());

    private static IReadOnlyList<ColumnMapping> UnitMappings() => new[]
    {
        new ColumnMapping("Building", "building", CoercionKind.String),
        new ColumnMapping("Unit", "unit", CoercionKind.String),
        new ColumnMapping("Bedrooms", "bedrooms", CoercionKind.Integer),
        new ColumnMapping("SqFt", "sqft", CoercionKind.Integer),
    };

    [Fact]
    public async Task Ingest_Csv_HappyPath_ReturnsSessionWith3Events()
    {
        await using var fs = File.OpenRead(FixturePath("units-small.csv"));
        var upload = new SpreadsheetUpload(
            Content: fs,
            Filename: "units-small.csv",
            Kind: SpreadsheetKind.Csv,
            SchemaId: "sunfish.pm.unit/1",
            Mappings: UnitMappings());

        var result = await Pipeline().IngestAsync(upload, Ctx());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value!.Events.Count);
        Assert.Equal(32, result.Value.EntityId.Length);
        Assert.All(result.Value.EntityId, c => Assert.True(Uri.IsHexDigit(c)));
        Assert.Equal("sunfish.ingestion.bulk_import_session/1", result.Value.SchemaId);
        Assert.Equal(3, result.Value.Body["rowCount"]);
        Assert.Equal("units-small.csv", result.Value.Body["filename"]);
    }

    [Fact]
    public async Task Ingest_Xlsx_HappyPath_ReturnsSessionWith3Events()
    {
        using var ms = XlsxFixtureBuilder.BuildUnitsSmall();
        var upload = new SpreadsheetUpload(
            Content: ms,
            Filename: "units-small.xlsx",
            Kind: SpreadsheetKind.Xlsx,
            SchemaId: "sunfish.pm.unit/1",
            Mappings: UnitMappings());

        var result = await Pipeline().IngestAsync(upload, Ctx());

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Events.Count);
        Assert.Equal(3, result.Value.Body["rowCount"]);
    }

    [Fact]
    public async Task Ingest_Csv_WithErrorRow_ReturnsValidationFailed()
    {
        await using var fs = File.OpenRead(FixturePath("units-with-errors.csv"));
        var upload = new SpreadsheetUpload(
            Content: fs,
            Filename: "units-with-errors.csv",
            Kind: SpreadsheetKind.Csv,
            SchemaId: "sunfish.pm.unit/1",
            Mappings: UnitMappings());

        var result = await Pipeline().IngestAsync(upload, Ctx());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ValidationFailed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Contains(result.Failure!.Details, d => d.Contains("Row 2"));
        Assert.Contains(result.Failure.Details, d => d.Contains("bedrooms"));
    }

    [Fact]
    public async Task Ingest_Csv_MissingColumn_ReturnsValidationFailed()
    {
        await using var fs = File.OpenRead(FixturePath("units-small.csv"));
        var mappings = new[]
        {
            new ColumnMapping("Building", "building", CoercionKind.String),
            new ColumnMapping("FloorCount", "floorCount", CoercionKind.Integer),
        };
        var upload = new SpreadsheetUpload(
            Content: fs,
            Filename: "units-small.csv",
            Kind: SpreadsheetKind.Csv,
            SchemaId: "sunfish.pm.unit/1",
            Mappings: mappings);

        var result = await Pipeline().IngestAsync(upload, Ctx());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ValidationFailed, result.Outcome);
        Assert.Contains(result.Failure!.Details, d => d.Contains("FloorCount"));
    }

    [Fact]
    public async Task Ingest_EmptyCsv_ReturnsEmptySession()
    {
        var headerOnly = "Building,Unit,Bedrooms,SqFt\n";
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(headerOnly));
        var upload = new SpreadsheetUpload(
            Content: ms,
            Filename: "empty.csv",
            Kind: SpreadsheetKind.Csv,
            SchemaId: "sunfish.pm.unit/1",
            Mappings: UnitMappings());

        var result = await Pipeline().IngestAsync(upload, Ctx());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Events);
        Assert.Equal(0, result.Value.Body["rowCount"]);
    }
}
