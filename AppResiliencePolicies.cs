using System;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace Chat
{
    public static class AppResiliencePolicies
    {
        public static AsyncRetryPolicy CreateLoopRetryPolicy(string loopName, Action<object?> logAction)
        {
            return Policy
                .Handle<Exception>(ex => ex is not ObjectDisposedException && ex is not InvalidOperationException)
                .WaitAndRetryAsync(
                    retryCount: 10,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(500),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        logAction?.Invoke($"[POLLY] Loop '{loopName}' attempt {retryCount}/10 encountered {exception.GetType().Name}: {exception.Message}. Retrying in {timeSpan.TotalMilliseconds}ms...");
                    }
                );
        }

        public static AsyncRetryPolicy CreateIoRetryPolicy(string operationName, Action<object?> logAction)
        {
            return Policy
                .Handle<System.IO.IOException>()
                .Or<UnauthorizedAccessException>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * attempt),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        logAction?.Invoke($"[POLLY] I/O '{operationName}' attempt {retryCount}/5 failed: {exception.Message}. Retrying in {timeSpan.TotalMilliseconds}ms...");
                    }
                );
        }
    }
}
