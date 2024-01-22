namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Plugin;
	using CounterStrikeSharp.API.Modules.Timers;

	using Microsoft.Extensions.Logging;

	public partial class ModuleUtils : IModuleUtils
	{
		public ModuleUtils(ILogger<ModuleUtils> logger, IPluginContext pluginContext)
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

			Initialize_Commands(plugin);
		}

		public void Release(bool hotReload)
		{
			this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}