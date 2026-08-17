namespace Canary.Core.Config;

public sealed class CanaryConfigException : Exception
{
    public CanaryConfigException(string message)
        : base(message)
    {
    }

    public CanaryConfigException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
