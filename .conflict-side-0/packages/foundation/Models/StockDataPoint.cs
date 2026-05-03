namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents a single OHLC candlestick data point.
/// </summary>
public class StockDataPoint
{
    public DateTime Date { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
}
