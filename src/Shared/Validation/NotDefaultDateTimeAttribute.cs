namespace Shared.Validation;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Validates that a <see cref="DateTime"/> value was actually supplied.
/// <c>[Required]</c> is a no-op on non-nullable value types, so a missing JSON
/// field silently binds to <c>default(DateTime)</c> (0001-01-01). This attribute
/// rejects that default (and null) so an absent date surfaces as a validation error.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class NotDefaultDateTimeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is DateTime dateTime && dateTime != default;
    }
}
