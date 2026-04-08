using System.Reflection;
using System.Text;
using System.Text.Json;
using TranarcApiTemplate.Models;
using Scriban;
using Scriban.Runtime;

namespace TranarcApiTemplate.Engine;

public class ProjectGenerator
{
    private const string TemplateResourcePrefix = "TranarcApiTemplate.Templates.";

    public async Task GenerateAsync(ProjectConfig config, Action<string> onStatus)
    {
        Directory.CreateDirectory(config.OutputPath);

        var model = BuildModel(config);
        var assembly = Assembly.GetExecutingAssembly();

        // Read manifest
        var manifestJson = await ReadResourceAsync(assembly, TemplateResourcePrefix + "manifest.json");
        var manifest = JsonSerializer.Deserialize<TemplateManifest>(manifestJson)!;

        foreach (var entry in manifest.Files)
        {
            // Check module conditions
            if (!string.IsNullOrEmpty(entry.RequiresModule) && !config.Modules.Contains(entry.RequiresModule))
                continue;

            var outputRelativePath = RenderPath(entry.OutputPath, config);
            onStatus($"Creating {outputRelativePath}...");

            var resourceName = TemplateResourcePrefix + entry.ResourceName;
            var templateContent = await ReadResourceAsync(assembly, resourceName);

            string rendered;
            if (entry.IsTemplate)
            {
                rendered = await RenderTemplateAsync(templateContent, model);
            }
            else
            {
                rendered = templateContent;
            }

            var fullPath = Path.Combine(config.OutputPath, outputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, rendered);
        }

        onStatus("Generating solution file...");
        await GenerateSolutionFileAsync(config);

        // Generate .gitignore
        onStatus("Creating .gitignore...");
        await GenerateGitignoreAsync(config);

        onStatus("Done!");
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

    private static string RenderPath(string pathTemplate, ProjectConfig config)
    {
        return pathTemplate
            .Replace("{Name}", config.Name)
            .Replace("{name_lower}", config.NameLower);
    }

    private static async Task<string> ReadResourceAsync(Assembly assembly, string resourceName)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task<string> RenderTemplateAsync(string templateContent, ScriptObject model)
    {
        var template = Template.Parse(templateContent);
        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Template parse error: {string.Join(", ", template.Messages)}");

        var context = new TemplateContext();
        context.PushGlobal(model);
        return await template.RenderAsync(context);
    }

    private async Task GenerateSolutionFileAsync(ProjectConfig config)
    {
        var slnPath = Path.Combine(config.OutputPath, $"{config.Name}.sln");

        var projects = new List<(string name, string path, string guid)>
        {
            ($"{config.Name}.Host", $"{config.Name}.Host/{config.Name}.Host.csproj", NewGuid()),
            ("Core", "Core/Core.csproj", NewGuid()),
            ("Shared", "Shared/Shared.csproj", NewGuid()),
            ($"{config.Name}.SourceGenerator", $"{config.Name}.SourceGenerator/{config.Name}.SourceGenerator.csproj", NewGuid()),
            ($"{config.Name}.Tests.Unit", $"Tests/{config.Name}.Tests.Unit/{config.Name}.Tests.Unit.csproj", NewGuid()),
            ($"{config.Name}.Tests.Integration", $"Tests/{config.Name}.Tests.Integration/{config.Name}.Tests.Integration.csproj", NewGuid()),
        };

        if (config.HasConsumer)
            projects.Add(($"{config.Name}.Consumer", $"{config.Name}.Consumer/{config.Name}.Consumer.csproj", NewGuid()));
        if (config.HasWebhook)
            projects.Add(($"{config.Name}.Webhook", $"{config.Name}.Webhook/{config.Name}.Webhook.csproj", NewGuid()));

        var testFolderGuid = NewGuid();
        const string csharpType = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";
        const string folderType = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");

        sb.AppendLine($"Project(\"{{{folderType}}}\") = \"Tests\", \"Tests\", \"{{{testFolderGuid}}}\"");
        sb.AppendLine("EndProject");

        foreach (var (name, path, guid) in projects)
        {
            sb.AppendLine($"Project(\"{{{csharpType}}}\") = \"{name}\", \"{path}\", \"{{{guid}}}\"");
            sb.AppendLine("EndProject");
        }

        sb.AppendLine("Global");
        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var (_, _, guid) in projects)
        {
            sb.AppendLine($"\t\t{{{guid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"\t\t{{{guid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"\t\t{{{guid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"\t\t{{{guid}}}.Release|Any CPU.Build.0 = Release|Any CPU");
        }
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(NestedProjects) = preSolution");
        foreach (var (_, _, guid) in projects.Where(p => p.name.Contains(".Tests.")))
            sb.AppendLine($"\t\t{{{guid}}} = {{{testFolderGuid}}}");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("EndGlobal");

        await File.WriteAllTextAsync(slnPath, sb.ToString());
    }

    private static async Task GenerateGitignoreAsync(ProjectConfig config)
    {
        var content = """
                      bin/
                      obj/
                      .vs/
                      .idea/
                      *.user
                      *.suo
                      *.DotSettings.user
                      appsettings.Development.json
                      Configurations/secrets.Local.json
                      dataprotection/
                      *.pfx
                      *.pem
                      .env
                      """;
        await File.WriteAllTextAsync(Path.Combine(config.OutputPath, ".gitignore"), content);
    }

    private static string NewGuid() => Guid.NewGuid().ToString("D").ToUpper();
}

public class TemplateManifest
{
    public List<TemplateEntry> Files { get; set; } = [];
}

public class TemplateEntry
{
    public string ResourceName { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public bool IsTemplate { get; set; } = true;
    public string? RequiresModule { get; set; }
}
