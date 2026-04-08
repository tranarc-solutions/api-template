namespace TranarcApiTemplate.Models;

public class ProjectConfig
{
    public required string Name { get; init; }
    public required string CompanyName { get; init; }
    public required string OutputPath { get; init; }
    public required HashSet<string> Modules { get; init; }

    // Derived properties for templates
    public string NameLower => Name.ToLowerInvariant();
    public string SchemaName => Name.ToLowerInvariant();
    public string NamePascal => Name; // Assumed already PascalCase from input
    public bool HasConsumer => Modules.Contains("consumer");
    public bool HasWebhook => Modules.Contains("webhook");
    public bool HasHangfire => Modules.Contains("hangfire");
    public bool HasWhatsApp => Modules.Contains("whatsapp");
    public bool HasPaymentGateway => Modules.Contains("payment-gateway");
    public bool HasNotifications => Modules.Contains("notifications");
    public bool HasSlack => Modules.Contains("slack");

    public static readonly string[] AvailableModules =
    [
        "consumer",
        "webhook",
        "hangfire",
        "whatsapp",
        "payment-gateway",
        "notifications",
        "slack"
    ];
}
