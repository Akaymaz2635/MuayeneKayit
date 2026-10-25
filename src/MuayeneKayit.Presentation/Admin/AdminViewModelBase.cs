using CommunityToolkit.Mvvm.ComponentModel;

namespace MuayeneKayit.Presentation;

/// <summary>Admin ViewModel'lerinin ortak tabanı: bildirim olayı ve beklenen hataları yakalayan Run.</summary>
public abstract class AdminViewModelBase : ObservableObject
{
    /// <summary>Durum çubuğu bildirimi (ana pencere InfoBar'ı).</summary>
    public event Action<string, StatusKind>? Notify;

    protected void Say(string message, StatusKind kind) => Notify?.Invoke(message, kind);

    /// <summary>Beklenen hataları yakalar ve bildirir. Uygulama kapanmaz.</summary>
    protected void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Say(ex.Message, StatusKind.Error);
        }
    }
}
