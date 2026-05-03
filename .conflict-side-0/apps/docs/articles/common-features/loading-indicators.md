---
uid: common-features-loading-indicators
title: Loading Indicators
description: Using spinners, skeletons, and progress indicators in Sunfish applications.
---

# Loading Indicators

Sunfish provides a set of components for communicating async work and content loading state to users: spinners, skeleton placeholders, and progress indicators. Data components such as DataGrid also have built-in loading states controlled through a `Loading` parameter.

## SunfishSpinner

`SunfishSpinner` is an animated circular indicator for short, indeterminate async operations such as form submission or data refresh.

```razor
@if (isLoading)
{
    <SunfishSpinner Size="SpinnerSize.Medium" />
}
```

### Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Size` | `SpinnerSize` | `SpinnerSize.Medium` | Predefined size of the spinner. |
| `Color` | `string?` | `null` | CSS color for the spinner arc. Defaults to the primary color token. |
| `Label` | `string?` | `null` | Screen-reader text. Defaults to `"Loading..."`. |
| `Class` | `string?` | `null` | Additional CSS classes. |
| `Style` | `string?` | `null` | Inline styles. |

### Sizes

| Value | Approximate diameter |
|---|---|
| `SpinnerSize.Small` | 16 px |
| `SpinnerSize.Medium` | 24 px |
| `SpinnerSize.Large` | 36 px |

### Example: Button with Loading State

```razor
<SunfishButton OnClick="SaveAsync" Disabled="@isSaving">
    @if (isSaving)
    {
        <SunfishSpinner Size="SpinnerSize.Small" />
        <span>Saving...</span>
    }
    else
    {
        <span>Save</span>
    }
</SunfishButton>

@code {
    private bool isSaving;

    private async Task SaveAsync()
    {
        isSaving = true;
        await DataService.SaveAsync(model);
        isSaving = false;
    }
}
```

## SunfishSkeleton

`SunfishSkeleton` renders an animated shimmer placeholder while content is loading. Use it to match the approximate shape of the content that will appear, reducing perceived wait time.

```razor
@if (isLoading)
{
    <SunfishSkeleton Variant="SkeletonVariant.Text" Width="200px" />
    <SunfishSkeleton Variant="SkeletonVariant.Text" Width="160px" />
    <SunfishSkeleton Variant="SkeletonVariant.Rectangle" Width="100%" Height="120px" />
}
else
{
    <h2>@article.Title</h2>
    <p>@article.Subtitle</p>
    <img src="@article.ThumbnailUrl" />
}
```

### Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Variant` | `SkeletonVariant` | `SkeletonVariant.Text` | Shape of the placeholder. |
| `Width` | `string?` | `"100%"` | CSS width. |
| `Height` | `string?` | `null` | CSS height. Defaults to a value appropriate for the variant. |
| `Animated` | `bool` | `true` | Whether the shimmer animation plays. |
| `Class` | `string?` | `null` | Additional CSS classes. |
| `Style` | `string?` | `null` | Inline styles. |

### Variants

| Value | Use for |
|---|---|
| `SkeletonVariant.Text` | Single lines of text. Height defaults to the current line-height. |
| `SkeletonVariant.Circle` | Avatars, icons, circular badges. Width and height should match. |
| `SkeletonVariant.Rectangle` | Images, cards, banners, or any rectangular block content. |

### Example: Card Skeleton

```razor
<SunfishCard Style="padding: 16px; display: flex; gap: 12px;">
    <SunfishSkeleton Variant="SkeletonVariant.Circle" Width="48px" Height="48px" />
    <div style="flex: 1; display: flex; flex-direction: column; gap: 8px;">
        <SunfishSkeleton Variant="SkeletonVariant.Text" Width="60%" />
        <SunfishSkeleton Variant="SkeletonVariant.Text" Width="40%" />
    </div>
</SunfishCard>
```

## SunfishProgressBar

`SunfishProgressBar` displays a horizontal bar that fills to indicate progress. It supports both determinate (known percentage) and indeterminate (unknown duration) modes.

```razor
<!-- Determinate: 65% complete -->
<SunfishProgressBar Value="65" Max="100" />

<!-- Indeterminate: unknown duration -->
<SunfishProgressBar Indeterminate="true" />

<!-- With label -->
<SunfishProgressBar Value="@uploadedBytes" Max="@totalBytes" ShowLabel="true" />
```

### Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Value` | `double` | `0` | Current progress value. |
| `Max` | `double` | `100` | Maximum value (100% full). |
| `Indeterminate` | `bool` | `false` | Plays an animated sweep when the total duration is unknown. |
| `ShowLabel` | `bool` | `false` | Displays the percentage as text inside or above the bar. |
| `Color` | `string?` | `null` | CSS color for the filled track. Defaults to the primary color token. |
| `Height` | `string?` | `null` | CSS height of the bar. |
| `Class` | `string?` | `null` | Additional CSS classes. |
| `Style` | `string?` | `null` | Inline styles. |

### Example: File Upload Progress

```razor
<SunfishProgressBar Value="@bytesUploaded" Max="@fileSize" ShowLabel="true" />
<p>@((bytesUploaded / (double)fileSize * 100):F0)% uploaded</p>

@code {
    private long bytesUploaded;
    private long fileSize;
}
```

## SunfishProgressCircle

`SunfishProgressCircle` is a circular variant of the progress indicator. Use it when horizontal space is limited or a radial representation suits the context better (for example, dashboard tiles or compact card headers).

```razor
<!-- Determinate -->
<SunfishProgressCircle Value="72" Max="100" ShowLabel="true" />

<!-- Indeterminate -->
<SunfishProgressCircle Indeterminate="true" Size="ProgressCircleSize.Large" />
```

### Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Value` | `double` | `0` | Current progress value. |
| `Max` | `double` | `100` | Maximum value. |
| `Indeterminate` | `bool` | `false` | Animated sweep for unknown duration. |
| `Size` | `ProgressCircleSize` | `ProgressCircleSize.Medium` | Predefined size of the circle. |
| `ShowLabel` | `bool` | `false` | Renders the percentage in the center of the circle. |
| `Color` | `string?` | `null` | CSS color for the progress arc. |
| `Class` | `string?` | `null` | Additional CSS classes. |
| `Style` | `string?` | `null` | Inline styles. |

## Component Built-in Loading States

Data-heavy components manage their own loading UI when you set their `Loading` parameter. The component renders a built-in spinner overlay on top of its content area and disables user interaction until loading is complete.

### DataGrid

```razor
<SunfishDataGrid TItem="Order"
                OnRead="@LoadOrders"
                Loading="@isLoading">
    <SunfishGridColumn Field="@nameof(Order.Id)" Title="Order #" />
    <SunfishGridColumn Field="@nameof(Order.Total)" Title="Total" />
</SunfishDataGrid>

@code {
    private bool isLoading;

    private async Task LoadOrders(GridReadEventArgs<Order> args)
    {
        isLoading = true;
        // ... fetch data ...
        isLoading = false;
    }
}
```

When using `OnRead`, the DataGrid sets its own internal loading indicator automatically for each read operation. Set `Loading` explicitly only when you need to control the state independently — for example, during an initial page load before the first `OnRead` fires.

### ListView

`SunfishListView` supports the same `Loading` parameter. The built-in spinner is centered within the list's content area.

```razor
<SunfishListView TItem="Article"
                Data="@articles"
                Loading="@isFetching" />
```

### Combining Loading Indicators

For full-page or section loading states, wrap the content area in a container and overlay a `SunfishSpinner` or `SunfishSkeleton` using CSS positioning:

```razor
<div style="position: relative; min-height: 200px;">
    @if (isLoading)
    {
        <div style="position: absolute; inset: 0; display: flex; align-items: center; justify-content: center;">
            <SunfishSpinner Size="SpinnerSize.Large" />
        </div>
    }
    else
    {
        <ArticleList Items="@articles" />
    }
</div>
```
