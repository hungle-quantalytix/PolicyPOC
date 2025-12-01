using System.Text.Json.Serialization;
using PolicyPOC.Converters;

namespace PolicyPOC.Models;

public class Policy
{
    public required Guid Id { get; set; }
    public string? Description { get; set; }
    public required string PolicyData { get; set; } // JSON string JSONB in postgres, serialized from PolicyRule

    // Navigation properties for many-to-many relationships with Resource
    // Note: These are the inverse navigation properties
    public ICollection<Resource> ReadResources { get; set; } = new List<Resource>();
    public ICollection<Resource> WriteResources { get; set; } = new List<Resource>();
    
    // Navigation properties for many-to-many relationships with Field
    public ICollection<Field> ReadFields { get; set; } = new List<Field>();
    public ICollection<Field> WriteFields { get; set; } = new List<Field>();
}

// PolicyResource class removed - now using direct many-to-many relationships

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