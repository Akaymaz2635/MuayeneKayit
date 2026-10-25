namespace MuayeneKayit.Presentation;

/// <summary>Windows'ta oturum açmış kullanıcı: kullanıcı adı (sicil no) ve Active Directory'deki görünen ad.</summary>
public interface IWindowsUser
{
    /// <summary>Domain öneki olmadan kullanıcı adı (ör. "12345").</summary>
    string UserName { get; }

    /// <summary>AD'deki görünen ad ("Ali Kaymaz"); domain dışı hesapta ya da AD'ye ulaşılamazsa null.</summary>
    string? DisplayName { get; }
}
