import React from "react";
import { AbsoluteFill, Img, interpolate, Series, staticFile, useCurrentFrame } from "remotion";
import { Bullets, C, Card, Code, FONT, MONO, Pill, Scene, Shot, Terminal, useIn } from "./ui";

// KURULUM.md bölümleri sahne sahne. Metinler belgeyle aynı tutulur; belge değişince burası da güncellenir.
// Sıra: kurulumu yapan kişinin adımları (indir → klasörler → veri yeri → Admin → deneme → kullanıcılar),
// sonra izinler / güncelleme / ipuçları, en sonda kodu değiştirmek isteyenler için Visual Studio.
const S = {
  intro: 150,
  paths: 240,
  download: 360,
  folders: 450,
  dataPath: 540,
  settings: 300,
  firstRun: 480,
  test: 360,
  users: 330,
  perms: 330,
  update: 420,
  inspector: 480,
  tips: 360,
  vs: 330,
  build: 480,
  offline: 270,
  outro: 210,
};
export const TOTAL_FRAMES = Object.values(S).reduce((a, b) => a + b, 0);

export const Kurulum: React.FC = () => (
  <AbsoluteFill style={{ background: C.bg }}>
    <Series>
      <Series.Sequence durationInFrames={S.intro}><Intro /></Series.Sequence>
      <Series.Sequence durationInFrames={S.paths}><Paths /></Series.Sequence>
      <Series.Sequence durationInFrames={S.download}><Download /></Series.Sequence>
      <Series.Sequence durationInFrames={S.folders}><Deploy /></Series.Sequence>
      <Series.Sequence durationInFrames={S.dataPath}><DataPath /></Series.Sequence>
      <Series.Sequence durationInFrames={S.settings}><SettingsFile /></Series.Sequence>
      <Series.Sequence durationInFrames={S.firstRun}><FirstRun /></Series.Sequence>
      <Series.Sequence durationInFrames={S.test}><TestRun /></Series.Sequence>
      <Series.Sequence durationInFrames={S.users}><Users /></Series.Sequence>
      <Series.Sequence durationInFrames={S.perms}><Permissions /></Series.Sequence>
      <Series.Sequence durationInFrames={S.update}><Update /></Series.Sequence>
      <Series.Sequence durationInFrames={S.inspector}><InspectorUse /></Series.Sequence>
      <Series.Sequence durationInFrames={S.tips}><Tips /></Series.Sequence>
      <Series.Sequence durationInFrames={S.vs}><VisualStudio /></Series.Sequence>
      <Series.Sequence durationInFrames={S.build}><Build /></Series.Sequence>
      <Series.Sequence durationInFrames={S.offline}><Offline /></Series.Sequence>
      <Series.Sequence durationInFrames={S.outro}><Outro /></Series.Sequence>
    </Series>
  </AbsoluteFill>
);

// ───────── Adım 1: GitHub'dan indir ─────────
const Download: React.FC = () => (
  <Scene duration={S.download} section="Adım 1" title="GitHub'dan indir">
    <AbsoluteFill style={{ flexDirection: "row", padding: "70px 100px", gap: 60 }}>
      <div style={{ flex: 1 }}>
        <Bullets
          start={10}
          gap={40}
          size={34}
          items={[
            <>github.com/Akaymaz2635/MuayeneKayit — <b>özel depo</b>: GitHub hesabınla giriş yap</>,
            <>Sağdaki <b>Releases</b> › en son sürüm (<b>1.0.1</b>)</>,
            <><Code>MuayeneKayit-1.0.1.zip</Code> dosyasını indir (~340 MB)</>,
            <><b>Code › Download ZIP</b> değil — o kaynak koddur, programı içermez</>,
          ]}
        />
      </div>
      <div style={{ flex: 0.9, display: "flex", flexDirection: "column", gap: 34, justifyContent: "center" }}>
        <ReleaseCard />
        <InfoLine delay={230} color={C.amber}>
          İndirilen zip'e sağ tık › <b>Özellikler</b> › <b>Engellemeyi kaldır</b> › Tamam. Sonra çıkar. Yoksa Windows programı engelleyebilir.
        </InfoLine>
      </div>
    </AbsoluteFill>
  </Scene>
);

const ReleaseCard: React.FC = () => {
  const p = useIn(60);
  const dl = useIn(130);
  return (
    <div style={{ background: "#fff", borderRadius: 16, border: `1px solid ${C.line}`, boxShadow: "0 16px 40px rgba(26,58,92,0.15)", padding: 34, opacity: p, transform: `translateY(${(1 - p) * 30}px)` }}>
      <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
        <div style={{ fontSize: 40, fontWeight: 700 }}>MuayeneKayit 1.0.1</div>
        <Pill color={C.green}>Latest</Pill>
      </div>
      <div style={{ fontSize: 26, color: C.muted, margin: "10px 0 24px" }}>Assets</div>
      {[
        ["MuayeneKayit-1.0.1.zip", "337 MB", true],
        ["Source code (zip)", "", false],
      ].map(([name, size, main]) => (
        <div
          key={name as string}
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            padding: "16px 20px",
            borderRadius: 10,
            fontSize: 30,
            border: main ? `3px solid ${dl > 0.5 ? C.green : C.line}` : `1px solid ${C.line}`,
            marginBottom: 12,
            background: main && dl > 0.5 ? "#E8F6EE" : undefined,
          }}
        >
          <span style={{ fontFamily: MONO, color: main ? C.navy : C.muted }}>📦 {name}</span>
          <span style={{ color: C.muted }}>{main && dl > 0.5 ? "⬇ indiriliyor…" : size}</span>
        </div>
      ))}
    </div>
  );
};

// ───────── Adım 3: verinin yazılacağı yer ─────────
const DataPath: React.FC = () => (
  <Scene duration={S.dataPath} section="Adım 3" title="Kayıtlar nereye yazılacak? (veri klasörü)">
    <AbsoluteFill style={{ flexDirection: "row", padding: "50px 80px", gap: 50, alignItems: "center" }}>
      <div style={{ flex: 0.75 }}>
        <Bullets
          start={10}
          gap={45}
          size={32}
          items={[
            <>Ağdaki <b>Program</b> klasöründen <Code>MuayeneKayit Admin.exe</Code> aç</>,
            "İlk açılışta bir PIN belirle (6-12 rakam)",
            <><b>Ayarlar › Ortak alan › Kök klasör</b>: <Code>\\SUNUCU\MuayeneKayit\Veri</Code></>,
            <><b>Kaydet ve yeniden bağlan</b> — yol, tüm bilgisayarların kullandığı ayar dosyasına yazılır</>,
            "Yeni veri klasörü için PIN'i bir kez daha belirle",
          ]}
        />
      </div>
      <Shot src="admin-settings.png" imgW={1266} imgH={853} width={1000} delay={60} duration={S.dataPath} highlight={{ x: 228, y: 64, w: 1012, h: 198, delay: 160, label: "Veri klasörü burada yazılır" }} />
    </AbsoluteFill>
  </Scene>
);

// ───────── Adım 5: deneme ─────────
const TestRun: React.FC = () => (
  <Scene duration={S.test} section="Adım 5" title="Deneme muayenesi">
    <AbsoluteFill style={{ flexDirection: "row", padding: "60px 90px", gap: 60, alignItems: "center" }}>
      <Shot src="insp-start.png" imgW={1426} imgH={746} width={1050} delay={10} duration={S.test} highlight={{ x: 470, y: 486, w: 536, h: 50, delay: 90, label: "Create Inspection" }} />
      <div style={{ flex: 1 }}>
        <Bullets
          start={40}
          gap={40}
          size={32}
          items={[
            <>Program klasöründen <Code>MuayeneKayit.exe</Code> aç</>,
            "Test parça / operasyon / seri gir, Create Inspection",
            <>Veri klasöründe <Code>Parça\Operasyon\Seri</Code> klasörü oluştuysa kurulum tamam</>,
            "Pencere başlığında sürüm görünür: MuayeneKayit 1.0.1",
          ]}
        />
      </div>
    </AbsoluteFill>
  </Scene>
);

// ───────── 1. Giriş ─────────
const Intro: React.FC = () => {
  const a = useIn(5);
  const b = useIn(25);
  const c = useIn(45);
  return (
    <Scene duration={S.intro}>
      <AbsoluteFill style={{ background: `linear-gradient(135deg, ${C.navy} 0%, #0C1E30 100%)`, alignItems: "center", justifyContent: "center", gap: 30 }}>
        <div style={{ color: "rgba(255,255,255,0.7)", fontSize: 36, letterSpacing: 6, opacity: a }}>MOTOR PARÇASI GÖRSEL MUAYENE</div>
        <div style={{ color: "#fff", fontSize: 140, fontWeight: 700, opacity: b, transform: `scale(${0.9 + 0.1 * b})` }}>MuayeneKayit</div>
        <div style={{ color: "#9CB8DA", fontSize: 54, opacity: c }}>Kurulumu yapan kişi için adım adım</div>
        <div style={{ color: "rgba(255,255,255,0.5)", fontSize: 28, marginTop: 30, opacity: c }}>Sürüm 1.0.1 · KURULUM.md</div>
      </AbsoluteFill>
    </Scene>
  );
};

// ───────── 2. İki yol ─────────
const Paths: React.FC = () => (
  <Scene duration={S.paths} title="Hangi yol size uygun?">
    <AbsoluteFill style={{ flexDirection: "row", gap: 60, padding: "90px 110px", alignItems: "stretch" }}>
      <Card delay={10} style={{ flex: 1 }}>
        <div style={{ fontSize: 34, color: C.green, fontWeight: 700 }}>A · Hazır paket</div>
        <div style={{ fontSize: 50, fontWeight: 700, margin: "18px 0 30px" }}>Derleme yok</div>
        <Bullets
          start={30}
          size={34}
          items={[
            <>GitHub › <b>Releases</b> › <Code>MuayeneKayit-1.0.1.zip</Code></>,
            "Ağda Program + Veri klasörü aç, zip'i Program'a çıkar",
            "Admin'de veri klasörünü yaz, kullanıcılara kısayol ver",
          ]}
        />
      </Card>
      <Card delay={50} style={{ flex: 1 }}>
        <div style={{ fontSize: 34, color: C.accent, fontWeight: 700 }}>B · Visual Studio</div>
        <div style={{ fontSize: 50, fontWeight: 700, margin: "18px 0 30px" }}>Kodu değiştirip derle</div>
        <Bullets
          start={70}
          size={34}
          items={["Depoyu kopyala, çözümü aç", "Release | x64 ile derle", <>Paketi <Code>build-release.ps1</Code> üretir</>]}
        />
      </Card>
    </AbsoluteFill>
  </Scene>
);

// ───────── 3. Visual Studio kurulumu ─────────
const VisualStudio: React.FC = () => (
  <Scene duration={S.vs} section="Kod" title="Kodu değiştirmek isterseniz: Visual Studio">
    <AbsoluteFill style={{ padding: "80px 110px", gap: 40 }}>
      <div style={{ fontSize: 40 }}>
        <b>Visual Studio 2022</b> · 17.12 ya da yeni · Community / Professional / Enterprise fark etmez
      </div>
      <div style={{ display: "flex", gap: 50 }}>
        <Workload delay={30} title=".NET masaüstü geliştirme" en=".NET Desktop Development" />
        <Workload delay={60} title="WinUI uygulama geliştirme" en="WinUI application development" extra="Windows App SDK C# Templates" />
      </div>
      <InfoLine delay={110}>.NET 9 SDK bu iş yükleriyle gelir. Ayrıca .NET, Windows App SDK ya da NuGet paketi kurmanız gerekmez.</InfoLine>
    </AbsoluteFill>
  </Scene>
);

const Workload: React.FC<{ delay: number; title: string; en: string; extra?: string }> = ({ delay, title, en, extra }) => {
  const check = useIn(delay + 25);
  return (
    <Card delay={delay} style={{ flex: 1, display: "flex", gap: 30, alignItems: "center" }}>
      <div
        style={{
          width: 64,
          height: 64,
          borderRadius: 12,
          border: `4px solid ${C.navy}`,
          background: check > 0.5 ? C.navy : "#fff",
          color: "#fff",
          fontSize: 46,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          flexShrink: 0,
        }}
      >
        {check > 0.5 ? "✓" : ""}
      </div>
      <div>
        <div style={{ fontSize: 40, fontWeight: 700 }}>{title}</div>
        <div style={{ fontSize: 28, color: C.muted }}>{en}</div>
        {extra && <div style={{ fontSize: 28, marginTop: 10 }}>Ayrıntılarda: <b>{extra}</b> seçili</div>}
      </div>
    </Card>
  );
};

const InfoLine: React.FC<{ delay: number; children: React.ReactNode; color?: string }> = ({ delay, children, color = C.accent }) => {
  const p = useIn(delay);
  return (
    <div
      style={{
        borderLeft: `8px solid ${color}`,
        background: "#fff",
        padding: "24px 32px",
        borderRadius: 10,
        fontSize: 34,
        opacity: p,
        transform: `translateY(${(1 - p) * 20}px)`,
      }}
    >
      {children}
    </div>
  );
};

// ───────── 4. Derleme ve paket ─────────
const Build: React.FC = () => (
  <Scene duration={S.build} section="Kod" title="Derleme ve yeni dağıtım paketi">
    <AbsoluteFill style={{ flexDirection: "row", padding: "70px 100px", gap: 60 }}>
      <div style={{ flex: 1 }}>
        <Bullets
          start={10}
          gap={35}
          size={36}
          items={[
            <>Visual Studio › <b>Depoyu kopyala</b> › GitHub adresi</>,
            <><Code>MuayeneKayit.sln</Code> açılır</>,
            <>Üst çubuk: <b>Release</b> + <b>x64</b> › <b>Derle</b></>,
            <>İlk derlemede paketler (~240 MB) kendiliğinden iner</>,
            <>Tek program için: sağ tık › <b>Yayımla</b> › <Code>win-x64</Code></>,
          ]}
        />
      </div>
      <div style={{ flex: 1.1, display: "flex", flexDirection: "column", gap: 30, justifyContent: "center" }}>
        <Terminal
          title="Görünüm › Terminal"
          start={200}
          cps={32}
          width={950}
          lines={["PS> .\\build-release.ps1", "Testler çalışıyor... Tümü geçti", "Yayın: Inspector x64, ARM64", "Yayın: Admin x64, ARM64", "Hazır: dist\\MuayeneKayit-1.0.1.zip"]}
        />
        <InfoLine delay={380} color={C.green}>Testler + başlatıcı + Inspector / Admin (x64 ve ARM64) tek zip'te</InfoLine>
      </div>
    </AbsoluteFill>
  </Scene>
);

// ───────── 5. Offline paketler ─────────
const Offline: React.FC = () => (
  <Scene duration={S.offline} section="Kod" title="Paketler inmiyorsa (internet / proxy yok)">
    <AbsoluteFill style={{ padding: "80px 110px", gap: 40 }}>
      <Bullets
        start={10}
        size={36}
        items={[
          <>Releases › <Code>MuayeneKayit-paketler-offline.zip</Code> dosyasını depo köküne indir</>,
          "Terminalde çalıştır, Visual Studio'yu kapatıp aç, derle",
        ]}
      />
      <Terminal title="PowerShell" start={70} cps={45} width={1500} lines={[".\\offline-paketler.ps1 -Kullan -Zip .\\MuayeneKayit-paketler-offline.zip", "Yerel paket kaynağı eklendi: packages-offline"]} />
    </AbsoluteFill>
  </Scene>
);

// ───────── 6. Dağıtım klasörü ─────────
const TREE: { name: string; note: string; depth: number; kind: "dir" | "exe" | "file" }[] = [
  { name: "\\\\SUNUCU\\MuayeneKayit\\", note: "", depth: 0, kind: "dir" },
  { name: "Program\\", note: "Zip'in içi buraya — kullanıcılar yalnızca okur", depth: 1, kind: "dir" },
  { name: "MuayeneKayit.exe", note: "Başlatıcı — kullanıcılar bunu açar", depth: 2, kind: "exe" },
  { name: "MuayeneKayit Admin.exe", note: "Admin programı (yöneticiler)", depth: 2, kind: "exe" },
  { name: "surum.txt", note: "Açılacak sürüm: 1.0.1", depth: 2, kind: "file" },
  { name: "1.0.1\\", note: "Inspector / Admin · x64 / ARM64", depth: 2, kind: "dir" },
  { name: "Veri\\", note: "Boş açın — kayıt, fotoğraf, rapor buraya yazılır", depth: 1, kind: "dir" },
];

const Deploy: React.FC = () => (
  <Scene duration={S.folders} section="Adım 2" title="Ağ klasörlerini hazırla">
    <AbsoluteFill style={{ padding: "50px 110px", gap: 30 }}>
      <div style={{ fontSize: 34, color: C.muted }}>
        Ağda iki klasör açın: <b style={{ color: C.text }}>Program</b> (zip'in içi) ve <b style={{ color: C.text }}>Veri</b> (boş). Kullanıcı bilgisayarına hiçbir şey kurulmaz.
      </div>
      <Card delay={10} style={{ padding: "24px 44px" }}>
        {TREE.map((t, i) => (
          <TreeRow key={t.name} item={t} delay={25 + i * 28} />
        ))}
      </Card>
      <InfoLine delay={250}>Surface Pro 9 (ARM64) kendi sürümünü otomatik açar. Program çalışırken internete çıkmaz.</InfoLine>
    </AbsoluteFill>
  </Scene>
);

const TreeRow: React.FC<{ item: (typeof TREE)[number]; delay: number }> = ({ item, delay }) => {
  const p = useIn(delay);
  const icon = item.kind === "dir" ? "📁" : item.kind === "exe" ? "▶" : "📄";
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 20,
        padding: "12px 0",
        paddingLeft: item.depth * 60,
        opacity: p,
        transform: `translateX(${(1 - p) * 30}px)`,
        borderBottom: item.depth ? `1px solid ${C.line}` : undefined,
      }}
    >
      <span style={{ fontSize: 34, width: 44, color: item.kind === "exe" ? C.green : undefined }}>{icon}</span>
      <span style={{ fontFamily: MONO, fontSize: 32, fontWeight: item.depth === 2 ? 500 : 700, color: C.navy, minWidth: 560 - item.depth * 60 }}>{item.name}</span>
      <span style={{ fontSize: 30, color: C.muted }}>{item.note}</span>
    </div>
  );
};

// ───────── 7. Ayar dosyası ─────────
const SettingsFile: React.FC = () => (
  <Scene duration={S.settings} section="Adım 3" title="Alternatif: ayar dosyasını elle yaz">
    <AbsoluteFill style={{ padding: "70px 110px", gap: 36 }}>
      <Bullets
        start={10}
        size={34}
        items={[
          <>Program klasöründe <Code>MuayeneKayit.settings.example.json</Code> › kopyala › adı <Code>MuayeneKayit.settings.json</Code></>,
          <>Not Defteri ile açıp veri klasörünü yaz (JSON'da her ters bölü iki kez)</>,
        ]}
      />
      <Terminal title="MuayeneKayit.settings.json — Not Defteri" start={70} cps={30} width={1250} size={40} lines={['{ "rootPath": "\\\\\\\\SUNUCU\\\\MuayeneKayit\\\\Veri" }']} />
      <InfoLine delay={190}>Admin'den kaydetmekle aynı dosya. Tüm sürüm klasörleri bu tek dosyayı kullanır; güncellemede ezilmez.</InfoLine>
    </AbsoluteFill>
  </Scene>
);

// ───────── 8. Kullanıcılar ─────────
const Users: React.FC = () => (
  <Scene duration={S.users} section="Adım 6" title="Kullanıcılara dağıt: masaüstü kısayolu">
    <AbsoluteFill style={{ flexDirection: "row", padding: "60px 90px", gap: 60, alignItems: "center" }}>
      <div style={{ flex: 0.8 }}>
        <ContextMenu />
        <div style={{ height: 40 }} />
        <Bullets start={110} size={32} items={["Yönetici yetkisi ya da şifre gerekmez", "BT kısayolu grup ilkesiyle de dağıtabilir"]} />
      </div>
      <Shot src="insp-start.png" imgW={1426} imgH={746} width={960} delay={150} duration={S.users} highlight={{ x: 438, y: 222, w: 600, h: 345, delay: 200, label: "Program açılır" }} />
    </AbsoluteFill>
  </Scene>
);

const ContextMenu: React.FC = () => {
  const p = useIn(15);
  const sub = useIn(55);
  const row = (text: string, active = false) => (
    <div style={{ padding: "12px 24px", fontSize: 30, background: active ? "#E6EBF2" : undefined, borderRadius: 8 }}>{text}</div>
  );
  return (
    <div style={{ position: "relative", height: 360 }}>
      <div style={{ fontFamily: MONO, fontSize: 32, color: C.navy, marginBottom: 14, opacity: p }}>▶ MuayeneKayit.exe — sağ tık</div>
      <div style={{ position: "absolute", top: 60, width: 420, background: "#fff", borderRadius: 12, boxShadow: "0 16px 40px rgba(0,0,0,0.2)", padding: 10, opacity: p }}>
        {row("Aç")}
        {row("Diğer seçenekleri göster")}
        {row("Gönder  ›", true)}
        {row("Özellikler")}
      </div>
      <div
        style={{
          position: "absolute",
          top: 200,
          left: 400,
          width: 420,
          background: "#fff",
          borderRadius: 12,
          boxShadow: "0 16px 40px rgba(0,0,0,0.2)",
          padding: 10,
          opacity: sub,
          transform: `translateX(${(1 - sub) * -20}px)`,
        }}
      >
        {row("Masaüstü (kısayol oluştur)", true)}
        {row("Sıkıştırılmış klasör")}
      </div>
    </div>
  );
};

// ───────── 9. Güncelleme ─────────
const Update: React.FC = () => {
  const frame = useCurrentFrame();
  const swap = frame > 200;
  return (
    <Scene duration={S.update} section="Sonra" title="Güncelleme — program açıkken de yapılır">
      <AbsoluteFill style={{ padding: "70px 110px", gap: 44 }}>
        <div style={{ display: "flex", gap: 40, alignItems: "stretch" }}>
          <Step delay={10} n="1" title="Yeni sürüm klasörünü kopyala" body={<Code>1.0.2\</Code>} />
          <Step delay={60} n="2" title="surum.txt'yi değiştir" body={<span style={{ fontFamily: MONO, fontSize: 40 }}>{swap ? "1.0.2" : "1.0.1"}</span>} />
          <Step delay={110} n="3" title="Kullanıcılar sonraki açılışta yeni sürümü alır" body={<Pill color={C.green}>otomatik</Pill>} />
        </div>
        <div style={{ display: "flex", gap: 40 }}>
          <InfoLine delay={230} color={C.amber}>Açık programlar eski klasörden çalışmaya devam eder — kimse kapatmak zorunda değil.</InfoLine>
        </div>
        <InfoLine delay={290}>Eski sürüm klasörü, herkes kapattıktan sonra silinebilir. Başlatıcılara ve ayar dosyasına dokunmayın.</InfoLine>
      </AbsoluteFill>
    </Scene>
  );
};

const Step: React.FC<{ delay: number; n: string; title: string; body: React.ReactNode }> = ({ delay, n, title, body }) => (
  <Card delay={delay} style={{ flex: 1 }}>
    <div style={{ width: 70, height: 70, borderRadius: 35, background: C.navy, color: "#fff", fontSize: 40, fontWeight: 700, display: "flex", alignItems: "center", justifyContent: "center" }}>{n}</div>
    <div style={{ fontSize: 36, fontWeight: 600, margin: "24px 0" }}>{title}</div>
    <div style={{ fontSize: 34 }}>{body}</div>
  </Card>
);

// ───────── 10. İzinler ─────────
const Permissions: React.FC = () => (
  <Scene duration={S.perms} section="İzin" title="Ağ klasörü izinleri (BT)">
    <AbsoluteFill style={{ padding: "70px 110px", gap: 40 }}>
      <div style={{ display: "flex", gap: 40 }}>
        <PermCard delay={10} title="Program klasörü" rows={[["Kullanıcılar", "Okuma", C.accent], ["BT / Admin", "Tam", C.navy]]} />
        <PermCard delay={50} title="Veri klasörü" rows={[["MK_Muayene", "Okuma + yazma", C.green], ["MK_Okuma (kalite)", "Okuma", C.accent], ["MK_Admin", "Tam", C.navy]]} />
        <PermCard delay={90} title="_config (şablonlar, PIN)" rows={[["MK_Muayene", "Yalnızca okuma", C.amber], ["MK_Admin", "Tam", C.navy]]} />
      </div>
      <InfoLine delay={160}>Komutlar KURULUM.md bölüm 3'te (icacls). Canlıya almadan önce bir test kullanıcısıyla deneyin.</InfoLine>
    </AbsoluteFill>
  </Scene>
);

const PermCard: React.FC<{ delay: number; title: string; rows: [string, string, string][] }> = ({ delay, title, rows }) => (
  <Card delay={delay} style={{ flex: 1 }}>
    <div style={{ fontSize: 38, fontWeight: 700, marginBottom: 24 }}>{title}</div>
    {rows.map(([who, what, color]) => (
      <div key={who} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: 30, padding: "14px 0", borderTop: `1px solid ${C.line}` }}>
        <span>{who}</span>
        <Pill color={color}>{what}</Pill>
      </div>
    ))}
  </Card>
);

// ───────── 11. İlk açılış (Admin) ─────────
const FirstRun: React.FC = () => {
  const frame = useCurrentFrame();
  const shots = [
    { src: "admin-pin.png", label: "PIN: Admin'e her girişte sorulur (unutulursa _config\\admin.json silinir)", from: 0 },
    { src: "admin-types.png", label: "Kusur tipleri ve ölçüm alanları › Tümünü ortak alana kaydet", from: 160 },
    { src: "admin-settings.png", label: "İsteğe bağlı: Inspector kimliği — ad Windows oturumundan gelir", from: 320 },
  ];
  const current = [...shots].reverse().find((s) => frame >= s.from) ?? shots[0];
  const local = frame - current.from;
  const fade = interpolate(local, [0, 15], [0, 1], { extrapolateRight: "clamp" });
  return (
    <Scene duration={S.firstRun} section="Adım 4" title="Admin: ilk ayarlar">
      <AbsoluteFill style={{ alignItems: "center", padding: "40px 0", gap: 26 }}>
        <div style={{ fontSize: 38, fontWeight: 600, color: C.navy, opacity: fade }}>{current.label}</div>
        <div
          style={{
            width: 1110,
            height: 748,
            borderRadius: 14,
            overflow: "hidden",
            boxShadow: "0 24px 60px rgba(26,58,92,0.25)",
            border: `1px solid ${C.line}`,
            opacity: fade,
            transform: `scale(${0.97 + 0.03 * fade})`,
          }}
        >
          <Img src={staticFile(current.src)} style={{ width: "100%", height: "100%", display: "block" }} />
        </div>
      </AbsoluteFill>
    </Scene>
  );
};

// ───────── 12. Inspector'da yenilikler ─────────
const InspectorUse: React.FC = () => {
  const frame = useCurrentFrame();
  const second = frame >= 240;
  return (
    <Scene duration={S.inspector} section="5" title={second ? "Rework sonrası: bölge bazında sonuç" : "Etkilenen parçalar (lot) ve açıklama cümlesi"}>
      <AbsoluteFill style={{ flexDirection: "row", padding: "50px 70px", gap: 50, alignItems: "center" }}>
        {!second ? (
          <>
            <Shot src="insp-measure.png" imgW={1426} imgH={746} width={1150} delay={5} duration={240} highlight={{ x: 440, y: 368, w: 316, h: 66, delay: 60, label: "Lot: 1-5, 8" }} />
            <div style={{ flex: 1 }}>
              <Bullets start={90} size={30} gap={30} items={["Her kusurda isteğe bağlı; boş = tüm parça", "Kutudan çıkınca otomatik kaydedilir", "Cümle: “on parts 1–5 and 8”", "Raporda ve CSV'de görünür"]} />
            </div>
          </>
        ) : (
          <>
            <Shot src="insp-reinspect.png" imgW={1426} imgH={746} width={1150} delay={245} duration={S.inspector} highlight={{ x: 473, y: 157, w: 480, h: 466, delay: 280 }} />
            <div style={{ flex: 1 }}>
              <Bullets start={300} size={30} gap={30} items={["Her fotoğraf (bölge) için: Giderildi / Devam ediyor / Yeni hata", "Birden fazla yeni hata eklenebilir", "Raporda bölge durumu yazar"]} />
            </div>
          </>
        )}
      </AbsoluteFill>
    </Scene>
  );
};

// ───────── 13. Kamera ve PDF ─────────
const Tips: React.FC = () => (
  <Scene duration={S.tips} section="4 · 6" title="Kamera ve PDF rapor">
    <AbsoluteFill style={{ flexDirection: "row", padding: "60px 90px", gap: 60, alignItems: "center" }}>
      <div style={{ flex: 0.9 }}>
        <Bullets
          start={10}
          size={32}
          gap={30}
          items={[
            "Notebook: Logitech C920'yi USB'ye takın — önce o seçilir",
            "Surface Pro 9: arka kamera seçilir",
            <>Windows › Gizlilik › Kamera › <b>Masaüstü uygulamalarına izin ver</b> açık olmalı</>,
            "Rapor, Windows'taki Edge ile yerelde PDF'e çevrilir (ağ yok)",
            "İki dilli rapor: Türkçe / English",
          ]}
        />
      </div>
      <Shot src="report-page.png" imgW={1575} imgH={1080} width={980} delay={120} duration={S.tips} zoomTo={1.06} />
    </AbsoluteFill>
  </Scene>
);

// ───────── 14. Kapanış ─────────
const Outro: React.FC = () => {
  const a = useIn(5);
  const b = useIn(30);
  return (
    <Scene duration={S.outro}>
      <AbsoluteFill style={{ background: `linear-gradient(135deg, ${C.navy} 0%, #0C1E30 100%)`, alignItems: "center", justifyContent: "center", gap: 36, fontFamily: FONT }}>
        <div style={{ color: "#fff", fontSize: 80, fontWeight: 700, opacity: a }}>Hazırsınız</div>
        <div style={{ color: "#9CB8DA", fontSize: 40, opacity: b }}>Ayrıntılar ve komutlar: KURULUM.md</div>
        <div style={{ color: "#fff", fontFamily: MONO, fontSize: 36, background: "rgba(255,255,255,0.1)", padding: "16px 34px", borderRadius: 14, opacity: b }}>
          github.com/Akaymaz2635/MuayeneKayit
        </div>
      </AbsoluteFill>
    </Scene>
  );
};
