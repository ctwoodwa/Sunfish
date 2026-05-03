using System.Text;
using Sunfish.Ingestion.Spreadsheets.Importers;
using Xunit;

namespace Sunfish.Ingestion.Spreadsheets.Tests;

public class CsvRowImporterTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public async Task ReadRowsAsync_ParsesHeaderAndRows_Correctly()
    {
        var importer = new CsvRowImporter();
        await using var fs = File.OpenRead(FixturePath("units-small.csv"));

        var rows = new List<IReadOnlyDictionary<string, string>>();
        await foreach (var row in importer.ReadRowsAsync(fs, CancellationToken.None))
            rows.Add(row);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(4, r.Count));
    }

    [Fact]
    public async Task ReadRowsAsync_FieldsMatchHeaderNames()
    {
        var importer = new CsvRowImporter();
        await using var fs = File.OpenRead(FixturePath("units-small.csv"));

        var rows = new List<IReadOnlyDictionary<string, string>>();
        await foreach (var row in importer.ReadRowsAsync(fs, CancellationToken.None))
            rows.Add(row);

        Assert.Equal("A", rows[0]["Building"]);
        Assert.Equal("101", rows[0]["Unit"]);
        Assert.Equal("2", rows[0]["Bedrooms"]);
        Assert.Equal("850", rows[0]["SqFt"]);
    }

    [Fact]
    public async Task ReadRowsAsync_EmptyStream_YieldsNothing()
    {
        var importer = new CsvRowImporter();
        var headerOnly = "Building,Unit,Bedrooms,SqFt\n";
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(headerOnly));

        var count = 0;
        await foreach (var _ in importer.ReadRowsAsync(ms, CancellationToken.None))
            count++;

        Assert.Equal(0, count);
    }
}
