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
    /// Gets list of PII columns that should be masked or filtered
    /// </summary>
    public static string[] GetPiiColumns(this ISecurityContextService securityContextService, string resourceName)
    {
        var securityContext = securityContextService.SecurityContext;
        if (securityContext == null)
        {
            return Array.Empty<string>();
        }

        var piiPolicies = securityContext.PiiPolicyRules
            .Where(p => p.ResourceName.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Columns)
            .Distinct()
            .ToArray();

        return piiPolicies;
    }

    /// <summary>
    /// Masks PII fields in an object based on the security context
    /// </summary>
    public static T MaskPiiFields<T>(this T obj, ISecurityContextService securityContextService, string resourceName) where T : class
    {
        var piiColumns = securityContextService.GetPiiColumns(resourceName);
        if (!piiColumns.Any())
        {
            return obj;
        }

        foreach (var column in piiColumns)
        {
            var property = typeof(T).GetProperty(column, 
                System.Reflection.BindingFlags.IgnoreCase | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);

            if (property != null && property.CanWrite)
            {
                if (property.PropertyType == typeof(string))
                {
                    property.SetValue(obj, "***MASKED***");
                }
                else
                {
                    // For non-string types, set to default value
                    property.SetValue(obj, null);
                }
            }
        }

        return obj;
    }
}

