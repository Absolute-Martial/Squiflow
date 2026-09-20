namespace Application.CoreApi.Composition;

internal sealed class ProfileRuntimeCapacityException : InvalidOperationException
{
    internal ProfileRuntimeCapacityException(int maximumRetainedRuntimes)
        : base($"The local profile runtime limit of {maximumRetainedRuntimes} has been reached.")
    {
    }
}
