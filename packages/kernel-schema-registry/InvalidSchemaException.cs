namespace Sunfish.Kernel.Schema;

/// <summary>
/// Thrown by <see cref="ISchemaRegistry.RegisterAsync"/> when the supplied
/// JSON Schema text is not a syntactically valid JSON Schema document (e.g.
/// malformed JSON, invalid keyword usage that the validator rejects at
/// parse-time).
/// </summary>
public sealed class InvalidSchemaException : Exception
{
    /// <summary>Creates a new <see cref="InvalidSchemaException"/>.</summary>
    public InvalidSchemaException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a new <see cref="InvalidSchemaException"/> wrapping an underlying parser error.</summary>
    public InvalidSchemaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown by <see cref="ISchemaRegistry.ValidateAsync"/> when the supplied
/// <c>SchemaId</c> does not identify a schema currently registered with this
/// registry instance.
/// </summary>
public sealed class SchemaNotFoundException : Exception
{
    /// <summary>Creates a new <see cref="SchemaNotFoundException"/>.</summary>
    public SchemaNotFoundException(string message)
        : base(message)
    {
    }
}
