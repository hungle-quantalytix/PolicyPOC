namespace PolicyPOC.Services;

public class SecurityContextService : ISecurityContextService
{
    private readonly SecurityContext _securityContext = new();

    public SecurityContext SecurityContext => _securityContext;

    public void AddRowLevelSecurityRule(RowLevelSecurityRule rule)
    {
        _securityContext.RowLevelSecurityRules.Add(rule);
    }

    public void SetRowLevelSecurityRuleGroup(RowLevelSecurityRuleGroup ruleGroup)
    {
        _securityContext.RowLevelSecurityRuleGroup = ruleGroup;
    }

    public void AddFieldAccess(FieldAccessRule rule)
    {
        // Remove existing rule for same field if exists
        _securityContext.FieldAccessRules.RemoveAll(r => 
            r.FieldName.Equals(rule.FieldName, StringComparison.OrdinalIgnoreCase));
        _securityContext.FieldAccessRules.Add(rule);
        _securityContext.HasFieldLevelSecurity = true;
    }

    public bool HasFieldAccess(string fieldName)
    {
        if (!_securityContext.HasFieldLevelSecurity)
            return true; // No field-level security evaluated, allow all
            
        var rule = GetFieldAccessRule(fieldName);
        return rule?.AccessLevel == FieldAccessLevel.Full;
    }

    public FieldAccessRule? GetFieldAccessRule(string fieldName)
    {
        return _securityContext.FieldAccessRules
            .FirstOrDefault(r => r.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    }

    public void Clear()
    {
        _securityContext.RowLevelSecurityRules.Clear();
        _securityContext.RowLevelSecurityRuleGroup = null;
        _securityContext.FieldAccessRules.Clear();
        _securityContext.HasFieldLevelSecurity = false;
    }
}

