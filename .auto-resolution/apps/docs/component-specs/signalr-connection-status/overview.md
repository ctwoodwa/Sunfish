---
title: SignalR Connection Status
page_title: SignalR Connection Status | Sunfish Blazor
description: Displays aggregate SignalR hub health as a toolbar icon with a popup listing every registered hub connection's state, health, and reconnect activity.
slug: components/signalr-connection-status/overview
tags: sunfish,blazor,signalr,connection,status,health
published: True
position: 0
components: ["signalr-connection-status"]
---

# SignalR Connection Status

The `SunfishSignalRConnectionStatus` component displays a wireless icon indicating aggregate health of all registered SignalR hub connections. Users can click the icon to open a popup listing every hub with its current state, last error, and reconnect activity.

## Basic Usage

Place the component in your app shell, toolbar, or status area:

````RAZOR
<SunfishSignalRConnectionStatus />
````

## Parameters

| Name | Type | Default | Description |
|------|------|---------|-------------|
| `ShowCounts` | `bool` | `true` | Show healthy/total count beside the icon |
| `IncludeNonCritical` | `bool` | `true` | Include optional hubs in the popup |
| `PopupPlacement` | `ConnectionPopupPlacement` | `BottomEnd` | Flyout alignment |
| `Title` | `string` | `"Real-time connections"` | Popup header text |
| `Compact` | `bool` | `false` | Smaller density for toolbar use |
| `Class` | `string?` | `null` | Additional CSS class |
| `Style` | `string?` | `null` | Additional inline style |

## Service Setup

Register the `ISignalRConnectionRegistry` service and your hubs in `Program.cs`:

````RAZOR
builder.Services.AddSingleton<ISignalRConnectionRegistry, SignalRConnectionRegistry>();

// After app starts:
var registry = app.Services.GetRequiredService<ISignalRConnectionRegistry>();
await registry.RegisterAsync(new SignalRHubRegistration(
    Name: "Notifications",
    Endpoint: "/hubs/notifications",
    IsCritical: true,
    Factory: sp => new HubConnectionBuilder()
        .WithUrl(nav.ToAbsoluteUri("/hubs/notifications"))
        .WithAutomaticReconnect()
        .Build()));
await registry.StartAllAsync();
````

## Aggregate State

| State | Icon | When |
|-------|------|------|
| Healthy | Wireless | All critical hubs connected |
| Degraded | Wireless + amber | Critical hub reconnecting |
| Offline | Wireless-slash | Critical hub disconnected |
| Partial | Wireless + info | Only noncritical hubs unhealthy |

## See Also

- [Appearance](slug:components/signalr-connection-status/appearance)
- [Accessibility](slug:components/signalr-connection-status/accessibility)
