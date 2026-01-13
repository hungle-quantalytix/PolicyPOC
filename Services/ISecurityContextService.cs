namespace PolicyPOC.Services;

public interface ISecurityContextService
{
    SecurityContext SecurityContext { get; }
    void AddRowLevelSecurityRule(RowLevelSecurityRule rule);
    void SetRowLevelSecurityRuleGroup(RowLevelSecurityRuleGroup ruleGroup);
    void AddFieldAccess(FieldAccessRule rule);
    void Clear();
    
    // Helper methods for field access
    bool HasFieldAccess(string fieldName);
    FieldAccessRule? GetFieldAccessRule(string fieldName);
}

public class SecurityContext
{
    public List<RowLevelSecurityRule> RowLevelSecurityRules { get; set; } = new();
    public List<FieldAccessRule> FieldAccessRules { get; set; } = new();
    
    // Advanced row-level security with AND/OR logic
    public RowLevelSecurityRuleGroup? RowLevelSecurityRuleGroup { get; set; }
    
    // Flag to indicate if field-level security has been evaluated
    public bool HasFieldLevelSecurity { get; set; } = false;
}

/// <summary>
/// Simple row-level security rule (backward compatible)
/// </summary>
public class RowLevelSecurityRule
{
    public required string ResourceField { get; set; }
    public required string Operator { get; set; }
    public required string Value { get; set; }
}

/// <summary>
/// Base class for row-level security rules that support complex AND/OR logic
/// </summary>
public abstract class RowLevelSecurityRuleBase { }

/// <summary>
/// A group of row-level security rules combined with AND or OR logic
/// </summary>
public class RowLevelSecurityRuleGroup : RowLevelSecurityRuleBase
{
    /// <summary>
    /// The logical operator: "AND" or "OR"
    /// </summary>
    public required string Condition { get; set; }
    
    /// <summary>
    /// The rules in this group (can be ComparisonRules or nested RuleGroups)
    /// </summary>
    public required List<RowLevelSecurityRuleBase> Rules { get; set; }
}

/// <summary>
/// A single comparison rule for row-level security
/// </summary>
public class RowLevelSecurityComparisonRule : RowLevelSecurityRuleBase
{
    public required string ResourceField { get; set; }
    public required string Operator { get; set; }
    public required string Value { get; set; }
}

/// <summary>
/// Represents field-level access control result
/// </summary>
public class FieldAccessRule
{
    public required string FieldName { get; set; }
    public required FieldAccessLevel AccessLevel { get; set; }
    
    // The mask format to apply when AccessLevel is Masked
    public string? MaskFormat { get; set; }
}

public enum FieldAccessLevel
{
    Full,    // User has full access to this field
    Masked,  // User can see masked value
    Empty,   // User sees null/empty
    Hidden   // Field is excluded from response entirely
}

