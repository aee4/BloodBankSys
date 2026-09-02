namespace BloodLink.Domain.Exceptions;

/// <summary>
/// Base exception for all BloodLink domain exceptions.
/// </summary>
public abstract class BloodLinkException : Exception
{
    protected BloodLinkException(string message) : base(message) { }
    protected BloodLinkException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an operation violates inventory constraints (e.g., insufficient units).
/// </summary>
public sealed class InsufficientInventoryException : BloodLinkException
{
    public InsufficientInventoryException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a requested entity is not found.
/// </summary>
public sealed class EntityNotFoundException : BloodLinkException
{
    public EntityNotFoundException(string message) : base(message) { }
}

/// <summary>
/// Thrown when an operation is unauthorized for the current user.
/// </summary>
public sealed class UnauthorizedAccessException : BloodLinkException
{
    public UnauthorizedAccessException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a facility does not meet required status for an operation.
/// </summary>
public sealed class InvalidFacilityStatusException : BloodLinkException
{
    public InvalidFacilityStatusException(string message) : base(message) { }
}

/// <summary>
/// Thrown when an operation violates business rules or state constraints.
/// </summary>
public sealed class BusinessRuleViolationException : BloodLinkException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a concurrency conflict is detected (RowVersion mismatch).
/// </summary>
public sealed class ConcurrencyException : BloodLinkException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
