using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Karar penceresinin zorunlu alan kuralı.</summary>
public static class DecisionForm
{
    /// <summary>Not zorunluysa (Reopen) boş olamaz; kapsam sorulduysa (high metal'li rework) seçilmiş olmalı.</summary>
    public static bool CanSave(bool noteRequired, string? note, bool needsScope, ReworkScope? scope) =>
        (!noteRequired || !string.IsNullOrWhiteSpace(note)) && (!needsScope || scope is not null);
}
