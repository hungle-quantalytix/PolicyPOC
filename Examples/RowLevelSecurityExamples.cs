using PolicyPOC.Services;

namespace PolicyPOC.Examples;

/// <summary>
/// Examples demonstrating row-level security with AND/OR logic
/// </summary>
public static class RowLevelSecurityExamples
{
    /// <summary>
    /// Example 1: Simple AND logic (backward compatible)
    /// User can only see loans in their branch with Active status
    /// SQL: WHERE BranchId = '123' AND Status = 'Active'
    /// </summary>
    public static void Example1_SimpleAndLogic(ISecurityContextService securityContext)
    {
        securityContext.AddRowLevelSecurityRule(new RowLevelSecurityRule
        {
            ResourceField = "BranchId",
            Operator = "=",
            Value = "123"
        });

        securityContext.AddRowLevelSecurityRule(new RowLevelSecurityRule
        {
            ResourceField = "Status",
            Operator = "=",
            Value = "Active"
        });
    }

    /// <summary>
    /// Example 2: Simple OR logic
    /// User can see loans from multiple branches
    /// SQL: WHERE BranchId = '123' OR BranchId = '456' OR BranchId = '789'
    /// </summary>
    public static void Example2_SimpleOrLogic(ISecurityContextService securityContext)
    {
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
                },
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "BranchId",
                    Operator = "=",
                    Value = "789"
                }
            }
        };

        securityContext.SetRowLevelSecurityRuleGroup(ruleGroup);
    }

    /// <summary>
    /// Example 3: Mixed AND/OR logic - User access based on role
    /// Manager sees all loans in their department
    /// Regular user only sees their own loans
    /// SQL: WHERE (Department = 'Sales' AND Role = 'Manager') OR UserId = 'user123'
    /// </summary>
    public static void Example3_MixedAndOrLogic(ISecurityContextService securityContext)
    {
        var ruleGroup = new RowLevelSecurityRuleGroup
        {
            Condition = "OR",
            Rules = new List<RowLevelSecurityRuleBase>
            {
                // Managers can see all loans in their department
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
                // OR regular users see only their own loans
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "UserId",
                    Operator = "=",
                    Value = "user123"
                }
            }
        };

        securityContext.SetRowLevelSecurityRuleGroup(ruleGroup);
    }

    /// <summary>
    /// Example 4: Complex nested logic - Multi-level access control
    /// Admins see everything, Managers see their region, Users see their branch
    /// SQL: WHERE Role = 'Admin' 
    ///         OR (Role = 'Manager' AND Region = 'West') 
    ///         OR (Role = 'User' AND BranchId = '123')
    /// </summary>
    public static void Example4_ComplexNestedLogic(ISecurityContextService securityContext)
    {
        var ruleGroup = new RowLevelSecurityRuleGroup
        {
            Condition = "OR",
            Rules = new List<RowLevelSecurityRuleBase>
            {
                // Admins see everything
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "Role",
                    Operator = "=",
                    Value = "Admin"
                },
                // Managers see their region
                new RowLevelSecurityRuleGroup
                {
                    Condition = "AND",
                    Rules = new List<RowLevelSecurityRuleBase>
                    {
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "Role",
                            Operator = "=",
                            Value = "Manager"
                        },
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "Region",
                            Operator = "=",
                            Value = "West"
                        }
                    }
                },
                // Regular users see only their branch
                new RowLevelSecurityRuleGroup
                {
                    Condition = "AND",
                    Rules = new List<RowLevelSecurityRuleBase>
                    {
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "Role",
                            Operator = "=",
                            Value = "User"
                        },
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "BranchId",
                            Operator = "=",
                            Value = "123"
                        }
                    }
                }
            }
        };

        securityContext.SetRowLevelSecurityRuleGroup(ruleGroup);
    }

    /// <summary>
    /// Example 5: Date range with OR logic
    /// User can see loans created in Q1 or Q2
    /// SQL: WHERE (CreatedDate >= '2024-01-01' AND CreatedDate < '2024-04-01')
    ///         OR (CreatedDate >= '2024-04-01' AND CreatedDate < '2024-07-01')
    /// </summary>
    public static void Example5_DateRangeOrLogic(ISecurityContextService securityContext)
    {
        var ruleGroup = new RowLevelSecurityRuleGroup
        {
            Condition = "OR",
            Rules = new List<RowLevelSecurityRuleBase>
            {
                // Q1: Jan-Mar
                new RowLevelSecurityRuleGroup
                {
                    Condition = "AND",
                    Rules = new List<RowLevelSecurityRuleBase>
                    {
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "CreatedDate",
                            Operator = ">=",
                            Value = "2024-01-01"
                        },
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "CreatedDate",
                            Operator = "<",
                            Value = "2024-04-01"
                        }
                    }
                },
                // Q2: Apr-Jun
                new RowLevelSecurityRuleGroup
                {
                    Condition = "AND",
                    Rules = new List<RowLevelSecurityRuleBase>
                    {
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "CreatedDate",
                            Operator = ">=",
                            Value = "2024-04-01"
                        },
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "CreatedDate",
                            Operator = "<",
                            Value = "2024-07-01"
                        }
                    }
                }
            }
        };

        securityContext.SetRowLevelSecurityRuleGroup(ruleGroup);
    }

    /// <summary>
    /// Example 6: Amount-based access with multiple conditions
    /// Users see small loans in any status, or large loans only if Approved
    /// SQL: WHERE (Amount <= 10000)
    ///         OR (Amount > 10000 AND Status = 'Approved')
    /// </summary>
    public static void Example6_AmountBasedAccess(ISecurityContextService securityContext)
    {
        var ruleGroup = new RowLevelSecurityRuleGroup
        {
            Condition = "OR",
            Rules = new List<RowLevelSecurityRuleBase>
            {
                // Small loans (any status)
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "Amount",
                    Operator = "<=",
                    Value = "10000"
                },
                // Large loans (only if approved)
                new RowLevelSecurityRuleGroup
                {
                    Condition = "AND",
                    Rules = new List<RowLevelSecurityRuleBase>
                    {
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "Amount",
                            Operator = ">",
                            Value = "10000"
                        },
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "Status",
                            Operator = "=",
                            Value = "Approved"
                        }
                    }
                }
            }
        };

        securityContext.SetRowLevelSecurityRuleGroup(ruleGroup);
    }

    /// <summary>
    /// Example 7: Very complex nested logic (3 levels deep)
    /// Different access rules based on role, department, and loan characteristics
    /// Use sparingly - complex rules can impact performance and maintainability
    /// </summary>
    public static void Example7_VeryComplexLogic(ISecurityContextService securityContext)
    {
        var ruleGroup = new RowLevelSecurityRuleGroup
        {
            Condition = "OR",
            Rules = new List<RowLevelSecurityRuleBase>
            {
                // Sales managers see active loans in their region over $5000
                new RowLevelSecurityRuleGroup
                {
                    Condition = "AND",
                    Rules = new List<RowLevelSecurityRuleBase>
                    {
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "Role",
                            Operator = "=",
                            Value = "Manager"
                        },
                        new RowLevelSecurityComparisonRule
                        {
                            ResourceField = "Department",
                            Operator = "=",
                            Value = "Sales"
                        },
                        new RowLevelSecurityRuleGroup
                        {
                            Condition = "AND",
                            Rules = new List<RowLevelSecurityRuleBase>
                            {
                                new RowLevelSecurityComparisonRule
                                {
                                    ResourceField = "Region",
                                    Operator = "=",
                                    Value = "West"
                                },
                                new RowLevelSecurityComparisonRule
                                {
                                    ResourceField = "Status",
                                    Operator = "=",
                                    Value = "Active"
                                },
                                new RowLevelSecurityComparisonRule
                                {
                                    ResourceField = "Amount",
                                    Operator = ">",
                                    Value = "5000"
                                }
                            }
                        }
                    }
                },
                // OR regular users see their own loans regardless of amount or status
                new RowLevelSecurityComparisonRule
                {
                    ResourceField = "UserId",
                    Operator = "=",
                    Value = "user123"
                }
            }
        };

        securityContext.SetRowLevelSecurityRuleGroup(ruleGroup);
    }
}
