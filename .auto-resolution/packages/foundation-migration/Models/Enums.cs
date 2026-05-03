namespace Sunfish.Foundation.Migration;

/// <summary>Form-factor discriminator per ADR 0028-A5.1 + A8.4 expanded migration table.</summary>
public enum FormFactorKind
{
    Laptop,
    Desktop,
    Tablet,
    Phone,
    Watch,
    Headless,
    Iot,
    Vehicle,
}

/// <summary>Input modality the form factor exposes.</summary>
public enum InputModalityKind
{
    Pointer,
    Keyboard,
    Touch,
    Voice,
    Pen,
    GestureSensor,
    None,
}

/// <summary>Display-class discriminator for the form factor's primary output surface.</summary>
public enum DisplayClassKind
{
    Large,
    Medium,
    Small,
    MicroDisplay,
    NoDisplay,
}

/// <summary>Network-posture the form factor sustains.</summary>
public enum NetworkPostureKind
{
    AlwaysConnected,
    IntermittentConnected,
    OfflineFirst,
    AirGapped,
}

/// <summary>Power profile the form factor exhibits.</summary>
public enum PowerProfileKind
{
    Wallpower,
    Battery,
    LowPower,
    IntermittentBattery,
}

/// <summary>Sensor surface available on the form factor.</summary>
public enum SensorKind
{
    Camera,
    Mic,
    Gps,
    Accelerometer,
    BiometricAuth,
    NfcReader,
    BarcodeScanner,
}

/// <summary>What triggered a re-profile / migration evaluation per A5.3.</summary>
public enum TriggeringEventKind
{
    StorageBudgetChanged,
    NetworkPostureChanged,
    SensorPermissionChanged,
    PowerProfileChanged,
    AdapterUpgrade,
    AdapterDowngrade,
    ManualReprofile,
}

/// <summary>
/// Sequestration flag per A8.3 — distinguishes the various reasons a record / field is held
/// in the sequestration partition rather than the active surface.
/// </summary>
public enum SequestrationFlagKind
{
    /// <summary>The record's form-factor capabilities filter excluded it from the derived surface (A5.1).</summary>
    FormFactorFilteredOut,

    /// <summary>The form factor's storage budget was exceeded; record sequestered to honor the budget.</summary>
    StorageBudgetExceeded,

    /// <summary>Plaintext payload was sequestered (A8.3 rule 5; the form factor cannot decrypt the field).</summary>
    PlaintextSequestered,

    /// <summary>Ciphertext payload was sequestered (A8.3 rule 5; the form factor cannot store the ciphertext).</summary>
    CiphertextSequestered,

    /// <summary>The record is sequestered + the form factor is ineligible for CP-record quorum participation per A8.3 rule 6.</summary>
    FormFactorQuorumIneligible,
}
