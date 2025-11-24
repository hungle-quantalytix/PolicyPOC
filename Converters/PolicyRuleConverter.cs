using System.Text.Json;
using System.Text.Json.Serialization;
using PolicyPOC.Models;

namespace PolicyPOC.Converters;

public class PolicyRuleConverter : JsonConverter<PolicyRule>
{
    public override PolicyRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var root = doc.RootElement;
            
            // Check if it's a ComparisonRule (has field, operator, value)
            if (root.TryGetProperty("field", out _) || root.TryGetProperty("Field", out _))
            {
                return DeserializeElement<ComparisonRule>(root, options);
            }
            // Check if it's a PolicyGroup (has condition, rules)
            else if (root.TryGetProperty("condition", out _) || root.TryGetProperty("Condition", out _))
            {
                return DeserializeElement<PolicyGroup>(root, options);
            }
            
            throw new JsonException("Unable to determine PolicyRule type from JSON properties");
        }
    }

    private T DeserializeElement<T>(JsonElement element, JsonSerializerOptions options)
    {
        // Deserialize the specific concrete type
        var result = Activator.CreateInstance<T>();
        
        foreach (var property in element.EnumerateObject())
        {
            var prop = typeof(T).GetProperty(property.Name, 
                System.Reflection.BindingFlags.IgnoreCase | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
            
            if (prop != null && prop.CanWrite)
            {
                var propertyType = prop.PropertyType;
                object? value;
                
                // Handle ICollection<PolicyRule> specifically
                if (propertyType.IsGenericType && 
                    propertyType.GetGenericTypeDefinition() == typeof(ICollection<>) &&
                    propertyType.GetGenericArguments()[0] == typeof(PolicyRule))
                {
                    var list = new List<PolicyRule>();
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        var rule = ReadElement(item, typeof(PolicyRule), options);
                        if (rule != null)
                        {
                            list.Add(rule);
                        }
                    }
                    value = list;
                }
                else
                {
                    value = JsonSerializer.Deserialize(property.Value.GetRawText(), propertyType, options);
                }
                
                prop.SetValue(result, value);
            }
        }
        
        return result;
    }
    
    private PolicyRule? ReadElement(JsonElement element, Type typeToConvert, JsonSerializerOptions options)
    {
        // Check if it's a ComparisonRule (has field, operator, value)
        if (element.TryGetProperty("field", out _) || element.TryGetProperty("Field", out _))
        {
            return DeserializeElement<ComparisonRule>(element, options);
        }
        // Check if it's a PolicyGroup (has condition, rules)
        else if (element.TryGetProperty("condition", out _) || element.TryGetProperty("Condition", out _))
        {
            return DeserializeElement<PolicyGroup>(element, options);
        }
        
        return null;
    }

    public override void Write(Utf8JsonWriter writer, PolicyRule value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ComparisonRule comparison:
                writer.WriteStartObject();
                WriteProperty(writer, "field", comparison.Field, options);
                WriteProperty(writer, "operator", comparison.Operator, options);
                WriteProperty(writer, "value", comparison.Value, options);
                writer.WriteEndObject();
                break;
                
            case PolicyGroup group:
                writer.WriteStartObject();
                WriteProperty(writer, "condition", group.Condition, options);
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName("Rules") ?? "Rules");
                writer.WriteStartArray();
                foreach (var rule in group.Rules)
                {
                    Write(writer, rule, options);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
                break;
        }
    }
    
    private void WriteProperty(Utf8JsonWriter writer, string propertyName, string value, JsonSerializerOptions options)
    {
        var name = options.PropertyNamingPolicy?.ConvertName(char.ToUpper(propertyName[0]) + propertyName.Substring(1)) ?? propertyName;
        writer.WriteString(name, value);
    }
}

