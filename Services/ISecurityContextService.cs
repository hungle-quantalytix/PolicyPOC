namespace PolicyPOC.Services;

public interface ISecurityContextService
{
    SecurityContext SecurityContext { get; }
    void AddRowLevelSecurityRule(RowLevelSecurityRule rule);
    void AddPiiPolicyRule(PiiPolicyRule rule);
    void Clear();
}

public class SecurityContext
{
    public List<RowLevelSecurityRule> RowLevelSecurityRules { get; set; } = new();
    public List<PiiPolicyRule> PiiPolicyRules { get; set; } = new();
}

public class RowLevelSecurityRule
{
    public required string ResourceField { get; set; }
    public required string Operator { get; set; }
    public required string Value { get; set; }
}

public class PiiPolicyRule
{
    public required string ResourceName { get; set; }
    public required string[] Columns { get; set; }
    public required string Action { get; set; }
}

