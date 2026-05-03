using Sunfish.Ingestion.Spreadsheets.Importers;
using Sunfish.Ingestion.Spreadsheets.Tests.Helpers;
using Xunit;

namespace Sunfish.Ingestion.Spreadsheets.Tests;

public class XlsxRowImporterTests
{
    [Fact]
    public async Task ReadRowsAsync_ParsesHeaderAndRows_Correctly()
    {
        var importer = new XlsxRowImporter();
        using var ms = XlsxFixtureBuilder.BuildUnitsSmall();

        var rows = new List<IReadOnlyDictionary<string, string>>();
        await foreach (var row in importer.ReadRowsAsync(ms, CancellationToken.None))
            rows.Add(row);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(4, r.Count));
    }

    [Fact]
    public async Task ReadRowsAsync_FieldsMatchHeaderNames()
    {
        var importer = new XlsxRowImporter();
        using var ms = XlsxFixtureBuilder.BuildUnitsSmall();

        var rows = new List<IReadOnlyDictionary<string, string>>();
        await foreach (var row in importer.ReadRowsAsync(ms, CancellationToken.None))
            rows.Add(row);

        Assert.Equal("A", rows[0]["Building"]);
        Assert.Equal("101", rows[0]["Unit"]);
        Assert.Equal("2", rows[0]["Bedrooms"]);
        Assert.Equal("850", rows[0]["SqFt"]);
    }

    [Fact]
    public async Task ReadRowsAsync_EmptyWorkbook_YieldsNothing()
    {
        var importer = new XlsxRowImporter();
        using var ms = XlsxFixtureBuilder.BuildEmptyWorkbook();

        var count = 0;
        await foreach (var _ in importer.ReadRowsAsync(ms, CancellationToken.None))
            count++;

        Assert.Equal(0, count);
    }
}
