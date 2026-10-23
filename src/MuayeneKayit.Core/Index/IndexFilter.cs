namespace MuayeneKayit.Core;

/// <summary>Dashboard'daki durum süzgeci.</summary>
public enum IndexStateFilter
{
    All,
    HasOpen,
    Completed,
    MrbPending,
    AwaitingReinspect,
    WaitingVendor,
}

/// <summary>Dashboard arama kriterleri. Boş metin alanları süzgeç uygulamaz.</summary>
public sealed record IndexCriteria(
    string? Part = null,
    string? Operation = null,
    string? Serial = null,
    string? DefectType = null,
    IndexStateFilter State = IndexStateFilter.All);

/// <summary>Index satırlarını kriterlere göre süzer. Metin aramaları büyük/küçük harf duyarsız "içerir" aramasıdır.</summary>
public static class IndexFilter
{
    public static List<IndexRow> Apply(IEnumerable<IndexRow> rows, IndexCriteria c) =>
        rows.Where(r =>
                Matches(r.Part, c.Part) &&
                Matches(r.Operation, c.Operation) &&
                Matches(r.Serial, c.Serial) &&
                (string.IsNullOrWhiteSpace(c.DefectType) ||
                 r.Defects.Any(d => string.Equals(d.Type, c.DefectType, StringComparison.OrdinalIgnoreCase))) &&
                MatchesState(r, c.State))
            .OrderByDescending(r => r.OpenDefects)
            .ThenBy(r => r.Part, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Operation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Serial, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool Matches(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesState(IndexRow r, IndexStateFilter state) => state switch
    {
        IndexStateFilter.HasOpen => r.OpenDefects > 0,
        IndexStateFilter.Completed => r.Status == nameof(SessionStatus.Completed),
        IndexStateFilter.MrbPending => r.Defects.Any(d => d.State == nameof(DefectState.MrbPending)),
        IndexStateFilter.AwaitingReinspect => r.Defects.Any(d => d.State == nameof(DefectState.AwaitingReinspect)),
        IndexStateFilter.WaitingVendor => r.Defects.Any(d => d.State == nameof(DefectState.WaitingVendor)),
        _ => true,
    };
}
