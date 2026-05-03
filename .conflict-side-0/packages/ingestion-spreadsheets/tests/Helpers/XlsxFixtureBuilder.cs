using ClosedXML.Excel;

namespace Sunfish.Ingestion.Spreadsheets.Tests.Helpers;

internal static class XlsxFixtureBuilder
{
    public static MemoryStream BuildUnitsSmall()
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Units");
        ws.Cell(1, 1).Value = "Building"; ws.Cell(1, 2).Value = "Unit";
        ws.Cell(1, 3).Value = "Bedrooms"; ws.Cell(1, 4).Value = "SqFt";
        ws.Cell(2, 1).Value = "A"; ws.Cell(2, 2).Value = "101"; ws.Cell(2, 3).Value = 2; ws.Cell(2, 4).Value = 850;
        ws.Cell(3, 1).Value = "A"; ws.Cell(3, 2).Value = "102"; ws.Cell(3, 3).Value = 1; ws.Cell(3, 4).Value = 650;
        ws.Cell(4, 1).Value = "B"; ws.Cell(4, 2).Value = "201"; ws.Cell(4, 3).Value = 3; ws.Cell(4, 4).Value = 1100;
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    public static MemoryStream BuildEmptyWorkbook()
    {
        var wb = new XLWorkbook();
        wb.Worksheets.Add("Empty");
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }
}
