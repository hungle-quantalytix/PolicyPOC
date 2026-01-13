# Row-Level Security: Before vs After Comparison

## The Problem You Identified

**You asked:** "This code only supports AND logic?"

**Answer:** Yes, the original code implicitly combined all rules with AND logic.

---

## Visual Comparison

### BEFORE (AND Logic Only)

```
Rule 1: BranchId = 123
Rule 2: Status = Active
Rule 3: Amount > 1000

Result: Rule 1 AND Rule 2 AND Rule 3
SQL: WHERE BranchId = 123 AND Status = 'Active' AND Amount > 1000
```

**Limitation:** You couldn't express "Rule 1 OR Rule 2"

### AFTER (AND/OR Logic Supported)

```
RuleGroup (OR)
├── Rule 1: BranchId = 123
├── Rule 2: BranchId = 456
└── Rule 3: BranchId = 789

Result: Rule 1 OR Rule 2 OR Rule 3
SQL: WHERE BranchId = 123 OR BranchId = 456 OR BranchId = 789
```

**Capability:** Now supports OR, nested AND/OR, and complex combinations

---

## Code Comparison

### Scenario: User can access loans from Branch 123 OR Branch 456

#### BEFORE: Not Possible ❌

```csharp
// This would produce: WHERE BranchId = 123 AND BranchId = 456
// Which is logically impossible (a field can't equal two values)
securityContext.AddRowLevelSecurityRule(new RowLevelSecurityRule
{
    ResourceField = "BranchId",
    Operator = "=",
    Value = "123"
});

securityContext.AddRowLevelSecurityRule(new RowLevelSecurityRule
{
    ResourceField = "BranchId",
    Operator = "=",
    Value = "456"
});
```

#### AFTER: Now Possible ✅

```csharp
// This produces: WHERE BranchId = 123 OR BranchId = 456
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

securityContext.SetRowLevelSecurityRuleGroup(ruleGroup);
```

---

## Complex Scenario Comparison

### Scenario: Managers see all loans in their department, regular users see only their own

#### BEFORE: Not Possible ❌

You would need separate queries or application-level filtering.

#### AFTER: Now Possible ✅

```csharp
var ruleGroup = new RowLevelSecurityRuleGroup
{
    Condition = "OR",
    Rules = new List<RowLevelSecurityRuleBase>
    {
        // Managers see all in department
        new RowLevelSecurityRuleGroup
        {
            Condition = "AND",
            Rules = new List<RowLevelSecurityRuleBase>
            {
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "Department",
                    Operator = "=",
                    Value = "Sales"
                },
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "Role",
                    Operator = "=",
                    Value = "Manager"
                }
            }
        },
        // OR users see only their own
        new RowLevelSecurityComparisonRule
        {
            ResourceField = "UserId",
            Operator = "=",
            Value = "user123"
        }
    }
};

// SQL: WHERE (Department = 'Sales' AND Role = 'Manager') OR UserId = 'user123'
```

---

## Architecture Comparison

### BEFORE: Linear Structure

```
SecurityContext
  └── RowLevelSecurityRules: List<RowLevelSecurityRule>
       ├── Rule 1
       ├── Rule 2
       └── Rule 3
       
All rules combined with AND (hardcoded in loop)
```

### AFTER: Tree Structure

```
SecurityContext
  ├── RowLevelSecurityRules: List<RowLevelSecurityRule> (backward compatible)
  └── RowLevelSecurityRuleGroup: Tree structure
       ├── Condition: "AND" or "OR"
       └── Rules: List<RowLevelSecurityRuleBase>
            ├── RowLevelSecurityComparisonRule
            ├── RowLevelSecurityRuleGroup (nested)
            │    ├── Condition: "AND" or "OR"
            │    └── Rules: ...
            └── RowLevelSecurityComparisonRule

Flexible tree with custom logic at each node
```

---

## Expression Building Comparison

### BEFORE: Simple Loop with AND

```csharp
foreach (var rule in rules)
{
    var expression = BuildExpression(rule);
    query = query.Where(expression);  // Each Where adds AND
}
```

### AFTER: Recursive Tree Traversal

```csharp
private Expression BuildRuleGroupExpression(RuleBase rule)
{
    if (rule is ComparisonRule comparison)
        return BuildComparison(comparison);
    
    if (rule is RuleGroup group)
    {
        var expressions = group.Rules.Select(r => BuildRuleGroupExpression(r));
        
        return group.Condition == "OR"
            ? CombineWithOr(expressions)
            : CombineWithAnd(expressions);
    }
}
```

---

## Use Case Examples

### Use Case 1: Multi-Branch Access

**Requirement:** User has access to 3 different branches

| Before | After |
|--------|-------|
| ❌ Not possible with AND | ✅ Simple OR group |

### Use Case 2: Role-Based Data Filtering

**Requirement:** Admins see all, managers see region, users see branch

| Before | After |
|--------|-------|
| ❌ Need separate endpoints | ✅ Single nested OR group |

### Use Case 3: Time-Based Access

**Requirement:** User can see data from Q1 OR Q2 (two date ranges)

| Before | After |
|--------|-------|
| ❌ Need application logic | ✅ OR group with AND ranges |

### Use Case 4: Conditional Field Access

**Requirement:** See all small loans OR only approved large loans

| Before | After |
|--------|-------|
| ❌ Need multiple queries | ✅ OR group with nested AND |

---

## SQL Output Comparison

### Simple Example: Branch Access

**Requirement:** BranchId = 123 OR BranchId = 456

#### Before
```sql
-- Not possible, would generate:
WHERE BranchId = '123' AND BranchId = '456'
-- (Always returns no results!)
```

#### After
```sql
-- Correct SQL:
WHERE BranchId = '123' OR BranchId = '456'
```

### Complex Example: Role-Based Access

**Requirement:** (Dept = Sales AND Role = Manager) OR UserId = user123

#### Before
```sql
-- Not possible with single query
-- Would need: 
--   SELECT ... WHERE Dept = 'Sales' AND Role = 'Manager'
--   UNION
--   SELECT ... WHERE UserId = 'user123'
```

#### After
```sql
-- Single efficient query:
WHERE (Department = 'Sales' AND Role = 'Manager') 
   OR UserId = 'user123'
```

---

## Decision Matrix: When to Use What?

| Scenario | Use Simple Rules | Use Rule Groups |
|----------|------------------|-----------------|
| All conditions must be true (AND) | ✅ Yes | ⚠️ Optional |
| At least one condition must be true (OR) | ❌ No | ✅ Yes |
| Mix of AND and OR | ❌ No | ✅ Yes |
| Complex nested logic | ❌ No | ✅ Yes |
| Simple, maintainable code | ✅ Yes | ⚠️ If simple |
| Backward compatibility needed | ✅ Yes | ❌ No |

---

## Performance Comparison

### Expression Building

| Aspect | Before | After |
|--------|--------|-------|
| Complexity | O(n) - Linear | O(n) - Linear (with recursion) |
| Memory | Low | Slightly higher (tree structure) |
| Query Performance | Same | Same (SQL is optimized) |

### SQL Execution

Both approaches generate equivalent SQL that runs at the same speed. The database optimizer handles the complexity.

---

## Summary

### What You Get

✅ **OR logic support** - Combine rules with OR  
✅ **Nested groups** - Complex (A AND B) OR (C AND D) logic  
✅ **Backward compatible** - Existing code still works  
✅ **Automatic policy conversion** - PolicyRule → RuleGroup  
✅ **Same performance** - No degradation in query speed  
✅ **Clean API** - Easy to use and understand  

### What Stays the Same

✅ Simple rules with `AddRowLevelSecurityRule()` still work  
✅ Same operators supported (=, !=, >, <, etc.)  
✅ Same `ApplyRowLevelSecurity()` extension method  
✅ Same SQL query performance  
✅ Same security guarantees  

### Key Difference

**Before:** You were locked into AND logic  
**After:** You have the flexibility to choose AND, OR, or any combination

---

## Quick Start

### Step 1: Identify Your Need

**Do you need OR logic?**
- No → Keep using simple rules
- Yes → Use rule groups

### Step 2: Choose Your Pattern

**Simple OR:** Use flat rule group with OR condition  
**Complex Logic:** Use nested rule groups with mixed conditions

### Step 3: Implement

See `/Examples/RowLevelSecurityExamples.cs` for copy-paste examples!

---

## Your Question Answered

> "This code only supports AND logic? Is it correct?"

**Answer:** 
- ✅ **Before:** Correct - only AND logic was supported
- ✅ **After:** Now supports both AND and OR logic with nesting
- ✅ **Backward Compatible:** Your existing AND-only code still works
- ✅ **New Capability:** Now you can express OR and complex combinations

**Bottom Line:** The limitation you identified has been fixed while maintaining full backward compatibility!
