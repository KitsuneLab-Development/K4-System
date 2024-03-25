namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API.Core.Plugin;

	public partial class ModuleTime : IModuleTime
	{
		public ModuleTime(ILogger<ModuleTime> logger, IPluginContext pluginContext)
		{
			this.Logger = logger;
			this.PluginContext = (pluginContext as PluginContext)!;
		}

		public void Initialize(bool hotReload)
		{
			this.Logger.LogInformation("Initializing '{0}'", this.GetType().Name);

			//** ? Forwarded Variables */

			Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

			this.Config = plugin.Config;

			//** ? Register Module Parts */

			Initialize_Events(plugin);
			Initialize_Commands(plugin);
		}

		public void Release(bool hotReload)
		{
			this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}