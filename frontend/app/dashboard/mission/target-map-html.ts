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

// The backend has no null coordinate: a target saved without a location reads back as 0,0. Treat that
// pair as "unplaced" rather than as Null Island, so it never anchors the view or draws a pin. The cost
// is that the real point at 0,0 (open ocean) can't be authored — the editor already made that trade.
export function isPlacedCoordinate(latitude: number, longitude: number): boolean {
  return latitude !== 0 || longitude !== 0
}

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
  // Fallback center when no markers or draft exist (e.g. browser geolocation).
  defaultCenter?: { lat: number; lng: number } | null
}

// Chooses the initial centre/zoom: the draft if present, else the first context marker, else the
// caller-supplied default, else a zoomed-out world view so an operator with nothing placed can still
// navigate. Callers pass only placed markers/draft, so an unplaced target can never anchor the view.
function resolveView(
  markers: readonly TargetMapMarker[],
  draft: TargetMapDraft | null,
  defaultCenter: { lat: number; lng: number } | null,
): { lat: number; lng: number; zoom: number } {
  if (draft) return { lat: draft.latitude, lng: draft.longitude, zoom: PLACED_ZOOM }
  if (markers.length > 0) return { lat: markers[0].latitude, lng: markers[0].longitude, zoom: PLACED_ZOOM }
  if (defaultCenter) return { lat: defaultCenter.lat, lng: defaultCenter.lng, zoom: PLACED_ZOOM }
  return { lat: 0, lng: 0, zoom: WORLD_ZOOM }
}

export function buildTargetMapHtml(options: BuildTargetMapOptions = {}): string {
  // Drop the 0,0 sentinel here as well as at the call sites: it must neither anchor the view nor draw a
  // pin in the middle of the Atlantic.
  const markers = (options.markers ?? []).filter((m) => isPlacedCoordinate(m.latitude, m.longitude))
  const rawDraft = options.draft ?? null
  const draft = rawDraft && isPlacedCoordinate(rawDraft.latitude, rawDraft.longitude) ? rawDraft : null
  const interactive = options.interactive ?? false
  const defaultCenter = options.defaultCenter ?? null
  const view = resolveView(markers, draft, defaultCenter)
  // The read-only overview shows several targets at once; anchoring on markers[0] (see resolveView) hid
  // any target in a different place. Frame all of them instead. Interactive per-target maps keep the
  // single fixed view so picking doesn't re-zoom the map on every click.
  const fitAllMarkers = !interactive && markers.length > 1

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
${interactive ? '<div class="pick-hint">Haz clic en el mapa para ubicar este target</div>' : ''}
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
  ${fitAllMarkers ? `// resolveView anchored the initial view on markers[0] at a fixed zoom, which is right for a single
  // point but leaves targets in other places outside the viewport — they look collapsed onto the first
  // pin. In the read-only overview, reframe to fit every marker. (Interactive mode keeps the draft-centred
  // view so the map doesn't re-zoom on each pick.)
  map.fitBounds(markers.map(function (m) { return [m.latitude, m.longitude]; }), { padding: [40, 40], maxZoom: ${PLACED_ZOOM} });` : ''}
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
