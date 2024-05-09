namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API.Core.Plugin;

	public partial class ModuleStat : IModuleStat
	{
		public ModuleStat(ILogger<ModuleStat> logger, IPluginContext pluginContext)
		{
			this.Logger = logger;
			this.pluginContext = pluginContext;
		}

		public void Initialize(bool hotReload)
		{
			this.plugin = (pluginContext.Plugin as Plugin)!;
			this.Config = plugin.Config;

			if (Config.GeneralSettings.LoadMessages)
				this.Logger.LogInformation("Initializing '{0}'", this.GetType().Name);

			//** ? Register Module Parts */

			Initialize_Events();
			Initialize_Commands();
		}

		public void Release(bool hotReload)
		{
			if (Config.GeneralSettings.LoadMessages)
				this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}