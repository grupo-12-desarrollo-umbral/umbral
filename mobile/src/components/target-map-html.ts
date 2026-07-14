/**
 * Builds the self-contained Leaflet HTML rendered inside the treasure-hunt Map tab's WebView (#156).
 *
 * Why a WebView + Leaflet rather than a native map module: it needs no Google Maps API key and no
 * custom dev-client rebuild, runs the same in Expo Go and the dev client, and keeps the map a pure
 * display surface. Leaflet + OpenStreetMap tiles are loaded from a CDN — a map is inherently online,
 * so this adds no offline capability the tiles didn't already require.
 *
 * The map centres on the active target (`markers[0]`, already ordered by sequence) and drops a pin on
 * every target. Coordinates are context only: there is no geofencing or GPS proximity here.
 */

export type TargetMapMarker = {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
};

const LEAFLET_VERSION = '1.9.4';

// Default single-target zoom. Multiple targets keep this zoom and centre on the active one, honouring
// "centred on the active target's coordinates" rather than fitting a bounds box that would move it.
const DEFAULT_ZOOM = 15;

/**
 * Renders the Leaflet document for a non-empty marker list. The caller (`TargetMap`) is responsible
 * for the empty state, so this assumes at least one marker and centres on the first.
 */
export function buildTargetMapHtml(markers: readonly TargetMapMarker[]): string {
  const center = markers[0];
  // Operator-authored target names reach this string, so serialise through JSON and neutralise `<`
  // so a name can never break out of the <script> block or inject markup.
  const payload = JSON.stringify(markers).replace(/</g, '\\u003c');

  return `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
<link rel="stylesheet" href="https://unpkg.com/leaflet@${LEAFLET_VERSION}/dist/leaflet.css" />
<style>
  html, body, #map { margin: 0; height: 100%; width: 100%; background: #FAF1E5; }
  .target-pin {
    width: 22px; height: 22px; border-radius: 50%;
    background: #E8651D; border: 3px solid #FFF9F0;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.35);
  }
</style>
</head>
<body>
<div id="map"></div>
<script src="https://unpkg.com/leaflet@${LEAFLET_VERSION}/dist/leaflet.js"></script>
<script>
  var targets = ${payload};
  var map = L.map('map', { zoomControl: true }).setView([${center.latitude}, ${center.longitude}], ${DEFAULT_ZOOM});
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap contributors'
  }).addTo(map);
  // A vector div-icon avoids Leaflet's default marker PNGs (whose CDN-relative paths break inside a
  // data/html WebView) and matches the app's ember pin.
  var icon = L.divIcon({ className: '', html: '<div class="target-pin"></div>', iconSize: [22, 22], iconAnchor: [11, 11] });
  targets.forEach(function (t) {
    L.marker([t.latitude, t.longitude], { icon: icon, title: t.name }).addTo(map).bindPopup(t.name);
  });
</script>
</body>
</html>`;
}
