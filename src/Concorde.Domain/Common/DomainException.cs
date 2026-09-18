namespace Concorde.Domain.Common;

/// <summary>
/// Base exception for all domain invariant violations.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) 
        : base(message, innerException) { }
}
