using Autofac;

namespace SquiFlow.CoreApi.Composition;

internal sealed class ProfileRuntimeDefinition(
    ProfileRuntimeKey key,
    Action<ContainerBuilder> configure)
{
    internal ProfileRuntimeKey Key { get; } =
        key ?? throw new ArgumentNullException(nameof(key));

    internal Action<ContainerBuilder> Configure { get; } =
        configure ?? throw new ArgumentNullException(nameof(configure));
}
