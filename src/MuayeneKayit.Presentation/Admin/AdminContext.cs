using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Admin oturumunun ortak durumu: kök klasör, depo ve denetim kaydı. Kök değişince (Ayarlar)
/// depo ve denetim kaydı yeni köke göre yeniden kurulur; tüm ViewModel'ler aynı nesneyi paylaşır.
/// </summary>
public sealed class AdminContext
{
    private readonly Func<SessionStore, IAuditTrail> _auditFactory;

    public AdminContext(string root, Func<SessionStore, IAuditTrail>? auditFactory = null)
    {
        _auditFactory = auditFactory ?? (store => new FileAuditTrail(store));
        Root = root;
        Store = new SessionStore(root);
        Audit = _auditFactory(Store);
    }

    public string Root { get; private set; }
    public SessionStore Store { get; private set; }
    public IAuditTrail Audit { get; private set; }

    public void SetRoot(string root)
    {
        Root = root;
        Store = new SessionStore(root);
        Audit = _auditFactory(Store);
    }
}
