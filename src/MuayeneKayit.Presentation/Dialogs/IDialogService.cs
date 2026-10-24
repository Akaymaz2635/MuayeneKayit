using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Diyalogların çizimi için port. Akış ve kurallar ViewModel'de, pencere View'dadır.
/// Vazgeçilirse null / false döner.
/// </summary>
public interface IDialogService
{
    /// <summary>Karar penceresi; null = vazgeç.</summary>
    Task<DecisionInput?> AskDecisionAsync(DecisionRequest request);

    /// <summary>"Rework şimdi yapıldı mı?" (Reinspect için "Kontrol şimdi yapılıyor mu?"). true = sonucu şimdi gir.</summary>
    Task<bool> AskReinspectNowAsync(Decision decision, int defectId);

    /// <summary>Rework sonrası muayene penceresi; null = vazgeç.</summary>
    Task<ReinspectInput?> AskReinspectAsync(ReinspectRequest request);

    /// <summary>Son kararı geri alma; gerekçe ya da null (vazgeç).</summary>
    Task<string?> AskUndoReasonAsync(UndoRequest request);

    /// <summary>Void gerekçesi ya da null (vazgeç).</summary>
    Task<string?> AskVoidReasonAsync(int defectId, string defectType);

    /// <summary>Fotoğrafı kaldırma onayı.</summary>
    Task<bool> ConfirmRemovePhotoAsync(int defectId, string photo);
}
