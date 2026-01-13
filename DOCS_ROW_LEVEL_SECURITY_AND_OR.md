# Row-Level Security: AND/OR Logic Support

## Overview

The row-level security system now supports complex AND/OR logic for filtering data. This allows you to create sophisticated access control rules that can express conditions like:
- "User can see records where BranchId = 123 OR BranchId = 456"
- "User can see records where (Status = 'Active' AND Department = 'Sales') OR Role = 'Admin'"

## Two Approaches

### 1. Simple Rules (Backward Compatible - AND Only)

The original approach using `RowLevelSecurityRule` still works and implicitly combines rules with AND logic:

```csharp
securityContextService.AddRowLevelSecurityRule(new RowLevelSecurityRule
{
    ResourceField = "Status",
    Operator = "=",
    Value = "Active"
});

securityContextService.AddRowLevelSecurityRule(new RowLevelSecurityRule
{
    ResourceField = "BranchId",
    Operator = "=",
    Value = "123"
});

// This produces: WHERE Status = 'Active' AND BranchId = 123
```

### 2. Complex Rule Groups (New - AND/OR Support)

The new approach using `RowLevelSecurityRuleGroup` supports nested AND/OR logic:

```csharp
var ruleGroup = new RowLevelSecurityRuleGroup
{
    Condition = "OR",
    Rules = new List<RowLevelSecurityRuleBase>
    {
        new RowLevelSecurityComparisonRule
        {
            ResourceField = "BranchId",
            Operator = "=",
            Value = "123"
        },
        new RowLevelSecurityComparisonRule
        {
            ResourceField = "BranchId",
            Operator = "=",
            Value = "456"
        }
    }
};

securityContextService.SetRowLevelSecurityRuleGroup(ruleGroup);

// This produces: WHERE BranchId = 123 OR BranchId = 456
```

## Nested Groups

You can nest rule groups to create complex logic:

```csharp
var ruleGroup = new RowLevelSecurityRuleGroup
{
    Condition = "OR",
    Rules = new List<RowLevelSecurityRuleBase>
    {
        // First group: Status = Active AND Department = Sales
        new RowLevelSecurityRuleGroup
        {
            Condition = "AND",
            Rules = new List<RowLevelSecurityRuleBase>
            {
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "Status",
                    Operator = "=",
                    Value = "Active"
                },
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "Department",
                    Operator = "=",
                    Value = "Sales"
                }
            }
        },
        // OR: Role = Admin
        new RowLevelSecurityComparisonRule
        {
            ResourceField = "Role",
            Operator = "=",
            Value = "Admin"
        }
    }
};

securityContextService.SetRowLevelSecurityRuleGroup(ruleGroup);

// This produces: WHERE (Status = 'Active' AND Department = 'Sales') OR Role = 'Admin'
```

## Policy Integration

When policies are evaluated, the system automatically converts `PolicyRule` structures (which already support AND/OR) into the new `RowLevelSecurityRuleGroup` structure.

For example, this policy:

```json
{
  "Condition": "OR",
  "Rules": [
    {
      "Field": "resource.BranchId",
      "Operator": "=",
      "Value": "${user.BranchId}"
    },
    {
      "Condition": "AND",
      "Rules": [
        {
          "Field": "user.Role",
          "Operator": "=",
          "Value": "Manager"
        },
        {
          "Field": "resource.Department",
          "Operator": "=",
          "Value": "${user.Department}"
        }
      ]
    }
  ]
}
```

Will be automatically converted to a rule group that filters data like:
```sql
WHERE BranchId = [user's branch] 
   OR (user is Manager AND Department = [user's department])
```

## Usage in Queries

The extension method `ApplyRowLevelSecurity` automatically handles both simple and complex rules:

```csharp
public async Task<List<Loan>> GetLoansAsync()
{
    var query = _dbContext.Loans.AsQueryable();
    
    // Applies row-level security (works with both simple rules and rule groups)
    query = query.ApplyRowLevelSecurity(_securityContextService);
    
    return await query.ToListAsync();
}
```

## Supported Operators

- `=` or `equals`: Equality
- `!=` or `notequals` or `not equals`: Inequality
- `>` or `greaterthan` or `greater than`: Greater than
- `>=` or `greaterthanorequal` or `greater than or equal`: Greater than or equal
- `<` or `lessthan` or `less than`: Less than
- `<=` or `lessthanorequal` or `less than or equal`: Less than or equal

## Migration Guide

### From Simple Rules to Rule Groups

If you have existing code using simple rules:

```csharp
// Old approach
securityContextService.AddRowLevelSecurityRule(new RowLevelSecurityRule
{
    ResourceField = "BranchId",
    Operator = "=",
    Value = "123"
});
```

And you want OR logic, migrate to:

```csharp
// New approach
securityContextService.SetRowLevelSecurityRuleGroup(new RowLevelSecurityRuleGroup
{
    Condition = "OR",
    Rules = new List<RowLevelSecurityRuleBase>
    {
        new RowLevelSecurityComparisonRule
        {
            ResourceField = "BranchId",
            Operator = "=",
            Value = "123"
        },
        new RowLevelSecurityComparisonRule
        {
            ResourceField = "BranchId",
            Operator = "=",
            Value = "456"
        }
    }
});
```

## Priority

If both simple rules AND a rule group are present:
1. The rule group takes precedence
2. Simple rules are ignored
3. It's recommended to use only one approach per request

## Performance Considerations

- Rule groups are evaluated recursively and converted to a single LINQ expression
- The resulting SQL query is optimized by Entity Framework
- Complex nested groups may produce complex SQL - test query performance with your database
- Use indexes on fields referenced in row-level security rules

## Best Practices

1. **Start with the simplest structure** that meets your needs
2. **Use simple rules** when all conditions should be ANDed together
3. **Use rule groups** when you need OR logic or complex combinations
4. **Test generated SQL** queries to ensure performance
5. **Document complex rule structures** for maintainability
6. **Keep nesting depth reasonable** (max 2-3 levels) for readability
