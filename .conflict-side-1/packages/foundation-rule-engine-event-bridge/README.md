# Sunfish.Foundation.RuleEngine.EventBridge

Wires the Sunfish `BusinessRuleEngine` to the kernel event bus — rules evaluate reactively when entity events arrive.

Spec §3.6 / gap G4.

## What this ships

### Bridge

- **`RuleEngineEventBridge`** — subscribes to kernel-event-bus topics + invokes the rule engine on each event matching a registered rule's trigger.
- **`IRuleTriggerRegistry`** — declarative registration of "rule X triggers on event Y" mappings.

### Reactive evaluation flow

```
Entity event published → kernel-event-bus
  → bridge filter (does any rule trigger on this event?)
    → BusinessRuleEngine.EvaluateAsync(rule, eventPayload, context)
      → rule outcome (action/notification/state-change/etc.)
```

The bridge does NOT itself execute rule outcomes — it returns the rule's verdict to the caller. Side effects (sending an email, transitioning a state, etc.) are the caller's responsibility, keeping the rule engine deterministic + side-effect-free.

## DI

```csharp
services.AddSunfishRuleEngineEventBridge();
```

(Requires both `BusinessRuleEngine` + `kernel-event-bus` to be registered upstream.)

## See also

- `Sunfish.Foundation.RuleEngine` — rule engine (separate package)
- [Sunfish.Kernel.EventBus](../kernel-event-bus/README.md) — event-bus substrate
