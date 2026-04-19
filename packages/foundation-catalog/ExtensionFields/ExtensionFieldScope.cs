namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Declares who authored an extension field and therefore who may remove or modify it.
/// </summary>
public enum ExtensionFieldScope
{
    /// <summary>Declared by a bundle manifest; tenant admins cannot remove it.</summary>
    Bundle = 0,

    /// <summary>Declared by a tenant admin within a bundle-allowed policy; tenant owns the lifecycle.</summary>
    Tenant = 1,
}
