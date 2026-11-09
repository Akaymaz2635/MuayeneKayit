import React from "react";
import { Composition } from "remotion";
import { Kurulum, TOTAL_FRAMES } from "./Kurulum";

// KURULUM.md'nin anlatımı: 1920x1080, 30 fps. Süre sahnelerin toplamından hesaplanır.
export const Root: React.FC = () => (
  <Composition id="Kurulum" component={Kurulum} durationInFrames={TOTAL_FRAMES} fps={30} width={1920} height={1080} />
);
