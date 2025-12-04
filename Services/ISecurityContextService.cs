namespace PolicyPOC.Services;

public interface ISecurityContextService
{
    SecurityContext SecurityContext { get; }
    void AddRowLevelSecurityRule(RowLevelSecurityRule rule);
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
    
    // Flag to indicate if field-level security has been evaluated
    public bool HasFieldLevelSecurity { get; set; } = false;
}

public class RowLevelSecurityRule
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

