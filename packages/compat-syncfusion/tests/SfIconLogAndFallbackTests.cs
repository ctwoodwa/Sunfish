using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Sunfish.Compat.Syncfusion.Tests;

/// <summary>
/// SfIcon.Name maps a curated subset of the ~1,500-value Syncfusion IconName enum. Values
/// outside the subset LogAndFallback. This test verifies the warning is emitted for
/// unmapped names.
///
/// Test uses a recording ILogger instead of a full Blazor renderer — the behavior under
/// test lives in <c>OnParametersSet</c> which can be invoked directly.
/// </summary>
public class SfIconLogAndFallbackTests
{
    [Fact]
    public void SfIcon_UnmappedName_EmitsWarning()
    {
        var logger = new RecordingLogger<Sunfish.Compat.Syncfusion.SfIcon>();
        var component = new Sunfish.Compat.Syncfusion.SfIcon();

        // Inject the logger via reflection (Blazor [Inject] properties aren't wired without a renderer).
        var loggerProp = typeof(Sunfish.Compat.Syncfusion.SfIcon)
            .GetProperty("Logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(loggerProp);
        loggerProp!.SetValue(component, logger);

        // Invoke the wrapper's parameter-set entry point via SetParametersAsync.
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Name"] = "ThisIconDoesNotExistInOurSubset"
        });
        // SetParametersAsync triggers OnParametersSet synchronously for a component with no async init.
        _ = component.SetParametersAsync(parameters);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Message.Contains("not in the curated subset"));
    }

    [Fact]
    public void SfIcon_MappedName_DoesNotWarn()
    {
        var logger = new RecordingLogger<Sunfish.Compat.Syncfusion.SfIcon>();
        var component = new Sunfish.Compat.Syncfusion.SfIcon();

        var loggerProp = typeof(Sunfish.Compat.Syncfusion.SfIcon)
            .GetProperty("Logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        loggerProp!.SetValue(component, logger);

        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Name"] = "Save"
        });
        _ = component.SetParametersAsync(parameters);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Empty(warnings);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public System.IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
