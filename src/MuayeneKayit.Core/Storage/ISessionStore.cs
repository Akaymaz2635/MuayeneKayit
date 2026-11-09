namespace MuayeneKayit.Core;

/// <summary>Oturum ve kusur dosyalarına erişim portu. ViewModel'ler somut depoya değil bu arayüze bağlanır.</summary>
public interface ISessionStore
{
    string Root { get; }

    string FolderFor(string part, string operation, string serial);

    (string Folder, Session Session) OpenOrCreate(Session input);

    Session? LoadSession(string folder);

    void SaveSession(string folder, Session session);

    List<Defect> LoadDefects(string folder);

    /// <summary>
    /// Yükleme sırasında atlanan (bozuk, adı ya da numarası uyuşmayan) dosyaların uyarıları. Çağrıldıkça liste boşalır;
    /// bozuk dosya yüklenmeye devam ettikçe uyarı yeniden üretilir.
    /// </summary>
    IReadOnlyList<string> TakeWarnings();

    /// <summary>Kusur klasöründeki okunamayan (bozuk, Id'si dosya adıyla uyuşmayan) kusur dosyalarının adları. Muayene kapatma ve rapor öncesi denetim için.</summary>
    IReadOnlyList<string> UnreadableDefectFiles(string folder);

    Defect AddDefect(string folder, string defectType, Action<Defect>? configure = null, LengthUnit unit = LengthUnit.Mm);

    Defect UpdateDefect(string folder, int defectId, Action<Defect> mutate);

    void UpdateDefects(string folder, Action<IDictionary<int, Defect>> mutate, IEnumerable<int> ids);

    Defect ContinueAsChild(
        string folder,
        int parentId,
        string childType,
        DecisionSource source,
        string decidedBy,
        string? note,
        string? reference);

    (Defect Parent, IReadOnlyList<Defect> Children) ApplyReinspect(
        string folder,
        int parentId,
        ReinspectResult result,
        string decidedBy,
        DecisionSource source = DecisionSource.Engineer,
        string? reference = null);

    int? VoidDefect(string folder, int defectId, string by, string note);

    (Disposition Undone, IReadOnlyList<int> VoidedChildIds) UndoLastDecision(string folder, int defectId, string by, string? note);
}
