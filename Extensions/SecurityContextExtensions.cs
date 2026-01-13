using System.Text.RegularExpressions;
using PolicyPOC.Services;

namespace PolicyPOC.Extensions;

public static class SecurityContextExtensions
{
    /// <summary>
    /// Applies row-level security rules to a queryable.
    /// Supports both simple rules (backward compatible) and complex rule groups with AND/OR logic.
    /// Example usage: query = query.ApplyRowLevelSecurity(securityContextService);
    /// </summary>
    public static IQueryable<T> ApplyRowLevelSecurity<T>(this IQueryable<T> query, ISecurityContextService securityContextService) where T : class
    {
        var securityContext = securityContextService.SecurityContext;
        if (securityContext == null)
        {
            return query;
        }

        // First, check if we have the new complex rule group structure
        if (securityContext.RowLevelSecurityRuleGroup != null)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
            var groupExpression = BuildRuleGroupExpression<T>(securityContext.RowLevelSecurityRuleGroup, parameter);
            
            if (groupExpression != null)
            {
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(groupExpression, parameter);
                query = query.Where(lambda);
            }
            
            return query;
        }

        // Backward compatibility: Handle simple rules (all combined with AND logic)
        if (!securityContext.RowLevelSecurityRules.Any())
        {
            return query;
        }

        foreach (var rule in securityContext.RowLevelSecurityRules)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
            var comparison = BuildComparisonExpression<T>(rule.ResourceField, rule.Operator, rule.Value, parameter);
            var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(comparison, parameter);
            query = query.Where(lambda);
        }

        return query;
    }

    /// <summary>
    /// Recursively builds an expression tree from a rule group
    /// </summary>
    private static System.Linq.Expressions.Expression? BuildRuleGroupExpression<T>(
        RowLevelSecurityRuleBase ruleBase,
        System.Linq.Expressions.ParameterExpression parameter) where T : class
    {
        if (ruleBase is RowLevelSecurityComparisonRule comparisonRule)
        {
            return BuildComparisonExpression<T>(
                comparisonRule.ResourceField,
                comparisonRule.Operator,
                comparisonRule.Value,
                parameter);
        }

        if (ruleBase is RowLevelSecurityRuleGroup ruleGroup)
        {
            if (ruleGroup.Rules == null || !ruleGroup.Rules.Any())
            {
                return null;
            }

            var expressions = new List<System.Linq.Expressions.Expression>();

            foreach (var rule in ruleGroup.Rules)
            {
                var expr = BuildRuleGroupExpression<T>(rule, parameter);
                if (expr != null)
                {
                    expressions.Add(expr);
                }
            }

            if (!expressions.Any())
            {
                return null;
            }

            // Combine expressions based on the condition (AND or OR)
            System.Linq.Expressions.Expression combined = expressions[0];

            for (int i = 1; i < expressions.Count; i++)
            {
                combined = ruleGroup.Condition.ToUpper() == "OR"
                    ? System.Linq.Expressions.Expression.OrElse(combined, expressions[i])
                    : System.Linq.Expressions.Expression.AndAlso(combined, expressions[i]);
            }

            return combined;
        }

        return null;
    }

    /// <summary>
    /// Builds a comparison expression for a single rule
    /// </summary>
    private static System.Linq.Expressions.Expression BuildComparisonExpression<T>(
        string resourceField,
        string operatorStr,
        string value,
        System.Linq.Expressions.ParameterExpression parameter) where T : class
    {
        var property = System.Linq.Expressions.Expression.Property(parameter, resourceField);
        
        System.Linq.Expressions.Expression comparison = operatorStr.ToLower() switch
        {
            "=" or "equals" => System.Linq.Expressions.Expression.Equal(property, System.Linq.Expressions.Expression.Constant(value)),
            "!=" or "notequals" or "not equals" => System.Linq.Expressions.Expression.NotEqual(property, System.Linq.Expressions.Expression.Constant(value)),
            ">" or "greaterthan" or "greater than" => System.Linq.Expressions.Expression.GreaterThan(property, System.Linq.Expressions.Expression.Constant(value)),
            ">=" or "greaterthanorequal" or "greater than or equal" => System.Linq.Expressions.Expression.GreaterThanOrEqual(property, System.Linq.Expressions.Expression.Constant(value)),
            "<" or "lessthan" or "less than" => System.Linq.Expressions.Expression.LessThan(property, System.Linq.Expressions.Expression.Constant(value)),
            "<=" or "lessthanorequal" or "less than or equal" => System.Linq.Expressions.Expression.LessThanOrEqual(property, System.Linq.Expressions.Expression.Constant(value)),
            "in" => BuildInExpression(property, value, isReverse: false),
            "not in" or "notin" => BuildInExpression(property, value, isReverse: true),
            _ => throw new NotSupportedException($"Operator {operatorStr} is not supported")
        };

        return comparison;
    }

    /// <summary>
    /// Builds an IN or NOT IN expression from a SQL-formatted list like ('value1','value2')
    /// </summary>
    private static System.Linq.Expressions.Expression BuildInExpression(
        System.Linq.Expressions.MemberExpression property,
        string sqlFormattedList,
        bool isReverse)
    {
        // Parse the SQL-formatted list: ('value1','value2') or ('value1', 'value2', 'value3')
        var values = ParseSqlList(sqlFormattedList);
        
        if (!values.Any())
        {
            // If no values, return false for IN, true for NOT IN (reversed)
            return System.Linq.Expressions.Expression.Constant(isReverse);
        }

        // Get the property type to properly convert values
        var propertyType = property.Type;
        
        // Convert string values to the property type
        var typedValues = values.Select(v => ConvertToType(v, propertyType)).ToList();
        
        // Create a constant expression for the list
        var listType = typeof(List<>).MakeGenericType(propertyType);
        var listConstant = System.Linq.Expressions.Expression.Constant(
            Activator.CreateInstance(listType, new object[] { typedValues }));
        
        // Call the Contains method: list.Contains(property)
        var containsMethod = listType.GetMethod("Contains", new[] { propertyType });
        var containsCall = System.Linq.Expressions.Expression.Call(listConstant, containsMethod!, property);
        
        // Reverse the expression if needed (for NOT IN)
        return isReverse 
            ? System.Linq.Expressions.Expression.Not(containsCall) 
            : containsCall;
    }

    /// <summary>
    /// Parses a SQL-formatted list like ('value1','value2') into individual values
    /// </summary>
    private static List<string> ParseSqlList(string sqlFormattedList)
    {
        if (string.IsNullOrWhiteSpace(sqlFormattedList))
        {
            return new List<string>();
        }

        // Remove outer parentheses and whitespace
        var trimmed = sqlFormattedList.Trim();
        if (trimmed.StartsWith("(") && trimmed.EndsWith(")"))
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }

        // Use regex to extract values between quotes
        // Matches both 'value' and "value"
        var matches = Regex.Matches(trimmed, @"'([^']*)'|""([^""]*)""");
        
        var values = new List<string>();
        foreach (Match match in matches)
        {
            // Get the value from either single or double quotes
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            values.Add(value);
        }

        return values;
    }

    /// <summary>
    /// Converts a string value to the target type
    /// </summary>
    private static object ConvertToType(string value, Type targetType)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
        {
            return value;
        }

        if (underlyingType == typeof(int))
        {
            return int.Parse(value);
        }

        if (underlyingType == typeof(long))
        {
            return long.Parse(value);
        }

        if (underlyingType == typeof(decimal))
        {
            return decimal.Parse(value);
        }

        if (underlyingType == typeof(double))
        {
            return double.Parse(value);
        }

        if (underlyingType == typeof(float))
        {
            return float.Parse(value);
        }

        if (underlyingType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (underlyingType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (underlyingType == typeof(DateTime))
        {
            return DateTime.Parse(value);
        }

        // Default: return the string value
        return value;
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

