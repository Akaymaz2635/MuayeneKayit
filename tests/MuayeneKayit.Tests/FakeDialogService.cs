using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

/// <summary>Sıralı cevaplar veren sahte diyalog servisi; gelen istekleri kaydeder. Cevap kalmadıysa vazgeçilmiş sayılır.</summary>
public sealed class FakeDialogService : IDialogService
{
    public Queue<DecisionInput?> Decisions { get; } = new();
    public Queue<bool> ReinspectNow { get; } = new();
    public Queue<ReinspectInput?> Reinspects { get; } = new();
    public Queue<string?> UndoReasons { get; } = new();
    public Queue<string?> VoidReasons { get; } = new();
    public Queue<bool> PhotoConfirms { get; } = new();

    public List<DecisionRequest> DecisionRequests { get; } = new();
    public List<(Decision Decision, int DefectId)> ReinspectNowCalls { get; } = new();
    public List<ReinspectRequest> ReinspectRequests { get; } = new();
    public List<UndoRequest> UndoRequests { get; } = new();
    public List<(int Id, string Type)> VoidRequests { get; } = new();
    public List<(int Id, string Photo)> PhotoRequests { get; } = new();

    public Task<DecisionInput?> AskDecisionAsync(DecisionRequest request)
    {
        DecisionRequests.Add(request);
        return Task.FromResult(Decisions.Count > 0 ? Decisions.Dequeue() : null);
    }

    public Task<bool> AskReinspectNowAsync(Decision decision, int defectId)
    {
        ReinspectNowCalls.Add((decision, defectId));
        return Task.FromResult(ReinspectNow.Count > 0 && ReinspectNow.Dequeue());
    }

    public Task<ReinspectInput?> AskReinspectAsync(ReinspectRequest request)
    {
        ReinspectRequests.Add(request);
        return Task.FromResult(Reinspects.Count > 0 ? Reinspects.Dequeue() : null);
    }

    public Task<string?> AskUndoReasonAsync(UndoRequest request)
    {
        UndoRequests.Add(request);
        return Task.FromResult(UndoReasons.Count > 0 ? UndoReasons.Dequeue() : null);
    }

    public Task<string?> AskVoidReasonAsync(int defectId, string defectType)
    {
        VoidRequests.Add((defectId, defectType));
        return Task.FromResult(VoidReasons.Count > 0 ? VoidReasons.Dequeue() : null);
    }

    public Task<bool> ConfirmRemovePhotoAsync(int defectId, string photo)
    {
        PhotoRequests.Add((defectId, photo));
        return Task.FromResult(PhotoConfirms.Count > 0 && PhotoConfirms.Dequeue());
    }
}
