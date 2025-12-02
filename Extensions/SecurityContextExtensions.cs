using System.Text.RegularExpressions;
using PolicyPOC.Services;

namespace PolicyPOC.Extensions;

public static class SecurityContextExtensions
{
    /// <summary>
    /// Applies row-level security rules to a queryable
    /// Example usage: query = query.ApplyRowLevelSecurity(securityContextService);
    /// </summary>
    public static IQueryable<T> ApplyRowLevelSecurity<T>(this IQueryable<T> query, ISecurityContextService securityContextService) where T : class
    {
        var securityContext = securityContextService.SecurityContext;
        if (securityContext == null || !securityContext.RowLevelSecurityRules.Any())
        {
            return query;
        }

        foreach (var rule in securityContext.RowLevelSecurityRules)
        {
            // Build dynamic query based on the rule using Expression trees
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
            var property = System.Linq.Expressions.Expression.Property(parameter, rule.ResourceField);
            var constant = System.Linq.Expressions.Expression.Constant(rule.Value);

            System.Linq.Expressions.Expression comparison = rule.Operator.ToLower() switch
            {
                "=" or "equals" => System.Linq.Expressions.Expression.Equal(property, constant),
                "!=" or "notequals" or "not equals" => System.Linq.Expressions.Expression.NotEqual(property, constant),
                ">" or "greaterthan" or "greater than" => System.Linq.Expressions.Expression.GreaterThan(property, constant),
                ">=" or "greaterthanorequal" or "greater than or equal" => System.Linq.Expressions.Expression.GreaterThanOrEqual(property, constant),
                "<" or "lessthan" or "less than" => System.Linq.Expressions.Expression.LessThan(property, constant),
                "<=" or "lessthanorequal" or "less than or equal" => System.Linq.Expressions.Expression.LessThanOrEqual(property, constant),
                _ => throw new NotSupportedException($"Operator {rule.Operator} is not supported")
            };

            var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(comparison, parameter);
            query = query.Where(lambda);
        }

        return query;
    }

    /// <summary>
    /// Applies field-level security to an object based on the security context.
    /// Handles Full, Masked, Empty, and Hidden access levels.
    /// </summary>
    public static T ApplyFieldSecurity<T>(this T obj, ISecurityContextService securityContextService) where T : class
    {
        var securityContext = securityContextService.SecurityContext;
        if (securityContext == null || !securityContext.HasFieldLevelSecurity)
        {
            return obj;
        }

        foreach (var fieldRule in securityContext.FieldAccessRules)
        {
            // Skip fields with full access
            if (fieldRule.AccessLevel == FieldAccessLevel.Full)
            {
                continue;
            }

            var property = typeof(T).GetProperty(fieldRule.FieldName, 
                System.Reflection.BindingFlags.IgnoreCase | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);

            if (property == null || !property.CanWrite)
            {
                continue;
            }

            switch (fieldRule.AccessLevel)
            {
                case FieldAccessLevel.Empty:
                    // Set to null/default
                    property.SetValue(obj, null);
                    break;

                case FieldAccessLevel.Masked:
                    // Apply mask format
                    if (property.PropertyType == typeof(string))
                    {
                        var originalValue = property.GetValue(obj) as string;
                        var maskedValue = ApplyMaskFormat(originalValue, fieldRule.MaskFormat);
                        property.SetValue(obj, maskedValue);
                    }
                    else
                    {
                        // For non-string types, set to null
                        property.SetValue(obj, null);
                    }
                    break;

                case FieldAccessLevel.Hidden:
                    // For Hidden, we set to null here. 
                    // Ideally, the field should be excluded from serialization entirely
                    // which would require a custom JSON serializer or DTO transformation
                    property.SetValue(obj, null);
                    break;
            }
        }

        return obj;
    }

    /// <summary>
    /// Gets list of field names that should be hidden (excluded from response)
    /// </summary>
    public static string[] GetHiddenFields(this ISecurityContextService securityContextService)
    {
        var securityContext = securityContextService.SecurityContext;
        if (securityContext == null || !securityContext.HasFieldLevelSecurity)
        {
            return Array.Empty<string>();
        }

        return securityContext.FieldAccessRules
            .Where(r => r.AccessLevel == FieldAccessLevel.Hidden)
            .Select(r => r.FieldName)
            .ToArray();
    }

    /// <summary>
    /// Checks if field-level security is enabled and there are restricted fields
    /// </summary>
    public static bool HasFieldRestrictions(this ISecurityContextService securityContextService)
    {
        var securityContext = securityContextService.SecurityContext;
        return securityContext?.HasFieldLevelSecurity == true &&
               securityContext.FieldAccessRules.Any(r => r.AccessLevel != FieldAccessLevel.Full);
    }

    /// <summary>
    /// Applies a mask format to a value.
    /// Supported placeholders:
    /// - {value}: The full original value
    /// - {last4}: Last 4 characters
    /// - {last3}: Last 3 characters
    /// - {first4}: First 4 characters
    /// - {first3}: First 3 characters
    /// - {first1}: First character
    /// </summary>
    private static string ApplyMaskFormat(string? originalValue, string? maskFormat)
    {
        if (string.IsNullOrEmpty(originalValue))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(maskFormat))
        {
            return string.Empty;
        }

        var result = maskFormat;

        // Replace placeholders
        result = Regex.Replace(result, @"\{value\}", originalValue, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{last(\d+)\}", m =>
        {
            var count = int.Parse(m.Groups[1].Value);
            return originalValue.Length >= count 
                ? originalValue.Substring(originalValue.Length - count) 
                : originalValue;
        }, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{first(\d+)\}", m =>
        {
            var count = int.Parse(m.Groups[1].Value);
            return originalValue.Length >= count 
                ? originalValue.Substring(0, count) 
                : originalValue;
        }, RegexOptions.IgnoreCase);

        return result;
    }
}

