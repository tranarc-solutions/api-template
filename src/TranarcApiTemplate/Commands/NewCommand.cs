using System.ComponentModel;
using System.Diagnostics;
using TranarcApiTemplate.Engine;
using TranarcApiTemplate.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TranarcApiTemplate.Commands;

public class NewCommand : AsyncCommand<NewCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-n|--name")]
        [Description("Project name (PascalCase, e.g. MyApp)")]
        public string? Name { get; set; }

        [CommandOption("-c|--company")]
        [Description("Company/organization name")]
        public string? Company { get; set; }

        [CommandOption("-m|--modules")]
        [Description("Comma-separated optional modules: consumer,webhook,hangfire,whatsapp,payment-gateway,notifications,slack")]
        public string? Modules { get; set; }

        [CommandOption("-o|--output")]
        [Description("Output directory (defaults to ./<name>)")]
        public string? Output { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool Yes { get; set; }

        [CommandOption("--skip-restore")]
        [Description("Skip dotnet restore after generation")]
        public bool SkipRestore { get; set; }

        [CommandOption("--skip-git")]
        [Description("Skip git init after generation")]
        public bool SkipGit { get; set; }

        public bool IsNonInteractive => Name is not null;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.Write(new FigletText("tranarc").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[dim]Scaffold a production-ready ASP.NET Core API[/]");
        AnsiConsole.WriteLine();

        var name = settings.Name ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Project [green]name[/] (PascalCase):")
                .Validate(n =>
                    !string.IsNullOrWhiteSpace(n) && char.IsUpper(n[0])
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Must be PascalCase (start with uppercase)")));

        var company = settings.Company ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Company/org [green]name[/]:")
                .DefaultValue(name));

        HashSet<string> modules;
        if (settings.Modules is not null)
        {
            modules = settings.Modules
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(m => m.ToLowerInvariant())
                .ToHashSet();
        }
        else
        {
            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Select [green]optional modules[/]:")
                    .NotRequired()
                    .AddChoices(ProjectConfig.AvailableModules)
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]"));
            modules = selected.ToHashSet();
        }

        var outputPath = settings.Output ?? Path.Combine(Directory.GetCurrentDirectory(), name);

        var config = new ProjectConfig
        {
            Name = name,
            CompanyName = company,
            OutputPath = outputPath,
            Modules = modules
        };

        // Show summary
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Project Name", config.Name);
        table.AddRow("Company", config.CompanyName);
        table.AddRow("Output", config.OutputPath);
        table.AddRow("Modules", modules.Count > 0 ? string.Join(", ", modules) : "[dim]none[/]");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!settings.Yes && !settings.IsNonInteractive)
        {
            if (!AnsiConsole.Confirm("Generate project?"))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return 1;
            }
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating project...", async ctx =>
            {
                var generator = new ProjectGenerator();
                await generator.GenerateAsync(config, msg => ctx.Status(msg));
            });

        AnsiConsole.MarkupLine("[green]Project generated.[/]");

        // Post-generation: git init
        if (!settings.SkipGit)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Initializing git repository...", async _ =>
                {
                    await RunProcessAsync("git", "init", outputPath);
                    await RunProcessAsync("git", "add -A", outputPath);
                    await RunProcessAsync("git", "commit -m \"Initial scaffold from tranarc-api-template\"", outputPath);
                });
            AnsiConsole.MarkupLine("[green]Git repository initialized with initial commit.[/]");
        }

        // Post-generation: dotnet restore
        if (!settings.SkipRestore)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Restoring NuGet packages...", async _ =>
                {
                    await RunProcessAsync("dotnet", "restore", outputPath);
                });
            AnsiConsole.MarkupLine("[green]NuGet packages restored.[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Project ready at:[/] {config.OutputPath}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Next steps:[/]");
        AnsiConsole.MarkupLine($"  cd {name}");
        if (settings.SkipRestore)
            AnsiConsole.MarkupLine("  dotnet restore");
        AnsiConsole.MarkupLine("  dotnet build");
        AnsiConsole.MarkupLine($"  cd {name}.Host && dotnet run");

        return 0;
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
