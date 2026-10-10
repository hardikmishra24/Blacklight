using TipShield.Gemma;
using Xunit;

namespace TipShield.Gemma.Tests;

public class CircuitBreakerTests
{
    [Fact]
    public void StartsClosed()
    {
        var cb = new CircuitBreaker(5, TimeSpan.FromSeconds(60));
        Assert.Equal(CircuitBreaker.CircuitState.Closed, cb.State);
        Assert.True(cb.IsAvailable);
    }

    [Fact]
    public void OpensAfterThresholdFailures()
    {
        var cb = new CircuitBreaker(3, TimeSpan.FromSeconds(60));
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitBreaker.CircuitState.Closed, cb.State);
        cb.RecordFailure();
        Assert.Equal(CircuitBreaker.CircuitState.Open, cb.State);
        Assert.False(cb.IsAvailable);
    }

    [Fact]
    public async Task ResetsToHalfOpenAfterTimeout()
    {
        var cb = new CircuitBreaker(1, TimeSpan.FromMilliseconds(100));
        cb.RecordFailure();
        Assert.Equal(CircuitBreaker.CircuitState.Open, cb.State);
        
        await Task.Delay(150);
        
        Assert.Equal(CircuitBreaker.CircuitState.HalfOpen, cb.State);
        Assert.True(cb.IsAvailable);
    }

    [Fact]
    public async Task ClosesOnSuccessAfterHalfOpen()
    {
        var cb = new CircuitBreaker(1, TimeSpan.FromMilliseconds(100));
        cb.RecordFailure();
        await Task.Delay(150);
        Assert.Equal(CircuitBreaker.CircuitState.HalfOpen, cb.State);
        
        cb.RecordSuccess();
        
        Assert.Equal(CircuitBreaker.CircuitState.Closed, cb.State);
        Assert.True(cb.IsAvailable);
    }
}
