# Changelog: Row-Level Security AND/OR Logic Support

## Summary

Added support for complex AND/OR logic in row-level security rules. Previously, all rules were implicitly combined with AND logic. Now you can create sophisticated rule groups with nested AND/OR conditions.

## What Changed

### 1. New Classes in `ISecurityContextService.cs`

#### Added Base Class
- `RowLevelSecurityRuleBase`: Abstract base class for all RLS rules

#### Added Rule Types
- `RowLevelSecurityRuleGroup`: Groups rules with AND or OR logic
  - Property: `Condition` (string: "AND" or "OR")
  - Property: `Rules` (List of RowLevelSecurityRuleBase)
  
- `RowLevelSecurityComparisonRule`: Single comparison rule
  - Property: `ResourceField` (string)
  - Property: `Operator` (string)
  - Property: `Value` (string)

#### Updated SecurityContext
- Added: `RowLevelSecurityRuleGroup? RowLevelSecurityRuleGroup` property
- Existing: `List<RowLevelSecurityRule> RowLevelSecurityRules` (kept for backward compatibility)

### 2. Updated `ISecurityContextService` Interface

#### New Method
```csharp
void SetRowLevelSecurityRuleGroup(RowLevelSecurityRuleGroup ruleGroup);
```

### 3. Updated `SecurityContextService` Implementation

#### Changes
- Added `SetRowLevelSecurityRuleGroup()` method
- Updated `Clear()` to reset `RowLevelSecurityRuleGroup`

### 4. Enhanced `SecurityContextExtensions`

#### Updated `ApplyRowLevelSecurity<T>()` Method
- Now checks for `RowLevelSecurityRuleGroup` first
- Falls back to simple rules for backward compatibility
- Priority: Rule groups > Simple rules

#### New Private Methods
- `BuildRuleGroupExpression<T>()`: Recursively builds expression trees from rule groups
- `BuildComparisonExpression<T>()`: Builds comparison expressions (extracted for reuse)

### 5. Updated `PermissionService`

#### Modified Policy Evaluation
- `EvaluatePolicyRulesAsync()`: Now collects RLS rules and converts to rule groups
- `EvaluatePolicyGroupAsync()`: Updated to collect RLS rules from nested policies
- `EvaluateComparisonRuleAsync()`: Collects RLS rules for both old and new structures

These changes enable automatic conversion of `PolicyRule` hierarchies to `RowLevelSecurityRuleGroup` structures.

## Backward Compatibility

✅ **Fully backward compatible**

- Existing code using `AddRowLevelSecurityRule()` continues to work unchanged
- Simple rules still use implicit AND logic
- Only adopt the new structure when you need OR logic or complex conditions

## Files Modified

1. `/Services/ISecurityContextService.cs` - Added new types and interface method
2. `/Services/SecurityContextService.cs` - Implemented new method
3. `/Extensions/SecurityContextExtensions.cs` - Enhanced to support rule groups
4. `/Services/PermissionService.cs` - Updated to convert policies to rule groups

## Files Added

1. `/DOCS_ROW_LEVEL_SECURITY_AND_OR.md` - Comprehensive documentation
2. `/Examples/RowLevelSecurityExamples.cs` - 7 practical usage examples
3. `/CHANGELOG_AND_OR_LOGIC.md` - This file

## Migration Path

### No Migration Required
If you're happy with AND logic (all rules must pass), no changes needed.

### To Use OR Logic
Replace:
```csharp
// Old: AND logic only
securityContext.AddRowLevelSecurityRule(rule1);
securityContext.AddRowLevelSecurityRule(rule2);
```

With:
```csharp
// New: OR logic
securityContext.SetRowLevelSecurityRuleGroup(new RowLevelSecurityRuleGroup
{
    Condition = "OR",
    Rules = new List<RowLevelSecurityRuleBase> { rule1, rule2 }
});
```

## Testing

### Build Status
✅ Project builds successfully with no warnings or errors

### What to Test
1. **Backward compatibility**: Verify existing code using simple rules still works
2. **Simple OR**: Test basic OR conditions (Example 2)
3. **Mixed AND/OR**: Test nested groups (Examples 3-4)
4. **Policy integration**: Verify policies with AND/OR are correctly applied
5. **Performance**: Test query performance with complex nested rules
6. **SQL generation**: Inspect generated SQL queries to ensure correctness

### Test Queries
After deploying, verify the generated SQL by:
1. Enabling EF Core logging
2. Checking SQL queries include proper WHERE clauses
3. Confirming nested conditions use correct parentheses

## Performance Considerations

- Expression trees are built once per request
- Recursive evaluation is fast for reasonable nesting depths (< 5 levels)
- Generated SQL is optimized by Entity Framework
- Complex rules may benefit from database indexes on filtered fields

## Best Practices

1. **Use simple rules** when all conditions are ANDed
2. **Use rule groups** only when you need OR logic
3. **Keep nesting shallow** (max 2-3 levels)
4. **Test SQL output** to verify query correctness
5. **Add database indexes** on fields used in RLS rules
6. **Document complex rules** for maintainability

## Examples

See `/Examples/RowLevelSecurityExamples.cs` for 7 practical examples:

1. **Simple AND logic** (backward compatible)
2. **Simple OR logic** (multiple branches)
3. **Mixed AND/OR** (manager vs regular user)
4. **Complex nested** (admin/manager/user hierarchy)
5. **Date range OR** (Q1 or Q2)
6. **Amount-based** (small loans vs approved large loans)
7. **Very complex** (3-level nesting - use sparingly!)

## Questions?

For detailed documentation, see:
- `/DOCS_ROW_LEVEL_SECURITY_AND_OR.md` - Full documentation with examples
- `/Examples/RowLevelSecurityExamples.cs` - Code examples you can run

## Future Enhancements

Potential improvements for future versions:
- Support for `IN` operator with arrays
- Support for `LIKE` operator for pattern matching
- Support for `NOT` operator for negation
- Visual query builder for complex rules
- Performance profiling tools
- Rule validation and testing utilities
