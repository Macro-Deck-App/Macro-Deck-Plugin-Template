using MacroDeck.Plugin.Hosting;
using MacroDeck.PluginTemplate;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
var plugin = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.RegisterIntegration<PluginIntegration>()
	.Build();

await plugin.RunAsync();
