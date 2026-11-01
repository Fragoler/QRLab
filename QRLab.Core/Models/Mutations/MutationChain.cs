using QRLab.Core.Models.Images;

namespace QRLab.Core.Models.Mutations;

/// <summary>Owns the applied frames and the tail removed by Undo.</summary>
public sealed class MutationChain
{
    private readonly Stack<MutationFrame> _undone = new();
    public IReadOnlyList<MutationFrame> Frames { get; private set; }
    public long Revision { get; private set; }
    public bool CanUndo => Frames.Count > 0;
    public bool CanRedo => _undone.Count > 0;
    public event EventHandler? Changed;

    public MutationChain(IReadOnlyList<MutationFrame> frames) => Frames = Array.AsReadOnly(frames.ToArray());

    public RasterImage CurrentImage(RasterImage original) => Frames.LastOrDefault()?.Image ?? original;

    internal void Replace(IReadOnlyList<MutationFrame> frames)
    {
        Frames = Array.AsReadOnly(frames.ToArray());
        _undone.Clear();
        NotifyChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _undone.Push(Frames[^1]);
        Frames = Array.AsReadOnly(Frames.SkipLast(1).ToArray());
        NotifyChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        Frames = Array.AsReadOnly(Frames.Append(_undone.Pop()).ToArray());
        NotifyChanged();
    }

    public void Reset() => Replace([]);
    public void ClearRedo() => _undone.Clear();

    private void NotifyChanged()
    {
        Revision++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
