using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Admin "Bakım" sayfası: indeks yenileme, denetim zinciri doğrulama, son denetim satırları, eski kilitler.</summary>
public sealed partial class MaintenanceViewModel : AdminViewModelBase
{
    private const int AuditTail = 300;

    /// <summary>Listede gösterilen satır uzunluğu sınırı: çok uzun tek satırlık ayrıntılar (yüzlerce dosya özeti) arayüzü yavaşlatmasın; tamamı denetim dosyasında durur.</summary>
    private const int MaxLineChars = 400;

    private readonly AdminContext _ctx;
    private readonly IConfirmService _confirm;

    public MaintenanceViewModel(AdminContext ctx, IConfirmService confirm)
    {
        _ctx = ctx;
        _confirm = confirm;
    }

    [ObservableProperty] private IReadOnlyList<string> _auditLines = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _staleLocks = Array.Empty<string>();
    [ObservableProperty] private string? _selectedLock;

    public void RebuildIndex() => Run(() =>
    {
        var rows = IndexBuilder.Rebuild(_ctx.Store);
        Say($"Index yeniden oluşturuldu: {rows.Count} kayıt.", StatusKind.Success);
    });

    /// <summary>
    /// Zinciri ve kayıtları doğrular: zincir kırığı, günlüğün kısalması / silinmesi ve denetimden sonra elle değiştirilmiş
    /// kayıt dosyaları ("kayıt denetim dışı değiştirilmiş"). Bulgular denetim listesinin başında gösterilir.
    /// </summary>
    public void VerifyAudit() => Run(() =>
    {
        var check = AuditLog.Check(_ctx.Store);
        var all = check.All;
        if (check.Ok)
            Say("Denetim zinciri sağlam.", StatusKind.Success);
        else if (check.BadLine is int bad)
            Say($"Denetim zinciri {bad}. satırda bozuk: kayıt değiştirilmiş ya da silinmiş." +
                (all.Count > 1 ? $" (+{all.Count - 1} bulgu daha; listeye bakın)" : ""), StatusKind.Error);
        else
            Say($"Denetim doğrulaması başarısız: {all[0]}" + (all.Count > 1 ? $" (+{all.Count - 1} bulgu daha; listeye bakın)" : ""), StatusKind.Error);

        LoadAudit();
        if (!check.Ok)
            AuditLines = all.Select(x => "!! " + x).Concat(AuditLines).ToList();
    });

    /// <summary>Son 300 denetim satırını okur.</summary>
    public void LoadAudit() => Run(() =>
    {
        AuditLines = AuditLog.ReadTail(_ctx.Store, AuditTail)
            .Select(a => $"{(DateTime.TryParse(a.Ts, out var ts) ? ts.ToString("dd.MM.yyyy HH:mm:ss") : a.Ts)}   {a.Actor,-14}   {a.Action,-22}   {ShortenHashes(a.Details)}")
            .Select(l => l.Length > MaxLineChars ? l[..MaxLineChars] + "…" : l)
            .ToList();
    });

    /// <summary>Ayrıntıdaki kayıt özetleri (64 hane) listede 8 haneye kısaltılır; denetim dosyasında tam hali durur.</summary>
    private static string ShortenHashes(string details) =>
        System.Text.RegularExpressions.Regex.Replace(details, "=([0-9a-f]{8})[0-9a-f]{56}", "=$1…");

    public void ScanLocks() => Run(() =>
    {
        var locks = SharedConfig.StaleLocks(_ctx.Root);
        StaleLocks = locks.ToList();
        Say(locks.Count == 0 ? "Eski kilit yok." : $"{locks.Count} eski kilit bulundu.",
            locks.Count == 0 ? StatusKind.Success : StatusKind.Warning);
    });

    public async Task RemoveLockAsync()
    {
        if (SelectedLock is not string path)
        {
            Say("Listeden bir kilit seçin.", StatusKind.Error);
            return;
        }
        var owner = FileLock.DescribeOwner(path);
        if (!await _confirm.ConfirmAsync("Kilidi sil",
                $"{path}{(owner is null ? "" : $"\nSahibi: {owner}")}\n\nBu kilit 10 dakikadan eski ya da ileri tarihli. Silmeden önce o kayıtta çalışan kimse olmadığından emin olun; " +
                "işlem denetim kaydına yazılır.", "Kilidi sil"))
            return;

        Run(() =>
        {
            SharedConfig.RemoveStaleLock(_ctx.Root, path);
            _ctx.Audit.Append("admin", "admin.lock.remove", path);
            StaleLocks = SharedConfig.StaleLocks(_ctx.Root).ToList();
            Say("Kilit silindi.", StatusKind.Success);
        });
    }
}
