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

    Defect AddDefect(string folder, string defectType, Action<Defect>? configure = null);

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

    (Disposition Undone, int? VoidedChildId) UndoLastDecision(string folder, int defectId, string by, string? note);
}
