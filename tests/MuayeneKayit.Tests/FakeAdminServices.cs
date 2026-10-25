using MuayeneKayit.Presentation;

/// <summary>Onay penceresi: sıradaki cevabı verir (sıra boşsa vazgeçilmiş sayılır) ve çağrıları kaydeder.</summary>
public sealed class FakeConfirm : IConfirmService
{
    public Queue<bool> Answers { get; } = new();
    public List<(string Title, string Message, string ConfirmText)> Calls { get; } = new();

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Calls.Add((title, message, confirmText));
        return Task.FromResult(Answers.Count > 0 && Answers.Dequeue());
    }
}

/// <summary>Elle ilerletilen saat.</summary>
public sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 1, 1, 9, 0, 0);

    public void Advance(TimeSpan span) => Now += span;
}

/// <summary>Ayar dosyası yerine bellekte tutulan kök yolu.</summary>
public sealed class FakeAdminSettings : IAdminSettings
{
    public string Root { get; set; } = "";
    public List<string> Saved { get; } = new();

    public string LoadRootPath() => Root;

    public void SaveRootPath(string root)
    {
        Saved.Add(root);
        Root = root;
    }
}

/// <summary>Kaydetme penceresi: verilen yolu döndürür (null = vazgeç).</summary>
public sealed class FakeFileSaver : IFileSaver
{
    public string? Path { get; set; }
    public List<string> SuggestedNames { get; } = new();

    public Task<string?> PickCsvPathAsync(string suggestedName)
    {
        SuggestedNames.Add(suggestedName);
        return Task.FromResult(Path);
    }
}

/// <summary>Kabuk çağrılarını kaydeder.</summary>
public sealed class FakeShell : IShell
{
    public List<string> Folders { get; } = new();
    public List<string> Files { get; } = new();

    public void OpenFolder(string path) => Folders.Add(path);

    public void OpenFile(string path) => Files.Add(path);
}
