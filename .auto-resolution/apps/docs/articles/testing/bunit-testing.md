---
uid: testing-bunit
title: Testing with bUnit
description: How to write unit tests for Sunfish components using bUnit v2.
---

# Testing with bUnit

Sunfish components are tested using [bUnit v2](https://bunit.dev/), a unit testing library for Blazor components. This article covers the key patterns for writing tests against Sunfish components, including the `SunfishTestBase` class, common test scenarios, and pitfalls specific to bUnit v2.

## bUnit v2 Basics

bUnit v2 provides `BunitContext` as the test host. Components are rendered with `RenderComponent<T>()`, and the resulting `IRenderedComponent<T>` is used to query the DOM and trigger events.

### Rendering a Component

```csharp
using Bunit;

public class MyTest : BunitContext
{
    [Fact]
    public void Button_RendersLabel()
    {
        var cut = RenderComponent<SunfishButton>(p => p
            .Add(b => b.Label, "Save"));

        cut.Find("button").TextContent.Contains("Save");
    }
}
```

### Querying the DOM

| Method | Description |
|--------|-------------|
| `cut.Find("selector")` | Returns the first matching element; throws if not found |
| `cut.FindAll("selector")` | Returns all matching elements |
| `cut.FindComponent<T>()` | Returns the first child component of type `T` |

### Triggering Events

```csharp
cut.Find("button").Click();
cut.Find("input").Input("hello");
cut.Find("input").Change("hello");
```

## SunfishTestBase

Sunfish provides `SunfishTestBase`, a convenience base class that inherits `BunitContext` and pre-registers all services that Sunfish components expect from the DI container. Without these registrations, components will throw during initialization.

Services registered by `SunfishTestBase`:

| Service | Purpose |
|---------|---------|
| `ISunfishCssProvider` | Provides CSS class names for the active provider |
| `IIconProvider` | Resolves icon SVG markup |
| `IJSRuntime` (mock) | Satisfies JS interop calls without a real browser |
| `ThemeService` | Manages active theme and dark mode state |
| `NotificationService` | Allows components to queue notifications |

### Using SunfishTestBase

```csharp
using Sunfish.Testing;

public class ButtonTests : SunfishTestBase
{
    [Fact]
    public void Button_Click_InvokesCallback()
    {
        var clicked = false;

        var cut = RenderComponent<SunfishButton>(p => p
            .Add(b => b.OnClick, EventCallback.Factory.Create(this, () => clicked = true)));

        cut.Find("button").Click();

        Assert.True(clicked);
    }
}
```

## Testing Component Interactions

### Example: Button Click

```csharp
[Fact]
public void Button_Click_UpdatesState()
{
    var callCount = 0;

    var cut = RenderComponent<SunfishButton>(p => p
        .Add(b => b.Label, "Submit")
        .Add(b => b.OnClick, EventCallback.Factory.Create(this, () => callCount++)));

    cut.Find("button").Click();

    Assert.Equal(1, callCount);
}
```

### Example: Form Validation

```csharp
[Fact]
public async Task TextField_ShowsValidationMessage_WhenRequired()
{
    var cut = RenderComponent<EditForm>(p => p
        .Add(f => f.Model, new MyModel())
        .AddChildContent<SunfishTextField>(tf => tf
            .Add(t => t.Label, "Name")
            .Add(t => t.Required, true)));

    // Submit to trigger validation
    cut.Find("form").Submit();

    await cut.WaitForStateAsync(() =>
        cut.FindAll(".mar-validation-message").Count > 0);

    Assert.Contains(cut.FindAll(".mar-validation-message"),
        el => el.TextContent.Contains("required"));
}
```

## Mocking Providers

To test that a component emits the correct CSS class output, swap the registered `ISunfishCssProvider` with a test double:

```csharp
[Fact]
public void Component_UsesExpectedCssClass()
{
    var mockProvider = new MockCssProvider();
    Services.AddSingleton<ISunfishCssProvider>(mockProvider);

    var cut = RenderComponent<SunfishButton>(p => p
        .Add(b => b.Variant, ButtonVariant.Primary));

    Assert.Contains("mar-button--primary", cut.Find("button").ClassList);
}
```

Register the mock before calling `RenderComponent` — `bUnit` resolves services at render time.

## bUnit v2 Parameter API

> [!IMPORTANT]
> bUnit v2 uses `cut.Render(p => p.Add(...))` to update parameters on a rendered component — **not** `SetParametersAndRender`. The v1 `SetParametersAndRender` method does not exist in bUnit v2 and will cause a build error.

Additionally, the v2 `Render` method does **not** merge with the previous parameter set. You must re-supply all parameters you want to keep when calling `Render`:

```csharp
// Initial render
var cut = RenderComponent<SunfishButton>(p => p
    .Add(b => b.Label, "Save")
    .Add(b => b.Disabled, false));

// Update — must re-supply Label if you want to keep it
cut.Render(p => p
    .Add(b => b.Label, "Save")
    .Add(b => b.Disabled, true));
```

## Common Pitfalls

### Async State Changes

Blazor processes events asynchronously. If a component performs async work in an event handler (e.g., an API call), the rendered output will not update synchronously after `Click()`. Use `WaitForStateAsync` to wait for the expected DOM condition:

```csharp
cut.Find("button").Click();

await cut.WaitForStateAsync(() =>
    cut.Find(".mar-spinner") is not null,
    timeout: TimeSpan.FromSeconds(2));
```

`WaitForAssertion` is an alternative that retries an `Assert` call until it passes or times out:

```csharp
await cut.WaitForAssertionAsync(() =>
    Assert.Contains("Saved", cut.Find(".status").TextContent));
```

### JS Interop

Components that call JS interop methods will fail in bUnit tests if the JS module has not been set up. `SunfishTestBase` registers a mock `IJSRuntime` that accepts and no-ops all invocations by default. If a test requires specific JS return values, configure the mock:

```csharp
JSInterop.SetupModule("_content/Sunfish.Core/js/sunfish-core.js")
    .Setup<bool>("isElementVisible", _ => true)
    .SetResult(true);
```

Refer to the [bUnit JSInterop documentation](https://bunit.dev/docs/test-doubles/js-interop.html) for the full API.

### Component Disposal During Async Operations

If a component is disposed while an async operation is in progress (e.g., the test ends before `await` completes), the component may attempt to call `StateHasChanged` after disposal, producing an `ObjectDisposedException`. Use `WaitForStateAsync` or `WaitForAssertionAsync` to ensure the operation completes before the test context is disposed.
