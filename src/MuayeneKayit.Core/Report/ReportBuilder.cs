using System.Globalization;
using System.Net;
using System.Text;

namespace MuayeneKayit.Core;

/// <summary>
/// Yatay A4 görsel rapor üretir: kapak özeti ve her kusur için bir sayfa.
/// Çıktı tek bir HTML dosyasıdır, dış kaynak kullanmaz; yazdırıp PDF'e çevrilebilir.
/// Kullanıcı metinleri HTML'e gömülmeden önce kodlanır.
/// </summary>
public static class ReportBuilder
{
    public static string Build(Session session, IReadOnlyList<Defect> allDefects, int version)
    {
        // Void edilen kayıtlar rapora girmez; ham dosyalarda kalır.
        var defects = allDefects
            .Where(d => DefectStateMachine.GetState(d) != DefectState.Voided)
            .OrderBy(d => d.Id)
            .ToList();

        var total = Math.Max(1, defects.Count);
        var sb = new StringBuilder();
        var status = session.Status == SessionStatus.Completed ? "Tamamlandı" : "Taslak, muayene henüz tamamlanmadı";

        sb.Append("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"/>");
        sb.Append($"<title>Rapor v{version} — {E(session.PartNumber)} / {E(session.SerialNumber)}</title>");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.Append("<style>").Append(Css).Append("</style></head><body>");

        // Özet sayfası yok: her kusur kendi sayfasında, başlıkta parça / operasyon / seri bilgisi var.
        if (defects.Count == 0)
        {
            sb.Append("<section class=\"page\">");
            sb.Append(Header(session, version, status));
            sb.Append("<p class=\"muted\">Kusur kaydı yok.</p>");
            sb.Append($"<footer><span>Parça {E(session.PartNumber)} · Seri {E(session.SerialNumber)}</span><span>Sayfa 1 / 1</span></footer>");
            sb.Append("</section>");
        }

        var index = 0;
        foreach (var d in defects)
        {
            index++;
            sb.Append("<section class=\"page\">");
            sb.Append(Header(session, version, status));
            sb.Append(DefectPage(d));
            sb.Append($"<footer><span>Kusur #{d.Id} · {E(d.DefectType)}</span><span>Sayfa {index} / {total}</span></footer>");
            sb.Append("</section>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Header(Session s, int version, string status) =>
        $"<div class=\"hdr\"><div><strong>Parça {E(s.PartNumber)}</strong> &nbsp; Operasyon {E(s.OperationNumber)} &nbsp; Seri {E(s.SerialNumber)}</div>" +
        $"<div>Proje {E(s.Project)} · Muayeneci {E(s.Inspector)} · {s.CreatedAt:dd.MM.yyyy} · Rapor v{version} · {E(status)}</div></div>";

    private static string DefectPage(Defect d)
    {
        var sb = new StringBuilder();
        var state = DefectStateMachine.GetState(d);

        sb.Append("<div class=\"defect-bar\">")
          .Append($"<span class=\"id\">#{d.Id}</span>")
          .Append($"<span class=\"type\">{E(d.DefectType)}</span>")
          .Append($"<span class=\"state st-{CssClass(state)}\">{E(StateText(state))}</span>")
          .Append("</div>");

        sb.Append("<div class=\"body\">");

        // Sol: fotoğraflar
        sb.Append("<div class=\"photos\">");
        if (d.Photos.Count == 0)
        {
            sb.Append("<div class=\"no-photo\">Fotoğraf yok</div>");
        }
        else
        {
            foreach (var photo in d.Photos)
                sb.Append(PhotoFigure(photo, d.Markups.GetValueOrDefault(photo)));
        }
        sb.Append("</div>");

        // Sağ: bilgi
        sb.Append("<div class=\"info\">");

        if (d.SupersedesId is int from)
            sb.Append($"<p class=\"link\">Bu kusur, {from} numaralı kusurun rework'ünden sonra yapılan muayenede bulundu.</p>");
        if (d.SupersededById is int to)
            sb.Append($"<p class=\"link\">Rework sonrası bu kusur giderildi, ancak yeni bir kusur oluştu. Takip {to} numaralı kusur üzerinden yapılıyor.</p>");

        sb.Append("<h3>Ölçümler</h3><div class=\"meas\">");
        sb.Append(MeasCell("Derinlik", Mm(d.Depth)));
        sb.Append(MeasCell("Genişlik", Mm(d.Width)));
        sb.Append(MeasCell("Uzunluk", Mm(d.Length)));
        sb.Append(MeasCell("Yarıçap", Mm(d.Radius)));
        sb.Append(MeasCell("Açı", d.AngleDeg is double a ? $"{Num(a)}°" : "—"));
        sb.Append(MeasCell("Renk", string.IsNullOrWhiteSpace(d.Color) ? "—" : d.Color!));
        sb.Append("</div>");

        if (!string.IsNullOrWhiteSpace(d.Notes))
            sb.Append($"<p class=\"note\"><strong>Not:</strong> {E(d.Notes)}</p>");

        // Geri alınan kararlar ve geri alma kayıtları raporda gösterilmez (ham dosyada ve denetim kaydında kalır).
        var history = DefectStateMachine.Effective(d);
        sb.Append("<h3>Karar geçmişi</h3>");
        if (history.Count == 0)
        {
            sb.Append("<p class=\"muted\">Henüz karar yok.</p>");
        }
        else
        {
            sb.Append("<table class=\"hist\"><thead><tr><th>#</th><th>Tarih</th><th>Kim</th><th>Karar</th><th>Not</th><th>Referans</th><th>Ölçüm (karar anı)</th></tr></thead><tbody>");
            var row = 0;
            foreach (var x in history)
            {
                row++;
                sb.Append("<tr>")
                  .Append($"<td>{row}</td>")
                  .Append($"<td>{x.DecidedAt:dd.MM.yyyy HH:mm}</td>")
                  .Append($"<td>{E(x.DecidedBy)}</td>")
                  .Append($"<td>{E(DefectSentence.DecisionLabel(x.Decision))}</td>")
                  .Append($"<td>{E(x.Note)}</td>")
                  .Append($"<td>{E(x.Reference)}</td>")
                  .Append($"<td>{E(SnapshotText(x.Snapshot))}</td>")
                  .Append("</tr>");
            }
            sb.Append("</tbody></table>");
        }

        if (d.MeasurementEdits.Count > 0)
        {
            sb.Append("<h3>Ölçüm düzeltmeleri</h3><table class=\"hist\"><thead><tr><th>Tarih</th><th>Kim</th><th>Önce</th><th>Sonra</th></tr></thead><tbody>");
            foreach (var e in d.MeasurementEdits)
            {
                sb.Append("<tr>")
                  .Append($"<td>{e.At:dd.MM.yyyy HH:mm}</td>")
                  .Append($"<td>{E(e.By)}</td>")
                  .Append($"<td>{E(SnapshotText(e.Before))}</td>")
                  .Append($"<td>{E(SnapshotText(e.After))}</td>")
                  .Append("</tr>");
            }
            sb.Append("</tbody></table>");
        }

        sb.Append("</div></div>");
        return sb.ToString();
    }

    /// <summary>
    /// Fotoğraf ve işaretleri. İşaret varsa görüntü SVG içine alınır, böylece kutu / ok / daire
    /// her ölçekte fotoğrafla aynı yere düşer. Arka planı kaldırılmış kopya varsa o gösterilir.
    /// </summary>
    private static string PhotoFigure(string photo, PhotoMarkup? markup)
    {
        var file = markup?.CleanedFile is string clean && PhotoMarkupValidator.IsPhotoFileName(clean) ? clean : photo;
        var src = $"../defects/{E(file)}";

        if (markup is null || markup.Shapes.Count == 0 || PhotoMarkupValidator.Validate(markup).Count > 0)
            return $"<figure><img src=\"{src}\" alt=\"{E(photo)}\"/><figcaption>{E(photo)}</figcaption></figure>";

        double w = markup.ImageWidth, h = markup.ImageHeight;
        var stroke = Math.Max(w, h) / 220;
        var font = Math.Max(w, h) / 28;
        var sb = new StringBuilder();
        sb.Append("<figure><svg class=\"mk\" xmlns=\"http://www.w3.org/2000/svg\" ")
          .Append($"viewBox=\"0 0 {Num(w)} {Num(h)}\" preserveAspectRatio=\"xMidYMid meet\">")
          .Append("<defs><marker id=\"ah\" viewBox=\"0 0 10 10\" refX=\"8\" refY=\"5\" markerWidth=\"4\" markerHeight=\"4\" orient=\"auto-start-reverse\">")
          .Append("<path d=\"M0,0 L10,5 L0,10 z\" fill=\"#FF2D2D\"/></marker></defs>")
          .Append($"<image href=\"{src}\" x=\"0\" y=\"0\" width=\"{Num(w)}\" height=\"{Num(h)}\"/>");

        foreach (var s in markup.Shapes)
        {
            double x1 = s.X1 * w, y1 = s.Y1 * h, x2 = s.X2 * w, y2 = s.Y2 * h;
            var common = $"fill=\"none\" stroke=\"#FF2D2D\" stroke-width=\"{Num(stroke)}\"";
            switch (s.Kind)
            {
                case AnnotationKind.Box:
                    sb.Append($"<rect x=\"{Num(Math.Min(x1, x2))}\" y=\"{Num(Math.Min(y1, y2))}\" width=\"{Num(Math.Abs(x2 - x1))}\" height=\"{Num(Math.Abs(y2 - y1))}\" {common}/>");
                    break;
                case AnnotationKind.Circle:
                    sb.Append($"<ellipse cx=\"{Num((x1 + x2) / 2)}\" cy=\"{Num((y1 + y2) / 2)}\" rx=\"{Num(Math.Abs(x2 - x1) / 2)}\" ry=\"{Num(Math.Abs(y2 - y1) / 2)}\" {common}/>");
                    break;
                case AnnotationKind.Arrow:
                    sb.Append($"<line x1=\"{Num(x1)}\" y1=\"{Num(y1)}\" x2=\"{Num(x2)}\" y2=\"{Num(y2)}\" {common} marker-end=\"url(#ah)\"/>");
                    break;
            }

            // Etiket: kutu ve dairede sol üst köşe, okta kuyruk ucu.
            var lx = s.Kind == AnnotationKind.Arrow ? x1 : Math.Min(x1, x2);
            var ly = s.Kind == AnnotationKind.Arrow ? y1 : Math.Min(y1, y2);
            var label = $"#{s.DefectId}";
            var lw = font * (0.62 * label.Length + 0.5);
            var bx = Math.Clamp(lx, 0, Math.Max(0, w - lw));
            var by = Math.Clamp(ly - font * 1.25, 0, Math.Max(0, h - font * 1.25));
            sb.Append($"<rect x=\"{Num(bx)}\" y=\"{Num(by)}\" width=\"{Num(lw)}\" height=\"{Num(font * 1.2)}\" rx=\"{Num(font * 0.2)}\" fill=\"#FF2D2D\"/>")
              .Append($"<text x=\"{Num(bx + font * 0.25)}\" y=\"{Num(by + font * 0.95)}\" font-size=\"{Num(font)}\" font-family=\"Arial\" font-weight=\"bold\" fill=\"#fff\">{E(label)}</text>");
        }

        sb.Append($"</svg><figcaption>{E(photo)}{(file != photo ? " · arka plan kaldırıldı" : "")}</figcaption></figure>");
        return sb.ToString();
    }

    private static string MeasCell(string label, string value) =>
        $"<div class=\"mc\"><div class=\"ml\">{E(label)}</div><div class=\"mv\">{E(value)}</div></div>";

    private static string SnapshotText(MeasurementSnapshot? s)
    {
        if (s is null)
            return "—";

        var parts = new List<string>();
        if (s.Depth is double d) parts.Add($"D {Num(d)}");
        if (s.Width is double w) parts.Add($"W {Num(w)}");
        if (s.Length is double l) parts.Add($"L {Num(l)}");
        if (s.Radius is double r) parts.Add($"R {Num(r)}");
        if (s.AngleDeg is double a) parts.Add($"A {Num(a)}°");
        if (!string.IsNullOrWhiteSpace(s.Color)) parts.Add(s.Color!);
        return parts.Count == 0 ? "—" : string.Join(" · ", parts);
    }

    private static string Mm(double? v) => v is double x ? $"{Num(x)} mm" : "—";

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string CssClass(DefectState s) => s switch
    {
        DefectState.Closed => "ok",
        DefectState.Voided => "void",
        DefectState.Superseded => "link",
        _ => "open",
    };

    private static string StateText(DefectState s) => s switch
    {
        DefectState.Open => "Açık",
        DefectState.Reworking => "Rework",
        DefectState.Repairing => "Repair",
        DefectState.AwaitingReinspect => "Rework sonrası muayene bekliyor",
        DefectState.WaitingVendor => "Tedarikçide",
        DefectState.MrbPending => "MRB bekliyor",
        DefectState.Closed => "Kapandı",
        DefectState.Voided => "Void",
        DefectState.Superseded => "Devredildi",
        _ => s.ToString(),
    };

    /// <summary>Kullanıcı metinlerini HTML'e güvenle gömer. Null ise boş döner.</summary>
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    private const string Css = @"
        @page { size: A4 landscape; margin: 10mm; }
        * { box-sizing: border-box; }
        body { font-family: Arial, Helvetica, sans-serif; font-size: 10pt; color: #1a1a1a; margin: 0; background: #e9ecf2; }
        .page { width: 277mm; height: 190mm; background: #fff; margin: 8mm auto; padding: 8mm; position: relative; overflow: hidden; display: flex; flex-direction: column; }
        .hdr { border-bottom: 2px solid #1A3A5C; padding-bottom: 4px; margin-bottom: 8px; font-size: 9pt; }
        .hdr div:first-child { font-size: 11pt; color: #1A3A5C; }
        .defect-bar { display: flex; align-items: center; gap: 12px; background: #F3F5F9; border: 1px solid #DDE2EA; padding: 6px 10px; border-radius: 4px; }
        .defect-bar .id { font-size: 16pt; font-weight: bold; color: #1A3A5C; }
        .defect-bar .type { font-size: 12pt; font-weight: bold; flex: 1; }
        .state { padding: 3px 10px; border-radius: 3px; font-size: 9pt; font-weight: bold; }
        .st-ok { background: #DCFCE7; color: #15803D; }
        .st-open { background: #FEF3C7; color: #B45309; }
        .st-void { background: #FEE2E2; color: #B91C1C; }
        .st-link { background: #E0E7FF; color: #3730A3; }
        .body { display: flex; gap: 10px; flex: 1; margin-top: 8px; min-height: 0; }
        .photos { width: 56%; display: flex; flex-direction: column; gap: 6px; overflow: hidden; }
        .photos figure { margin: 0; flex: 1; min-height: 0; display: flex; flex-direction: column; }
        .photos img { max-width: 100%; max-height: 100%; object-fit: contain; border: 1px solid #DDE2EA; border-radius: 4px; background: #111; }
        .photos svg.mk { width: 100%; height: 100%; min-height: 0; flex: 1; border: 1px solid #DDE2EA; border-radius: 4px; background: #111; }
        .photos figcaption { font-size: 8pt; color: #777; text-align: center; }
        .no-photo { flex: 1; border: 1.5px dashed #B0B8C4; border-radius: 4px; display: flex; align-items: center; justify-content: center; color: #888; }
        .info { width: 44%; overflow: hidden; }
        .info h3 { font-size: 10pt; color: #1A3A5C; margin: 8px 0 4px; border-bottom: 1px solid #DDE2EA; padding-bottom: 2px; }
        .meas { display: grid; grid-template-columns: repeat(3, 1fr); gap: 4px; }
        .mc { background: #F3F5F9; border-radius: 3px; padding: 4px 6px; }
        .ml { font-size: 7pt; color: #777; text-transform: uppercase; }
        .mv { font-size: 10pt; font-weight: bold; }
        .note { font-size: 9pt; margin: 6px 0; }
        .link { font-size: 9pt; background: #FFF8E1; border-left: 3px solid #F59E0B; padding: 4px 8px; margin: 4px 0; }
        table { width: 100%; border-collapse: collapse; font-size: 8pt; }
        th, td { border: 1px solid #DDE2EA; padding: 3px 5px; vertical-align: top; text-align: left; }
        th { background: #1A3A5C; color: #fff; }
        .hist td:nth-child(5) { max-width: 40mm; }
        .summary table { font-size: 10pt; }
        .summary h2 { color: #1A3A5C; font-size: 13pt; margin: 6px 0; }
        .muted { color: #888; }
        footer { position: absolute; left: 8mm; right: 8mm; bottom: 4mm; display: flex; justify-content: space-between; font-size: 7.5pt; color: #999; border-top: 1px solid #DDE2EA; padding-top: 3px; }
        @media print { body { background: #fff; } .page { margin: 0; box-shadow: none; page-break-after: always; } }
    ";
}
