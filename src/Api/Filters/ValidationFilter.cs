namespace Api.Filters;

using System.ComponentModel.DataAnnotations;
using System.Reflection;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();

        if (argument is null)
            return Results.BadRequest(new { errors = new[] { "Request body is required." } });

        var validationResults = new List<ValidationResult>();

        // For C# records, validation attributes are placed on constructor parameters.
        // Validator.TryValidateObject may not find them on properties.
        // We need to check constructor parameter attributes and validate against property values.
        var constructor = typeof(T).GetConstructors().FirstOrDefault();
        if (constructor is not null)
        {
            foreach (var param in constructor.GetParameters())
            {
                var property = typeof(T).GetProperty(param.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is null) continue;

                var value = property.GetValue(argument);
                var attributes = param.GetCustomAttributes<ValidationAttribute>(true);
                var propertyContext = new ValidationContext(argument) { MemberName = property.Name };

                foreach (var attribute in attributes)
                {
                    var result = attribute.GetValidationResult(value, propertyContext);
                    if (result != ValidationResult.Success && result is not null)
                    {
                        validationResults.Add(result);
                    }
                }
            }
        }

        if (validationResults.Count > 0)
        {
            var errors = validationResults
                .Where(r => r.ErrorMessage is not null)
                .Select(r => r.ErrorMessage!)
                .Distinct()
                .ToArray();

            return Results.BadRequest(new { errors });
        }

        return await next(context);
    }
}
