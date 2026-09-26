using BloodLink.Web.Components.Needs;

namespace BloodLink.Web.Tests;

public sealed class NeedSubmissionGuardTests
{
    [Fact]
    public void ConcurrentEvents_AllowOnlyOneSubmission()
    {
        var guard = new NeedSubmissionGuard();
        var successes = 0;

        Parallel.For(0, 100, _ =>
        {
            if (guard.TryBegin())
            {
                Interlocked.Increment(ref successes);
            }
        });

        Assert.Equal(1, successes);
    }

    [Fact]
    public void SuccessfulSubmission_RejectsQueuedDuplicate()
    {
        var guard = new NeedSubmissionGuard();

        Assert.True(guard.TryBegin());
        Assert.False(guard.TryBegin());
        guard.Complete(submitted: true);

        Assert.False(guard.TryBegin());
    }

    [Fact]
    public void FailedSubmission_AllowsRetry()
    {
        var guard = new NeedSubmissionGuard();

        Assert.True(guard.TryBegin());
        guard.Complete(submitted: false);

        Assert.True(guard.TryBegin());
    }
}
