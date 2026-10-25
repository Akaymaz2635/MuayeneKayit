namespace MuayeneKayit.Presentation;

/// <summary>Saat portu: PIN bekleme süresi ve boşta kilit testlerde sahte saatle sınanır.</summary>
public interface IClock
{
    DateTime Now { get; }
}

/// <summary>Sistem saati.</summary>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
