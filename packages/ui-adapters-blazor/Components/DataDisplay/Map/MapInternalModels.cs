using Sunfish.Foundation.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Map;

/// <summary>
/// Options passed to the JS engine adapter during initialization.
/// </summary>
internal record MapInitOptions(
    double CenterLat,
    double CenterLng,
    double Zoom,
    double MinZoom,
    double MaxZoom,
    bool Zoomable,
    bool Pannable);

/// <summary>
/// Describes a single map layer to the JS engine adapter.
/// Translates from the public MapLayer component parameters into a flat DTO
/// suitable for JS interop serialization.
/// </summary>
internal record MapLayerDescriptor(
    string Id,
    MapLayerType Type,
    string? UrlTemplate,
    string[]? Subdomains,
    string? Attribution,
    string? GeoJsonData,
    object? Data,
    string? LocationField,
    string? TitleField,
    string? ValueField,
    double Opacity,
    double MinZoom,
    double MaxZoom,
    MapLayerStyleDescriptor? Style);

/// <summary>
/// Style information for shape and bubble layers.
/// </summary>
internal record MapLayerStyleDescriptor(
    string? FillColor,
    double? FillOpacity,
    string? StrokeColor,
    double? StrokeWidth,
    double? MinSize,
    double? MaxSize);
