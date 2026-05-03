namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Context passed to <see cref="ILocalNodePlugin.OnLoadAsync"/>. Lets a
/// plugin register its extension-point contributions and resolve services
/// from the host's DI container. The registry collects everything the plugin
/// registers and makes it available to the rest of the kernel.
/// </summary>
public interface IPluginContext
{
    /// <summary>Register a CRDT stream definition.</summary>
    void RegisterStream(IStreamDefinition stream);

    /// <summary>Register a read-model projection.</summary>
    void RegisterProjection(IProjectionBuilder projection);

    /// <summary>Register a schema version / upcaster.</summary>
    void RegisterSchemaVersion(ISchemaVersion schema);

    /// <summary>Register a UI block manifest.</summary>
    void RegisterUiBlock(IUiBlockManifest block);

    /// <summary>Host DI container — plugins resolve shared services from here.</summary>
    IServiceProvider Services { get; }
}
