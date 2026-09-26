namespace BloodLink.Web.Components.Needs;

internal sealed class NeedSubmissionGuard
{
    private int _state;

    public bool TryBegin() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

    public void Complete(bool submitted) => Volatile.Write(ref _state, submitted ? 2 : 0);
}
