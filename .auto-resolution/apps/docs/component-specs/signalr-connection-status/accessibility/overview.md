---
title: SignalR Connection Status Accessibility
page_title: Accessibility | SignalR Connection Status | Sunfish Blazor
description: Keyboard interactions and ARIA attributes for the SignalR connection status component.
slug: components/signalr-connection-status/accessibility
tags: sunfish,blazor,signalr,accessibility,a11y
published: True
position: 2
components: ["signalr-connection-status"]
---

# Accessibility

## Keyboard Interactions

| Key | Action |
|-----|--------|
| Enter / Space | Toggle popup open/close |
| Escape | Close popup |
| Tab | Move focus through popup rows |

## ARIA Attributes

The trigger button uses:
- `aria-label="SignalR connection status"`
- `aria-haspopup="dialog"`
- `aria-expanded` bound to popup open state

The popup uses:
- `role="dialog"`
- `aria-labelledby` pointing to the header

## Screen Reader Support

- Each hub row exposes textual health status (e.g. "Healthy", "Offline"), not color alone
- The tooltip provides a single-line summary (e.g. "SignalR: 1 of 3 hubs unhealthy")
- Status badge text matches the `ConnectionHealthState` enum label
