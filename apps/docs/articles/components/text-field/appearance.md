---
uid: component-textfield-appearance
title: Text Field Appearance
description: Customize SunfishTextField with prefix/suffix content, separators, and validation styling.
---

# Text Field Appearance

## Prefix and Suffix

Use the `Prefix` and `Suffix` render fragments to add icons, labels, or action buttons alongside the input:

```razor
<SunfishTextField @bind-Value="url" Placeholder="https://example.com">
    <Prefix>
        <SunfishIcon Name="globe" Size="IconSize.Small" />
    </Prefix>
    <Suffix>
        <SunfishIcon Name="check" Size="IconSize.Small" />
    </Suffix>
</SunfishTextField>
```

## Separators

Enable visual dividers between the prefix/suffix and the input:

```razor
<SunfishTextField @bind-Value="amount" ShowPrefixSeparator="true">
    <Prefix>$</Prefix>
</SunfishTextField>
```

## Validation state

Set `IsInvalid` to apply error styling:

```razor
<SunfishTextField @bind-Value="email" IsInvalid="@(!isValid)" Placeholder="Email" />
```

## See Also

- [Text Field Overview](xref:component-textfield-overview)
