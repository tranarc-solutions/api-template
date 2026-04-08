using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TranarcApiTemplate.Engine;
using TranarcApiTemplate.Models;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TranarcApiTemplate.Commands;

public class AddCommand : AsyncCommand<AddCommand.Settings>
{
    private const string TemplateResourcePrefix = "TranarcApiTemplate.Templates.";

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<module>")]
        [Description("Module to add: consumer, webhook, hangfire, whatsapp, payment-gateway, notifications, slack")]
        public string Module { get; set; } = "";

        [CommandOption("-p|--path")]
        [Description("Path to existing project root (defaults to current directory)")]
        public string? Path { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var moduleName = settings.Module.ToLowerInvariant().Trim();
        var projectRoot = settings.Path ?? Directory.GetCurrentDirectory();

        // Validate module name
        if (!ProjectConfig.AvailableModules.Contains(moduleName))
        {
            AnsiConsole.MarkupLine($"[red]Unknown module:[/] {moduleName}");
            AnsiConsole.MarkupLine($"[dim]Available: {string.Join(", ", ProjectConfig.AvailableModules)}[/]");
            return 1;
        }

        // Find .sln file to detect project
        var slnFiles = Directory.GetFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly);
        if (slnFiles.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No .sln file found in the specified directory.[/]");
            AnsiConsole.MarkupLine("[dim]Run this command from your project root, or use --path.[/]");
            return 1;
        }

        var slnFile = slnFiles[0];
        var projectName = System.IO.Path.GetFileNameWithoutExtension(slnFile);

        // Detect existing modules
        var existingModules = DetectExistingModules(projectRoot, projectName);

        if (existingModules.Contains(moduleName))
        {
            AnsiConsole.MarkupLine($"[yellow]Module '{moduleName}' is already present in this project.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Adding [green]{moduleName}[/] module to [cyan]{projectName}[/]...");
        AnsiConsole.WriteLine();

        // Build the config with ALL modules (existing + new) so Scriban conditionals work
        var allModules = new HashSet<string>(existingModules) { moduleName };
        var config = new ProjectConfig
        {
            Name = projectName,
            CompanyName = projectName, // Best guess — not critical for adding modules
            OutputPath = projectRoot,
            Modules = allModules
        };

        var model = BuildModel(config);
        var assembly = Assembly.GetExecutingAssembly();
        var manifestJson = await ReadResourceAsync(assembly, TemplateResourcePrefix + "manifest.json");
        var manifest = JsonSerializer.Deserialize<TemplateManifest>(manifestJson)!;

        var filesAdded = 0;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Adding {moduleName} module...", async ctx =>
            {
                foreach (var entry in manifest.Files)
                {
                    // Only process files for the module being added
                    if (entry.RequiresModule != moduleName)
                        continue;

                    var outputRelativePath = entry.OutputPath
                        .Replace("{Name}", config.Name)
                        .Replace("{name_lower}", config.NameLower);

                    var fullPath = System.IO.Path.Combine(projectRoot, outputRelativePath);

                    // Don't overwrite existing files
                    if (File.Exists(fullPath))
                    {
                        AnsiConsole.MarkupLine($"  [yellow]skip[/] {outputRelativePath} (already exists)");
                        continue;
                    }

                    ctx.Status($"Creating {outputRelativePath}...");

                    var resourceName = TemplateResourcePrefix + entry.ResourceName;
                    var templateContent = await ReadResourceAsync(assembly, resourceName);

                    string rendered;
                    if (entry.IsTemplate)
                    {
                        var template = Template.Parse(templateContent);
                        var templateContext = new TemplateContext();
                        templateContext.PushGlobal(model);
                        rendered = await template.RenderAsync(templateContext);
                    }
                    else
                    {
                        rendered = templateContent;
                    }

                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
                    await File.WriteAllTextAsync(fullPath, rendered);
                    AnsiConsole.MarkupLine($"  [green]added[/] {outputRelativePath}");
                    filesAdded++;
                }

                // Add project to solution if it's a new project (Consumer/Webhook)
                if (moduleName is "consumer" or "webhook")
                {
                    var projDir = moduleName == "consumer"
                        ? $"{projectName}.Consumer"
                        : $"{projectName}.Webhook";
                    var csprojPath = $"{projDir}/{projDir}.csproj";

                    if (File.Exists(System.IO.Path.Combine(projectRoot, csprojPath)))
                    {
                        ctx.Status("Adding project to solution...");
                        await RunProcessAsync("dotnet", $"sln add {csprojPath}", projectRoot);
                        AnsiConsole.MarkupLine($"  [green]added[/] {csprojPath} to solution");
                    }
                }

                // Add required NuGet packages for the module
                ctx.Status("Adding NuGet packages...");
                await AddModulePackagesAsync(moduleName, projectRoot, projectName);

                // Restore packages
                ctx.Status("Restoring packages...");
                await RunProcessAsync("dotnet", "restore", projectRoot);
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Module '{moduleName}' added successfully.[/] ({filesAdded} files created)");

        // Module-specific guidance
        PrintPostAddGuidance(moduleName, projectName);

        return 0;
    }

    private static async Task AddModulePackagesAsync(string moduleName, string projectRoot, string projectName)
    {
        var coreCsproj = System.IO.Path.Combine(projectRoot, "Core", "Core.csproj");
        var hostCsproj = System.IO.Path.Combine(projectRoot, $"{projectName}.Host", $"{projectName}.Host.csproj");

        switch (moduleName)
        {
            case "consumer":
                // Host needs MassTransit for publishing
                await RunProcessAsync("dotnet", $"add \"{hostCsproj}\" package MassTransit.Azure.ServiceBus.Core --version 8.5.8", projectRoot);
                await RunProcessAsync("dotnet", $"add \"{hostCsproj}\" package MassTransit.RabbitMQ --version 8.5.8", projectRoot);
                // Core needs MassTransit + Service Bus for health checks
                await RunProcessAsync("dotnet", $"add \"{coreCsproj}\" package MassTransit.Azure.ServiceBus.Core --version 8.5.8", projectRoot);
                await RunProcessAsync("dotnet", $"add \"{coreCsproj}\" package Azure.Messaging.ServiceBus --version 7.20.1", projectRoot);
                await RunProcessAsync("dotnet", $"add \"{coreCsproj}\" package AspNetCore.HealthChecks.AzureServiceBus --version 9.0.0", projectRoot);
                break;

            case "hangfire":
                await RunProcessAsync("dotnet", $"add \"{coreCsproj}\" package Hangfire.Core --version 1.8.23", projectRoot);
                await RunProcessAsync("dotnet", $"add \"{coreCsproj}\" package Hangfire.AspNetCore --version 1.8.23", projectRoot);
                await RunProcessAsync("dotnet", $"add \"{coreCsproj}\" package Hangfire.PostgreSql --version 1.21.1", projectRoot);
                break;

            case "slack":
                await RunProcessAsync("dotnet", $"add \"{coreCsproj}\" package Serilog.Sinks.Slack --version 2.2.3", projectRoot);
                break;
        }
    }

    private static HashSet<string> DetectExistingModules(string projectRoot, string projectName)
    {
        var modules = new HashSet<string>();

        if (Directory.Exists(System.IO.Path.Combine(projectRoot, $"{projectName}.Consumer")))
            modules.Add("consumer");
        if (Directory.Exists(System.IO.Path.Combine(projectRoot, $"{projectName}.Webhook")))
            modules.Add("webhook");
        if (Directory.Exists(System.IO.Path.Combine(projectRoot, "Core", "Hangfire")))
            modules.Add("hangfire");
        if (Directory.Exists(System.IO.Path.Combine(projectRoot, "Core", "Notifications", "Slack")))
            modules.Add("slack");
        if (File.Exists(System.IO.Path.Combine(projectRoot, "Core", "Notifications", "AlertSendingService.cs")))
            modules.Add("notifications");
        if (Directory.Exists(System.IO.Path.Combine(projectRoot, "Core", "WhatsApp")))
            modules.Add("whatsapp");
        if (Directory.Exists(System.IO.Path.Combine(projectRoot, "Core", "PaymentGateway")))
            modules.Add("payment-gateway");

        return modules;
    }

    private static void PrintPostAddGuidance(string moduleName, string projectName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Next steps:[/]");

        switch (moduleName)
        {
            case "consumer":
                AnsiConsole.MarkupLine($"  1. Add consumer registrations in {projectName}.Consumer/Startup.cs");
                AnsiConsole.MarkupLine("  2. Wire AddMessageBroker() in Host's Program.cs if not already present");
                AnsiConsole.MarkupLine("  3. Add message-broker.json configs if needed");
                break;
            case "webhook":
                AnsiConsole.MarkupLine($"  1. Add webhook endpoints in {projectName}.Webhook/Endpoints/");
                AnsiConsole.MarkupLine("  2. Configure HMAC secrets in security config");
                break;
            case "hangfire":
                AnsiConsole.MarkupLine("  1. Add AddHangfireDashboardOnly() to Host's Startup.AddHostCore()");
                AnsiConsole.MarkupLine("  2. Add AddHangfireServices() to Consumer's Program.cs (if using Consumer)");
                AnsiConsole.MarkupLine("  3. Create recurring jobs extending HangfireJobBase");
                break;
            case "slack":
                AnsiConsole.MarkupLine("  1. Set SlackUrlWithAccessToken in config/secrets");
                AnsiConsole.MarkupLine("  2. Inject ISlackService where needed");
                break;
            case "notifications":
                AnsiConsole.MarkupLine("  1. Inject IAlertSendingService where needed");
                AnsiConsole.MarkupLine("  2. Implement channel-specific sending (email/SMS/WhatsApp)");
                break;
            case "whatsapp":
                AnsiConsole.MarkupLine("  1. Configure WhatsAppSettings in whatsapp.json");
                AnsiConsole.MarkupLine("  2. Add AddWhatsApp(config) to Core/Startup.cs");
                break;
            case "payment-gateway":
                AnsiConsole.MarkupLine("  1. Implement IPaymentProvider for your payment provider");
                AnsiConsole.MarkupLine("  2. Register it in Core/PaymentGateway/PaymentGateway.cs");
                break;
        }
    }

    private static ScriptObject BuildModel(ProjectConfig config)
    {
        var model = new ScriptObject();
        model.Add("name", config.Name);
        model.Add("name_lower", config.NameLower);
        model.Add("company_name", config.CompanyName);
        model.Add("schema_name", config.SchemaName);
        model.Add("has_consumer", config.HasConsumer);
        model.Add("has_webhook", config.HasWebhook);
        model.Add("has_hangfire", config.HasHangfire);
        model.Add("has_whatsapp", config.HasWhatsApp);
        model.Add("has_payment_gateway", config.HasPaymentGateway);
        model.Add("has_notifications", config.HasNotifications);
        model.Add("has_slack", config.HasSlack);
        return model;
    }

    private static async Task<string> ReadResourceAsync(Assembly assembly, string resourceName)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName)
                                 ?? throw new InvalidOperationException($"Missing resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task RunProcessAsync(string command, string args, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return;
        await process.WaitForExitAsync();
    }
}
