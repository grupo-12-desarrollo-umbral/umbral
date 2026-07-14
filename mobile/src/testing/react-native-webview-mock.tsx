/**
 * Jest stand-in for `react-native-webview` (wired via `moduleNameMapper`). The real WebView is a
 * native component that jest-expo cannot render, and a map's content lives inside it anyway, so tests
 * assert on the component's presence and its `source.html` rather than a live map. Keeps the whole
 * treasure-hunt board renderable under react-test-renderer.
 */
import { View, type ViewProps } from 'react-native';

export type WebViewProps = ViewProps & {
  source?: { html?: string; uri?: string };
  originWhitelist?: readonly string[];
  scrollEnabled?: boolean;
  javaScriptEnabled?: boolean;
  domStorageEnabled?: boolean;
};

// Forward every prop (including `source`) onto a host View so tests can find it by testID and read
// back the injected HTML.
export function WebView(props: WebViewProps) {
  return <View {...props} />;
}

export default WebView;
