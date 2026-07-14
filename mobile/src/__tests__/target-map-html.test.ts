import { buildTargetMapHtml, type TargetMapMarker } from '@/components/target-map-html';

const MARKERS: TargetMapMarker[] = [
  { id: 't1', name: 'Brass Astrolabe', latitude: 40.4319, longitude: -3.6883 },
  { id: 't2', name: 'North Colonnade', latitude: 40.4155, longitude: -3.7074 },
];

describe('buildTargetMapHtml', () => {
  test('centres the map on the first (active) marker', () => {
    const html = buildTargetMapHtml(MARKERS);
    // setView takes the active target's coordinates, not a fitted bounds box.
    expect(html).toContain('setView([40.4319, -3.6883]');
  });

  test('loads Leaflet and OpenStreetMap tiles', () => {
    const html = buildTargetMapHtml(MARKERS);
    expect(html).toContain('leaflet@1.9.4/dist/leaflet.js');
    expect(html).toContain('leaflet@1.9.4/dist/leaflet.css');
    expect(html).toContain('tile.openstreetmap.org');
  });

  test('drops a marker for every target with its coordinates', () => {
    const html = buildTargetMapHtml(MARKERS);
    expect(html).toContain('40.4155');
    expect(html).toContain('-3.7074');
    // Both target names are carried into the payload for the pins/popups.
    expect(html).toContain('Brass Astrolabe');
    expect(html).toContain('North Colonnade');
  });

  test('neutralises "<" so an operator-authored name cannot break out of the script', () => {
    const html = buildTargetMapHtml([
      { id: 't1', name: '</script><img src=x>', latitude: 1, longitude: 2 },
    ]);
    // The raw closing tag must not appear verbatim in the document.
    expect(html).not.toContain('</script><img');
    // It survives as an escaped unicode sequence inside the JSON payload instead.
    expect(html).toContain('\\u003c/script>');
  });
});
