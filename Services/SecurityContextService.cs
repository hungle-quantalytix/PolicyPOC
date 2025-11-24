namespace PolicyPOC.Services;

public class SecurityContextService : ISecurityContextService
{
    private readonly SecurityContext _securityContext = new();

    public SecurityContext SecurityContext => _securityContext;

    public void AddRowLevelSecurityRule(RowLevelSecurityRule rule)
    {
        _securityContext.RowLevelSecurityRules.Add(rule);
    }

    public void AddPiiPolicyRule(PiiPolicyRule rule)
    {
        _securityContext.PiiPolicyRules.Add(rule);
    }

    public void Clear()
    {
        _securityContext.RowLevelSecurityRules.Clear();
        _securityContext.PiiPolicyRules.Clear();
    }
}

