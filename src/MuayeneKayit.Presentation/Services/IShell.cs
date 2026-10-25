namespace MuayeneKayit.Presentation;

/// <summary>İşletim sistemi kabuğu portu: klasörü Gezgin'de ya da dosyayı varsayılan uygulamasıyla açar.</summary>
public interface IShell
{
    void OpenFolder(string path);

    void OpenFile(string path);
}
