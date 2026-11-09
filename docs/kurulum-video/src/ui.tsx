import React from "react";
import { AbsoluteFill, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";

// Ortak görsel dil: kurumsal lacivert, açık zemin, Segoe UI (Windows ile gelir).
export const C = {
  navy: "#1A3A5C",
  navyLight: "#2C5480",
  accent: "#4A73A0",
  bg: "#F3F5F9",
  card: "#FFFFFF",
  text: "#1A1A1A",
  muted: "#5A6472",
  line: "#DDE2EA",
  green: "#15803D",
  amber: "#B45309",
  red: "#DC2626",
};
export const FONT = "'Segoe UI', 'Segoe UI Variable', Arial, sans-serif";
export const MONO = "'Cascadia Mono', Consolas, 'Courier New', monospace";

/** 0 → 1, sahne başından itibaren `delay` kare sonra yumuşak giriş. */
export const useIn = (delay = 0) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  return spring({ frame: frame - delay, fps, config: { damping: 200 } });
};

/** Sahnenin son karelerinde kararma (geçiş). */
export const useOut = (duration: number, length = 12) => {
  const frame = useCurrentFrame();
  return interpolate(frame, [duration - length, duration], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
};

export const Scene: React.FC<{ duration: number; section?: string; title?: string; children: React.ReactNode }> = ({
  duration,
  section,
  title,
  children,
}) => {
  const out = useOut(duration);
  const head = useIn(0);
  return (
    <AbsoluteFill style={{ background: C.bg, fontFamily: FONT, color: C.text, opacity: out }}>
      {title && (
        <div
          style={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            height: 120,
            background: C.navy,
            display: "flex",
            alignItems: "center",
            padding: "0 80px",
            gap: 28,
            transform: `translateY(${(1 - head) * -120}px)`,
          }}
        >
          {section && (
            <div
              style={{
                background: "rgba(255,255,255,0.15)",
                color: "#fff",
                borderRadius: 12,
                padding: "8px 18px",
                fontSize: 30,
                fontWeight: 600,
              }}
            >
              {section}
            </div>
          )}
          <div style={{ color: "#fff", fontSize: 48, fontWeight: 600 }}>{title}</div>
          <div style={{ marginLeft: "auto", color: "rgba(255,255,255,0.6)", fontSize: 26 }}>MuayeneKayit · Kurulum</div>
        </div>
      )}
      <AbsoluteFill style={{ top: title ? 120 : 0 }}>{children}</AbsoluteFill>
    </AbsoluteFill>
  );
};

/** Sırayla beliren madde listesi. */
export const Bullets: React.FC<{ items: React.ReactNode[]; start?: number; gap?: number; size?: number }> = ({
  items,
  start = 15,
  gap = 22,
  size = 38,
}) => (
  <div style={{ display: "flex", flexDirection: "column", gap: 26 }}>
    {items.map((item, i) => (
      <Bullet key={i} delay={start + i * gap} size={size}>
        {item}
      </Bullet>
    ))}
  </div>
);

const Bullet: React.FC<{ delay: number; size: number; children: React.ReactNode }> = ({ delay, size, children }) => {
  const p = useIn(delay);
  return (
    <div
      style={{
        display: "flex",
        gap: 22,
        alignItems: "flex-start",
        fontSize: size,
        lineHeight: 1.35,
        opacity: p,
        transform: `translateX(${(1 - p) * 40}px)`,
      }}
    >
      <div style={{ width: 14, height: 14, borderRadius: 7, background: C.accent, marginTop: size * 0.45, flexShrink: 0 }} />
      <div>{children}</div>
    </div>
  );
};

export const Code: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <span
    style={{
      fontFamily: MONO,
      background: "#E6EBF2",
      color: C.navy,
      borderRadius: 8,
      padding: "2px 12px",
      fontSize: "0.85em",
    }}
  >
    {children}
  </span>
);

/** Ekran görüntüsü: hafif yakınlaşma, isteğe bağlı vurgu kutusu (görüntü pikseli olarak). */
export const Shot: React.FC<{
  src: string;
  width: number;
  imgW: number;
  imgH: number;
  delay?: number;
  zoomTo?: number;
  highlight?: { x: number; y: number; w: number; h: number; delay: number; label?: string };
  duration: number;
}> = ({ src, width, imgW, imgH, delay = 5, zoomTo = 1.04, highlight, duration }) => {
  const frame = useCurrentFrame();
  const p = useIn(delay);
  const zoom = interpolate(frame, [delay, duration], [1, zoomTo], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const scale = width / imgW;
  const hl = useIn(highlight?.delay ?? 0);
  return (
    <div
      style={{
        width,
        height: imgH * scale,
        borderRadius: 14,
        overflow: "hidden",
        boxShadow: "0 24px 60px rgba(26,58,92,0.25)",
        border: `1px solid ${C.line}`,
        opacity: p,
        transform: `translateY(${(1 - p) * 40}px)`,
        position: "relative",
        background: "#fff",
      }}
    >
      <div style={{ transform: `scale(${zoom})`, transformOrigin: "50% 40%", width: "100%", height: "100%", position: "relative" }}>
        <Img src={staticFile(src)} style={{ width: "100%", height: "100%", display: "block" }} />
        {highlight && (
          <div
            style={{
              position: "absolute",
              left: highlight.x * scale,
              top: highlight.y * scale,
              width: highlight.w * scale,
              height: highlight.h * scale,
              border: `5px solid ${C.red}`,
              borderRadius: 10,
              opacity: hl,
              transform: `scale(${1.15 - 0.15 * hl})`,
              boxShadow: "0 0 0 9999px rgba(0,0,0,0.18)",
            }}
          >
            {highlight.label && (
              <div
                style={{
                  position: "absolute",
                  top: -48,
                  left: 0,
                  background: C.red,
                  color: "#fff",
                  fontSize: 24,
                  fontWeight: 600,
                  padding: "4px 14px",
                  borderRadius: 8,
                  whiteSpace: "nowrap",
                }}
              >
                {highlight.label}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

/** Yazılıyormuş gibi beliren terminal / dosya. */
export const Terminal: React.FC<{ title: string; lines: string[]; start?: number; cps?: number; width?: number; size?: number }> = ({
  title,
  lines,
  start = 10,
  cps = 40,
  width = 1100,
  size = 30,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const p = useIn(start - 8);
  const chars = Math.max(0, Math.floor(((frame - start) / fps) * cps));
  let left = chars;
  const shown = lines.map((l) => {
    const take = Math.max(0, Math.min(l.length, left));
    left -= l.length + 4;
    return l.slice(0, take);
  });
  const cursorOn = Math.floor(frame / 15) % 2 === 0;
  return (
    <div
      style={{
        width,
        background: "#0F1B2A",
        borderRadius: 16,
        boxShadow: "0 24px 60px rgba(0,0,0,0.3)",
        overflow: "hidden",
        opacity: p,
        transform: `translateY(${(1 - p) * 30}px)`,
      }}
    >
      <div style={{ background: "#1E2B3C", padding: "12px 20px", display: "flex", gap: 10, alignItems: "center" }}>
        {["#FF5F57", "#FEBC2E", "#28C840"].map((c) => (
          <div key={c} style={{ width: 14, height: 14, borderRadius: 7, background: c }} />
        ))}
        <div style={{ color: "#9FB0C6", fontSize: 22, marginLeft: 14, fontFamily: FONT }}>{title}</div>
      </div>
      <div style={{ padding: "22px 28px", fontFamily: MONO, fontSize: size, color: "#E3ECF7", lineHeight: 1.55, minHeight: 80 }}>
        {shown.map((l, i) => (
          <div key={i} style={{ whiteSpace: "pre", color: lines[i].startsWith("#") || lines[i].startsWith("rem") ? "#7F93AD" : undefined }}>
            {l}
            {i === shown.findIndex((s, j) => s.length < lines[j].length) && cursorOn ? "▌" : ""}
          </div>
        ))}
      </div>
    </div>
  );
};

export const Card: React.FC<{ children: React.ReactNode; delay?: number; style?: React.CSSProperties }> = ({ children, delay = 0, style }) => {
  const p = useIn(delay);
  return (
    <div
      style={{
        background: C.card,
        borderRadius: 18,
        border: `1px solid ${C.line}`,
        boxShadow: "0 12px 32px rgba(26,58,92,0.10)",
        padding: 36,
        opacity: p,
        transform: `translateY(${(1 - p) * 30}px) scale(${0.97 + 0.03 * p})`,
        ...style,
      }}
    >
      {children}
    </div>
  );
};

export const Pill: React.FC<{ color: string; children: React.ReactNode }> = ({ color, children }) => (
  <span style={{ background: color + "1F", color, borderRadius: 10, padding: "4px 14px", fontWeight: 600, fontSize: "0.8em" }}>{children}</span>
);
