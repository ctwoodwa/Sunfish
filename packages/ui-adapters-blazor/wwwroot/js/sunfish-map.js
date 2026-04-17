// sunfish-map.js — MapLibre GL JS adapter for SunfishMap component.
// Loaded as an ESM module via JS interop from MapLibreAdapter.cs.
// MapLibre GL JS is loaded from CDN (not bundled).

const MAPLIBRE_JS_URL = 'https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.js';
const MAPLIBRE_CSS_URL = 'https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.css';

let _mapInstance = null;
let _dotNetRef = null;
let _loadPromise = null;
let _loadError = null;

/**
 * Ensure MapLibre GL JS is loaded (global script + CSS).
 * Checks for window.maplibregl first; if missing, injects <script> and <link>.
 * @returns {Promise<void>}
 */
function ensureMapLibreLoaded() {
    if (window.maplibregl) return Promise.resolve();
    if (_loadError) return Promise.reject(_loadError);
    if (_loadPromise) return _loadPromise;

    _loadPromise = new Promise((resolve, reject) => {
        // Inject CSS if not already present.
        if (!document.querySelector(`link[href="${MAPLIBRE_CSS_URL}"]`)) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = MAPLIBRE_CSS_URL;
            document.head.appendChild(link);
        }

        // Inject script if not already present.
        const existingScript = document.querySelector(`script[src="${MAPLIBRE_JS_URL}"]`);
        if (existingScript) {
            // Script tag exists but may still be loading.
            if (window.maplibregl) {
                resolve();
                return;
            }
            existingScript.addEventListener('load', () => resolve());
            existingScript.addEventListener('error', (e) => {
                _loadError = new Error('Failed to load MapLibre GL JS from CDN.');
                reject(_loadError);
            });
            return;
        }

        const script = document.createElement('script');
        script.src = MAPLIBRE_JS_URL;
        script.async = true;
        script.addEventListener('load', () => resolve());
        script.addEventListener('error', () => {
            _loadError = new Error('Failed to load MapLibre GL JS from CDN.');
            reject(_loadError);
        });
        document.head.appendChild(script);
    });

    return _loadPromise;
}

/**
 * Show a graceful fallback error message in the container.
 * Uses safe DOM methods (no innerHTML).
 * @param {HTMLElement} element - Container element.
 */
function showFallbackError(element) {
    const outer = document.createElement('div');
    outer.style.cssText = 'display:flex;align-items:center;justify-content:center;width:100%;height:100%;' +
        'background:#f8d7da;color:#721c24;font-family:sans-serif;font-size:14px;padding:1rem;box-sizing:border-box;';

    const inner = document.createElement('div');
    inner.style.textAlign = 'center';

    const heading = document.createElement('div');
    heading.style.cssText = 'font-weight:600;margin-bottom:0.5rem;';
    heading.textContent = 'Map unavailable';

    const message = document.createElement('div');
    message.textContent = 'Could not load MapLibre GL JS. Check your network connection.';

    inner.appendChild(heading);
    inner.appendChild(message);
    outer.appendChild(inner);
    element.appendChild(outer);
}

/**
 * Initialize a MapLibre map in the given container element.
 * @param {HTMLElement} element - Container element (Blazor ElementReference).
 * @param {object} dotNetRef - DotNetObjectReference for event callbacks.
 * @param {object} options - Map configuration options.
 */
export async function init(element, dotNetRef, options) {
    _dotNetRef = dotNetRef;

    try {
        await ensureMapLibreLoaded();
    } catch (err) {
        // Graceful fallback: show error message in the container.
        showFallbackError(element);
        return;
    }

    // Destroy previous instance on same container (hot-reload safety).
    if (_mapInstance) {
        try { _mapInstance.remove(); } catch (_) { }
        _mapInstance = null;
    }

    const maplibregl = window.maplibregl;

    _mapInstance = new maplibregl.Map({
        container: element,
        style: {
            version: 8,
            sources: {},
            layers: [
                { id: '_background', type: 'background', paint: { 'background-color': '#e8e8e8' } }
            ]
        },
        center: [options.centerLng, options.centerLat],
        zoom: options.zoom,
        minZoom: options.minZoom,
        maxZoom: options.maxZoom,
        scrollZoom: options.zoomable !== false,
        dragPan: options.pannable !== false,
        attributionControl: true
    });

    if (options.zoomable !== false) {
        _mapInstance.addControl(new maplibregl.NavigationControl(), 'top-right');
    }

    // Wire events back to .NET.
    _mapInstance.on('click', (e) => {
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnMapClickFromJs', e.lngLat.lat, e.lngLat.lng);
        }
    });

    _mapInstance.on('zoomend', () => {
        if (_dotNetRef) {
            const center = _mapInstance.getCenter();
            _dotNetRef.invokeMethodAsync('OnZoomEndFromJs', _mapInstance.getZoom(), center.lat, center.lng);
        }
    });

    _mapInstance.on('moveend', () => {
        if (_dotNetRef) {
            const center = _mapInstance.getCenter();
            _dotNetRef.invokeMethodAsync('OnPanEndFromJs', center.lat, center.lng);
        }
    });
}

/**
 * Update the map viewport (center and zoom).
 * @param {number} centerLat
 * @param {number} centerLng
 * @param {number} zoom
 */
export function updateViewport(centerLat, centerLng, zoom) {
    if (!_mapInstance) return;
    _mapInstance.jumpTo({ center: [centerLng, centerLat], zoom: zoom });
}

/**
 * Translate a Sunfish URL template to MapLibre's tiles array format.
 * Sunfish uses {s}/{z}/{x}/{y}. MapLibre uses {z}/{x}/{y} and expects
 * multiple tile URLs for subdomains (one per subdomain value).
 * @param {string} urlTemplate
 * @param {string[]|null} subdomains
 * @returns {string[]}
 */
function translateTileUrl(urlTemplate, subdomains) {
    if (!urlTemplate) return [];
    if (subdomains && subdomains.length > 0 && urlTemplate.includes('{s}')) {
        return subdomains.map(s => urlTemplate.replace('{s}', s));
    }
    // No subdomains — return as single URL (remove {s} if present).
    return [urlTemplate.replace('{s}', '')];
}

/**
 * Build GeoJSON FeatureCollection from data items for marker/bubble layers.
 * @param {Array} data - Array of data objects.
 * @param {string} locationField - Property containing [lat, lng] array.
 * @param {string|null} titleField - Property for title (marker layers).
 * @param {string|null} valueField - Property for numeric value (bubble layers).
 * @returns {object} GeoJSON FeatureCollection.
 */
function buildGeoJsonFromData(data, locationField, titleField, valueField) {
    const features = [];
    if (!data || !Array.isArray(data) || !locationField) return { type: 'FeatureCollection', features };

    for (const item of data) {
        const loc = item[locationField];
        if (!loc || !Array.isArray(loc) || loc.length < 2) continue;

        const props = {};
        if (titleField && item[titleField] != null) props.title = item[titleField];
        if (valueField && item[valueField] != null) props.value = Number(item[valueField]);

        features.push({
            type: 'Feature',
            geometry: { type: 'Point', coordinates: [loc[1], loc[0]] }, // [lng, lat]
            properties: props
        });
    }
    return { type: 'FeatureCollection', features };
}

/**
 * Add a layer to the map.
 * @param {object} layer - MapLayerDescriptor serialized from C#.
 */
export function addLayer(layer) {
    if (!_mapInstance || !layer) return;

    const id = layer.id;
    const sourceId = id + '-source';

    switch (layer.type) {
        case 0: // Tile
            addTileLayer(id, sourceId, layer);
            break;
        case 1: // Marker
            addMarkerLayer(id, sourceId, layer);
            break;
        case 2: // Shape
            addShapeLayer(id, sourceId, layer);
            break;
        case 3: // Bubble
            addBubbleLayer(id, sourceId, layer);
            break;
    }
}

function addTileLayer(id, sourceId, layer) {
    const tiles = translateTileUrl(layer.urlTemplate, layer.subdomains);
    if (tiles.length === 0) return;

    _mapInstance.addSource(sourceId, {
        type: 'raster',
        tiles: tiles,
        tileSize: 256,
        attribution: layer.attribution || ''
    });

    _mapInstance.addLayer({
        id: id,
        type: 'raster',
        source: sourceId,
        minzoom: layer.minZoom ?? 0,
        maxzoom: layer.maxZoom ?? 22,
        paint: {
            'raster-opacity': layer.opacity ?? 1.0
        }
    });
}

function addMarkerLayer(id, sourceId, layer) {
    const geojson = buildGeoJsonFromData(layer.data, layer.locationField, layer.titleField, null);

    _mapInstance.addSource(sourceId, {
        type: 'geojson',
        data: geojson
    });

    // Use circle layer as a simple marker visualization.
    // A full symbol layer would require loading sprite images.
    _mapInstance.addLayer({
        id: id,
        type: 'circle',
        source: sourceId,
        minzoom: layer.minZoom ?? 0,
        maxzoom: layer.maxZoom ?? 22,
        paint: {
            'circle-radius': 8,
            'circle-color': '#d32f2f',
            'circle-stroke-width': 2,
            'circle-stroke-color': '#ffffff',
            'circle-opacity': layer.opacity ?? 1.0
        }
    });

    // Add title labels if the data has titles.
    _mapInstance.addLayer({
        id: id + '-labels',
        type: 'symbol',
        source: sourceId,
        minzoom: layer.minZoom ?? 0,
        maxzoom: layer.maxZoom ?? 22,
        layout: {
            'text-field': ['get', 'title'],
            'text-offset': [0, 1.5],
            'text-anchor': 'top',
            'text-size': 12
        },
        paint: {
            'text-color': '#333333',
            'text-halo-color': '#ffffff',
            'text-halo-width': 1
        }
    });

    // Wire click event for markers.
    _mapInstance.on('click', id, (e) => {
        if (_dotNetRef && e.features && e.features.length > 0) {
            const props = e.features[0].properties;
            const coords = e.features[0].geometry.coordinates;
            _dotNetRef.invokeMethodAsync('OnMarkerClickFromJs',
                props.title || '', coords[1], coords[0]);
        }
    });

    // Change cursor on hover.
    _mapInstance.on('mouseenter', id, () => { _mapInstance.getCanvas().style.cursor = 'pointer'; });
    _mapInstance.on('mouseleave', id, () => { _mapInstance.getCanvas().style.cursor = ''; });
}

function addShapeLayer(id, sourceId, layer) {
    let geojsonData;
    if (layer.geoJsonData) {
        try {
            geojsonData = JSON.parse(layer.geoJsonData);
        } catch (_) {
            geojsonData = { type: 'FeatureCollection', features: [] };
        }
    } else {
        geojsonData = { type: 'FeatureCollection', features: [] };
    }

    _mapInstance.addSource(sourceId, {
        type: 'geojson',
        data: geojsonData
    });

    const fillColor = layer.style?.fillColor || '#3388ff';
    const fillOpacity = layer.style?.fillOpacity ?? (layer.opacity ?? 0.5);
    const strokeColor = layer.style?.strokeColor || '#3388ff';
    const strokeWidth = layer.style?.strokeWidth ?? 2;

    _mapInstance.addLayer({
        id: id + '-fill',
        type: 'fill',
        source: sourceId,
        minzoom: layer.minZoom ?? 0,
        maxzoom: layer.maxZoom ?? 22,
        paint: {
            'fill-color': fillColor,
            'fill-opacity': fillOpacity
        }
    });

    _mapInstance.addLayer({
        id: id + '-line',
        type: 'line',
        source: sourceId,
        minzoom: layer.minZoom ?? 0,
        maxzoom: layer.maxZoom ?? 22,
        paint: {
            'line-color': strokeColor,
            'line-width': strokeWidth,
            'line-opacity': layer.opacity ?? 1.0
        }
    });

    // Wire click for shapes.
    _mapInstance.on('click', id + '-fill', (e) => {
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnShapeClickFromJs', null);
        }
    });
}

function addBubbleLayer(id, sourceId, layer) {
    const geojson = buildGeoJsonFromData(layer.data, layer.locationField, null, layer.valueField);

    _mapInstance.addSource(sourceId, {
        type: 'geojson',
        data: geojson
    });

    // Compute min/max values for radius interpolation.
    const values = geojson.features.map(f => f.properties.value).filter(v => v != null);
    const minVal = values.length > 0 ? Math.min(...values) : 0;
    const maxVal = values.length > 0 ? Math.max(...values) : 1;
    const minSize = layer.style?.minSize ?? 5;
    const maxSize = layer.style?.maxSize ?? 40;

    let radiusExpr;
    if (minVal === maxVal) {
        radiusExpr = (minSize + maxSize) / 2;
    } else {
        // Data-driven radius via interpolate expression.
        radiusExpr = [
            'interpolate', ['linear'], ['get', 'value'],
            minVal, minSize,
            maxVal, maxSize
        ];
    }

    const fillColor = layer.style?.fillColor || '#0066ff';

    _mapInstance.addLayer({
        id: id,
        type: 'circle',
        source: sourceId,
        minzoom: layer.minZoom ?? 0,
        maxzoom: layer.maxZoom ?? 22,
        paint: {
            'circle-radius': radiusExpr,
            'circle-color': fillColor,
            'circle-opacity': layer.opacity ?? 0.6,
            'circle-stroke-width': 1,
            'circle-stroke-color': '#ffffff'
        }
    });

    // Wire click for bubbles.
    _mapInstance.on('click', id, (e) => {
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnShapeClickFromJs', null);
        }
    });

    _mapInstance.on('mouseenter', id, () => { _mapInstance.getCanvas().style.cursor = 'pointer'; });
    _mapInstance.on('mouseleave', id, () => { _mapInstance.getCanvas().style.cursor = ''; });
}

/**
 * Remove a layer (and its source) from the map.
 * @param {string} layerId
 */
export function removeLayer(layerId) {
    if (!_mapInstance) return;

    const sourceId = layerId + '-source';

    // Remove all sub-layers that belong to this logical layer.
    const suffixes = ['', '-labels', '-fill', '-line'];
    for (const suffix of suffixes) {
        const lid = layerId + suffix;
        if (_mapInstance.getLayer(lid)) {
            _mapInstance.removeLayer(lid);
        }
    }

    if (_mapInstance.getSource(sourceId)) {
        _mapInstance.removeSource(sourceId);
    }
}

/**
 * Update an existing layer by removing and re-adding it.
 * @param {object} layer - MapLayerDescriptor serialized from C#.
 */
export function updateLayer(layer) {
    if (!_mapInstance || !layer) return;
    removeLayer(layer.id);
    addLayer(layer);
}

/**
 * Destroy the map instance and clean up.
 */
export function destroy() {
    if (_mapInstance) {
        try { _mapInstance.remove(); } catch (_) { }
        _mapInstance = null;
    }
    _dotNetRef = null;
    _loadError = null;
}
