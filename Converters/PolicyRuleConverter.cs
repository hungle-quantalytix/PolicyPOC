using System.Text.Json;
using System.Text.Json.Serialization;
using PolicyPOC.Models;

namespace PolicyPOC.Converters;

public class PolicyRuleConverter : JsonConverter<PolicyRule>
{
    public override PolicyRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Clone the reader to peek at the structure
        var readerClone = reader;
        
        using (JsonDocument document = JsonDocument.ParseValue(ref readerClone))
        {
            var root = document.RootElement;
            
            // Check if it has the properties of a PolicyGroup (Condition and Rules)
            if (root.TryGetProperty("Condition", out _) || 
                root.TryGetProperty("condition", out _))
            {
                // It's a PolicyGroup
                return JsonSerializer.Deserialize<PolicyGroup>(ref reader, options);
            }
            
            // Check if it has the properties of a ComparisonRule (Field, Operator, Value)
            if (root.TryGetProperty("Field", out _) || 
                root.TryGetProperty("field", out _) ||
                root.TryGetProperty("Operator", out _) || 
                root.TryGetProperty("operator", out _))
            {
                // It's a ComparisonRule
                return JsonSerializer.Deserialize<ComparisonRule>(ref reader, options);
            }
            
            // Check for explicit $type discriminator (fallback)
            if (root.TryGetProperty("$type", out var typeProperty))
            {
                var typeValue = typeProperty.GetString();
                return typeValue switch
                {
                    "group" => JsonSerializer.Deserialize<PolicyGroup>(ref reader, options),
                    "comparison" => JsonSerializer.Deserialize<ComparisonRule>(ref reader, options),
                    _ => throw new JsonException($"Unknown type discriminator: {typeValue}")
                };
            }
            
            throw new JsonException("Unable to determine PolicyRule type from JSON structure");
        }
    }

    public override void Write(Utf8JsonWriter writer, PolicyRule value, JsonSerializerOptions options)
    {
        // When serializing, include the $type discriminator for clarity
        writer.WriteStartObject();
        
        if (value is PolicyGroup group)
        {
            writer.WriteString("$type", "group");
            writer.WriteString("Condition", group.Condition);
            writer.WritePropertyName("Rules");
            JsonSerializer.Serialize(writer, group.Rules, options);
        }
        else if (value is ComparisonRule comparison)
        {
            writer.WriteString("$type", "comparison");
            writer.WriteString("Field", comparison.Field);
            writer.WriteString("Operator", comparison.Operator);
            writer.WriteString("Value", comparison.Value);
        }
        
        writer.WriteEndObject();
    }
}

