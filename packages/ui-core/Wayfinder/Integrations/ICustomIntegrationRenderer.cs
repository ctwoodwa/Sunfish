using System;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Safety-valve escape hatch per ADR 0067 §6.3. Allows an adapter
/// package to register a fully-custom Razor / React component for
/// providers whose UX cannot be expressed via the standard
/// <see cref="IntegrationProviderSchema"/> + per-field rendering.
/// </summary>
/// <remarks>
/// <para>
/// <b>No v1 registrations.</b> The escape hatch ships in Phase 1 so
/// the surface is complete, but no production providers register a
/// custom renderer in v1. Council finding council-#7: any v1
/// registration MUST be approved by a council pre-merge review.
/// </para>
/// <para>
/// Cycle-safe: <see cref="Type"/> is a CLR reflection handle; no
/// foreign-package deps. Renderers reflection-bind at composition time.
/// </para>
/// </remarks>
public interface ICustomIntegrationRenderer
{
    /// <summary>The provider identifier this renderer supports.</summary>
    string SupportedProvider { get; }

    /// <summary>
    /// CLR <see cref="Type"/> of the Razor component for Anchor / Blazor
    /// adapters. Reflection-bound at composition time.
    /// </summary>
    Type RendererType { get; }

    /// <summary>
    /// React component module path for Bridge (e.g.,
    /// <c>"@sunfish/integrations-react/StripeCustom"</c>). The Bridge
    /// React adapter resolves this via the React module registry.
    /// </summary>
    string ReactComponentSpec { get; }
}
