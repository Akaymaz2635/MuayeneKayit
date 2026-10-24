namespace MuayeneKayit.Core;

/// <summary>Denetim kaydına yazma portu. Gerçek uygulama zincirli dosyaya yazar; testlerde sahtesi kullanılır.</summary>
public interface IAuditTrail
{
    void Append(string actor, string action, string details);
}

/// <summary>Ortak alandaki zincirli denetim dosyasına yazar (<see cref="AuditLog"/>).</summary>
public sealed class FileAuditTrail(SessionStore store) : IAuditTrail
{
    public void Append(string actor, string action, string details) =>
        AuditLog.Append(store, actor, action, details);
}
