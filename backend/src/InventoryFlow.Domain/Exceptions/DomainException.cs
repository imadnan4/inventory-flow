namespace InventoryFlow.Domain.Exceptions;

/// <summary>
/// Represents a violated domain invariant.
/// </summary>
public sealed class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="message">A description of the violated invariant.</param>
    public DomainException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="message">A description of the violated invariant.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
