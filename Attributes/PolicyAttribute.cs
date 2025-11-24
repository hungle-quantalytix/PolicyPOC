namespace PolicyPOC.Attributes;

/// <summary>
/// Dynamic policy-based authorization attribute that works with middleware.
/// Policies are evaluated from database at runtime via DynamicPolicyMiddleware.
/// Compatible with ASP.NET Core Controllers, Azure Functions, and Minimal APIs.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class PolicyAttribute : Attribute
{
    public string ResourceName { get; }
    public string Action { get; }

    public PolicyAttribute(string resourceName, string action)
    {
        ResourceName = resourceName;
        Action = action;
    }
}

