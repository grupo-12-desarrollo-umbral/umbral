// Builds the self-contained Leaflet document rendered inside the mission-editor map (#156, operator
// side). Mirrors the mobile play-surface builder (mobile/src/components/target-map-html.ts) so operator
// and participant see the same pins, but adds an interactive "pick" mode: clicking the map moves a draft
// marker and posts the chosen coordinates back to the parent window, which drives the lat/lng inputs.
//
// Rendered in an <iframe srcDoc>. Leaflet + OpenStreetMap tiles load from a CDN — a map is inherently
// online, so this adds no offline requirement the tiles didn't already have. QR scanning remains the sole
// source of truth for target resolution; coordinates are display/context only (no geofencing).

export type TargetMapMarker = {
  id: string
  name: string
  latitude: number
  longitude: number
}

export type TargetMapDraft = {
  latitude: number
  longitude: number
}

// Message channel between the iframe and the React host. The host filters window 'message' events on
// this type before trusting the payload.
export const TARGET_PICK_MESSAGE = 'umbral:target-pick'

const LEAFLET_VERSION = '1.9.4'

// Zoom when a single point anchors the view. A world view (nothing placed yet) starts zoomed out so the
// operator can navigate to the real location.
const PLACED_ZOOM = 15
const WORLD_ZOOM = 2

export type BuildTargetMapOptions = {
  // Context pins: the substage's already-placed targets. Rendered muted so the draft stands out.
  markers?: readonly TargetMapMarker[]
  // The coordinate being authored (the pin that click-to-pick moves). Highlighted.
  draft?: TargetMapDraft | null
  // When true, clicking the map posts a TARGET_PICK_MESSAGE and moves the draft marker.
  interactive?: boolean
}

// Chooses the initial centre/zoom: the draft if present, else the first context marker, else a
// zoomed-out world view so an operator with nothing placed can still navigate.
function resolveView(
  markers: readonly TargetMapMarker[],
  draft: TargetMapDraft | null,
): { lat: number; lng: number; zoom: number } {
  if (draft) return { lat: draft.latitude, lng: draft.longitude, zoom: PLACED_ZOOM }
  if (markers.length > 0) return { lat: markers[0].latitude, lng: markers[0].longitude, zoom: PLACED_ZOOM }
  return { lat: 0, lng: 0, zoom: WORLD_ZOOM }
}

export function buildTargetMapHtml(options: BuildTargetMapOptions = {}): string {
  const markers = options.markers ?? []
  const draft = options.draft ?? null
  const interactive = options.interactive ?? false
  const view = resolveView(markers, draft)

  // Operator-authored target names reach this string, so serialise through JSON and neutralise `<` so a
  // name can never break out of the <script> block or inject markup.
  const escape = (value: unknown) => JSON.stringify(value).replace(/</g, '\\u003c')

  return `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<link rel="stylesheet" href="https://unpkg.com/leaflet@${LEAFLET_VERSION}/dist/leaflet.css" />
<style>
  html, body, #map { margin: 0; height: 100%; width: 100%; background: #FAF1E5; }
  .target-pin {
    width: 20px; height: 20px; border-radius: 50%;
    background: #C08A5B; border: 3px solid #FFF9F0;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
  }
  .target-pin.draft { background: #E8651D; width: 24px; height: 24px; }
  .pick-hint {
    position: absolute; top: 8px; left: 50%; transform: translateX(-50%); z-index: 1000;
    background: rgba(24, 16, 10, 0.82); color: #FFF9F0; font: 12px/1.4 system-ui, sans-serif;
    padding: 4px 10px; border-radius: 999px; pointer-events: none;
  }
</style>
</head>
<body>
<div id="map"></div>
${interactive ? '<div class="pick-hint">Click the map to place this target</div>' : ''}
<script src="https://unpkg.com/leaflet@${LEAFLET_VERSION}/dist/leaflet.js"></script>
<script>
  var markers = ${escape(markers)};
  var draft = ${escape(draft)};
  var interactive = ${escape(interactive)};
  var map = L.map('map', { zoomControl: true }).setView([${view.lat}, ${view.lng}], ${view.zoom});
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap contributors'
  }).addTo(map);
  // Vector div-icons avoid Leaflet's default marker PNGs (whose CDN-relative paths break inside srcDoc).
  function pin(isDraft) {
    return L.divIcon({
      className: '',
      html: '<div class="target-pin' + (isDraft ? ' draft' : '') + '"></div>',
      iconSize: isDraft ? [24, 24] : [20, 20],
      iconAnchor: isDraft ? [12, 12] : [10, 10]
    });
  }
  markers.forEach(function (m) {
    L.marker([m.latitude, m.longitude], { icon: pin(false), title: m.name }).addTo(map).bindPopup(m.name);
  });
  var draftMarker = null;
  function placeDraft(lat, lng) {
    if (draftMarker) {
      draftMarker.setLatLng([lat, lng]);
    } else {
      draftMarker = L.marker([lat, lng], { icon: pin(true) }).addTo(map);
    }
  }
  if (draft) placeDraft(draft.latitude, draft.longitude);
  if (interactive) {
    map.on('click', function (e) {
      var lat = e.latlng.lat, lng = e.latlng.lng;
      placeDraft(lat, lng);
      // parent may be cross-origin (sandboxed iframe → opaque origin), so target '*'.
      parent.postMessage({ type: ${escape(TARGET_PICK_MESSAGE)}, latitude: lat, longitude: lng }, '*');
    });
  }
</script>
</body>
</html>`
}
