namespace MuayeneKayit.Presentation;

/// <summary>
/// Geri alınamayan ya da toplu değişiklikler için onay penceresi portu. true = onaylandı, false = vazgeçildi.
/// </summary>
public interface IConfirmService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
}
