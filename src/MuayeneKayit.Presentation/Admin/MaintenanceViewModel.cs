using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Admin "Bakım" sayfası: indeks yenileme, denetim zinciri doğrulama, son denetim satırları, eski kilitler.</summary>
public sealed partial class MaintenanceViewModel : AdminViewModelBase
{
    private const int AuditTail = 300;

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

    public void VerifyAudit() => Run(() =>
    {
        var (ok, bad) = AuditLog.Verify(_ctx.Store);
        Say(ok ? "Denetim zinciri sağlam." : $"Denetim zinciri {bad}. satırda bozuk: kayıt değiştirilmiş ya da silinmiş.",
            ok ? StatusKind.Success : StatusKind.Error);
        LoadAudit();
    });

    /// <summary>Son 300 denetim satırını okur.</summary>
    public void LoadAudit() => Run(() =>
    {
        AuditLines = AuditLog.ReadTail(_ctx.Store, AuditTail)
            .Select(a => $"{(DateTime.TryParse(a.Ts, out var ts) ? ts.ToString("dd.MM.yyyy HH:mm:ss") : a.Ts)}   {a.Actor,-14}   {a.Action,-22}   {a.Details}")
            .ToList();
    });

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
        if (!await _confirm.ConfirmAsync("Kilidi sil",
                $"{path}\n\nBu kilit 10 dakikadan eski. Silmeden önce o kayıtta çalışan kimse olmadığından emin olun; " +
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
