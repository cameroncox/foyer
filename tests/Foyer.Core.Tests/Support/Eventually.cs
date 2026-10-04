namespace Foyer.Core.Tests.Support;

public static class Eventually
{
    /// <summary>Polls <paramref name="condition"/> until it holds, failing after <paramref name="timeout"/>.</summary>
    public static async Task HoldsAsync(Func<Task<bool>> condition, TimeSpan? timeout = null, string? because = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new ShouldAssertException($"Condition didn't hold in time{(because is null ? "" : ": " + because)}.");
            }

            await Task.Delay(25);
        }
    }

    public static Task HoldsAsync(Func<bool> condition, TimeSpan? timeout = null, string? because = null) =>
        HoldsAsync(() => Task.FromResult(condition()), timeout, because);
}
