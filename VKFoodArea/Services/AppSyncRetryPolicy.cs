using System.Net;

namespace VKFoodArea.Services;

public static class AppSyncRetryPolicy
{
    public const int MaxAttempts = 6;

    public static TimeSpan GetDelay(int attemptCount)
    {
        return Math.Max(attemptCount, 1) switch
        {
            1 => TimeSpan.FromSeconds(15),
            2 => TimeSpan.FromMinutes(1),
            3 => TimeSpan.FromMinutes(5),
            4 => TimeSpan.FromMinutes(15),
            5 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(1)
        };
    }

    public static bool ShouldRetry(HttpStatusCode? statusCode, int nextAttemptCount)
    {
        if (nextAttemptCount > MaxAttempts)
            return false;

        if (!statusCode.HasValue)
            return true;

        var numericCode = (int)statusCode.Value;
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
               numericCode >= 500;
    }
}
