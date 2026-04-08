using TranarcApiTemplate.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("tranarc");
    config.AddCommand<NewCommand>("new")
        .WithDescription("Create a new API project from the scaffold template.")
        .WithExample("new")
        .WithExample("new", "--name", "MyApp", "--company", "Acme")
        .WithExample("new", "--name", "MyApp", "--modules", "consumer,webhook,hangfire");

    config.AddCommand<AddCommand>("add")
        .WithDescription("Add a module to an existing project.")
        .WithExample("add", "consumer")
        .WithExample("add", "hangfire", "--path", "./MyApp");
});

return app.Run(args);
