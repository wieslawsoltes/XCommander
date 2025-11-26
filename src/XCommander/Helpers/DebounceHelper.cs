namespace XCommander.Helpers;

/// <summary>
/// Provides debouncing and throttling utilities for UI events.
/// </summary>
public static class DebounceHelper
{
    /// <summary>
    /// Creates a debounced action that delays invoking until after the specified delay
    /// has elapsed since the last time the debounced action was invoked.
    /// </summary>
    /// <param name="action">The action to debounce.</param>
    /// <param name="delayMs">The delay in milliseconds.</param>
    /// <returns>A debounced action.</returns>
    public static Action Debounce(Action action, int delayMs = 300)
    {
        System.Timers.Timer? timer = null;
        
        return () =>
        {
            timer?.Stop();
            timer?.Dispose();
            
            timer = new System.Timers.Timer(delayMs);
            timer.AutoReset = false;
            timer.Elapsed += (_, _) =>
            {
                timer.Dispose();
                action();
            };
            timer.Start();
        };
    }
    
    /// <summary>
    /// Creates a debounced action with a parameter.
    /// </summary>
    public static Action<T> Debounce<T>(Action<T> action, int delayMs = 300)
    {
        System.Timers.Timer? timer = null;
        T? lastArg = default;
        
        return arg =>
        {
            lastArg = arg;
            timer?.Stop();
            timer?.Dispose();
            
            timer = new System.Timers.Timer(delayMs);
            timer.AutoReset = false;
            timer.Elapsed += (_, _) =>
            {
                timer.Dispose();
                action(lastArg!);
            };
            timer.Start();
        };
    }
    
    /// <summary>
    /// Creates a throttled action that only allows execution once per specified interval.
    /// </summary>
    /// <param name="action">The action to throttle.</param>
    /// <param name="intervalMs">The minimum interval between invocations in milliseconds.</param>
    /// <returns>A throttled action.</returns>
    public static Action Throttle(Action action, int intervalMs = 100)
    {
        DateTime lastExecution = DateTime.MinValue;
        bool pending = false;
        System.Timers.Timer? pendingTimer = null;
        
        return () =>
        {
            var now = DateTime.Now;
            var elapsed = (now - lastExecution).TotalMilliseconds;
            
            if (elapsed >= intervalMs)
            {
                lastExecution = now;
                action();
            }
            else if (!pending)
            {
                pending = true;
                var remaining = intervalMs - elapsed;
                
                pendingTimer?.Stop();
                pendingTimer?.Dispose();
                
                pendingTimer = new System.Timers.Timer(remaining);
                pendingTimer.AutoReset = false;
                pendingTimer.Elapsed += (_, _) =>
                {
                    pendingTimer.Dispose();
                    pending = false;
                    lastExecution = DateTime.Now;
                    action();
                };
                pendingTimer.Start();
            }
        };
    }
    
    /// <summary>
    /// Creates a throttled action with a parameter.
    /// </summary>
    public static Action<T> Throttle<T>(Action<T> action, int intervalMs = 100)
    {
        DateTime lastExecution = DateTime.MinValue;
        bool pending = false;
        System.Timers.Timer? pendingTimer = null;
        T? lastArg = default;
        
        return arg =>
        {
            lastArg = arg;
            var now = DateTime.Now;
            var elapsed = (now - lastExecution).TotalMilliseconds;
            
            if (elapsed >= intervalMs)
            {
                lastExecution = now;
                action(arg);
            }
            else if (!pending)
            {
                pending = true;
                var remaining = intervalMs - elapsed;
                
                pendingTimer?.Stop();
                pendingTimer?.Dispose();
                
                pendingTimer = new System.Timers.Timer(remaining);
                pendingTimer.AutoReset = false;
                pendingTimer.Elapsed += (_, _) =>
                {
                    pendingTimer.Dispose();
                    pending = false;
                    lastExecution = DateTime.Now;
                    action(lastArg!);
                };
                pendingTimer.Start();
            }
        };
    }
}

/// <summary>
/// A debouncer that can be used to delay async actions.
/// </summary>
public sealed class AsyncDebouncer : IDisposable
{
    private readonly int _delayMs;
    private CancellationTokenSource? _cts;
    
    public AsyncDebouncer(int delayMs = 300)
    {
        _delayMs = delayMs;
    }
    
    /// <summary>
    /// Debounces the specified async action.
    /// </summary>
    public async Task DebounceAsync(Func<CancellationToken, Task> action)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(_delayMs, _cts.Token);
            await action(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when debounce resets
        }
    }
    
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
