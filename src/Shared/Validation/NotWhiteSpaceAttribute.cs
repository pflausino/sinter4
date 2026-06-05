namespace Shared.Validation;

using System.ComponentModel.DataAnnotations;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class NotWhiteSpaceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is not string text || !string.IsNullOrWhiteSpace(text);
    }
}
