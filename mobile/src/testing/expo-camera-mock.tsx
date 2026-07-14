/**
 * Jest stand-in for `expo-camera` (wired via `moduleNameMapper`). The real `CameraView` is a native
 * component jest-expo cannot render, so it forwards props onto a host `View` — tests find it by testID
 * and can invoke the captured `onBarcodeScanned` to simulate a scan. `useCameraPermissions` defaults to
 * granted so any screen that mounts the scanner tree renders; a test needing a denied/pending state
 * overrides the whole module with its own `jest.mock('expo-camera', …)`.
 */
import { View, type ViewProps } from 'react-native';

export type BarcodeScanningResult = { type: string; data: string };

export type CameraViewProps = ViewProps & {
  facing?: 'front' | 'back';
  barcodeScannerSettings?: { barcodeTypes?: readonly string[] };
  onBarcodeScanned?: (result: BarcodeScanningResult) => void;
};

export function CameraView(props: CameraViewProps) {
  return <View {...props} />;
}

const GRANTED = { granted: true, canAskAgain: true, status: 'granted', expires: 'never' } as const;

export function useCameraPermissions(): [
  typeof GRANTED,
  () => Promise<typeof GRANTED>,
  () => Promise<typeof GRANTED>,
] {
  const request = () => Promise.resolve(GRANTED);
  return [GRANTED, request, request];
}
