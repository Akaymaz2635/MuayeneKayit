using System.Runtime.InteropServices;
using System.Text;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Windows oturumundaki kullanıcı. Kullanıcı adı (sicil no) Environment'tan, görünen ad Active Directory'den
/// (GetUserNameEx / NameDisplay) okunur. AD sorgusu yavaş olabileceği için ilk kullanımda bir kez yapılır.
/// </summary>
public sealed class WindowsUserAdapter : IWindowsUser
{
    private const int NameDisplay = 3;

    private readonly Lazy<string?> _displayName = new(ReadDisplayName);

    public string UserName => Environment.UserName;

    public string? DisplayName => _displayName.Value;

    private static string? ReadDisplayName()
    {
        var size = 256;
        var buffer = new StringBuilder(size);
        // Domain dışı (yerel) hesapta ya da domain denetleyicisine ulaşılamazsa başarısız olur: yalnızca sicil no kullanılır.
        return GetUserNameEx(NameDisplay, buffer, ref size) && buffer.Length > 0 ? buffer.ToString() : null;
    }

    [DllImport("secur32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserNameEx(int nameFormat, StringBuilder nameBuffer, ref int size);
}
