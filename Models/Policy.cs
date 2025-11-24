using System.Text.Json.Serialization;
using PolicyPOC.Converters;

namespace PolicyPOC.Models;

public class Policy
{
    public required Guid Id { get; set; }
    public string? Description { get; set; }
    public required string PolicyData { get; set; } // JSON string JSONB in postgres, serialized from PolicyRule

    public required ICollection<PolicyResource> PolicyResources { get; set; }
}

public class PolicyResource
{
    public required Guid Id { get; set; }
    public required Guid PolicyId { get; set; }
    public string? ResourceName { get; set; }
    public string? ResourceColumns { get; set; }
    public string? Action { get; set; }
    public string? Effect { get; set; }

    public required Policy Policy { get; set; }
}

[JsonConverter(typeof(PolicyRuleConverter))]
public abstract class PolicyRule {}

public class PolicyGroup : PolicyRule
{
    public required string Condition { get; set; }
    public required ICollection<PolicyRule> Rules { get; set; }
}

public class ComparisonRule : PolicyRule
{
    public required string Field { get; set; }
    public required string Operator { get; set; }
    public required string Value { get; set; }
}