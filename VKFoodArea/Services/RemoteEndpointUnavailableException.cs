namespace VKFoodArea.Services;

public sealed class RemoteEndpointUnavailableException : InvalidOperationException
{
    public RemoteEndpointUnavailableException()
        : base("Remote endpoint is not configured for this build.")
    {
    }
}
