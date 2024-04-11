namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API.Core.Plugin;

	public partial class ModuleTime : IModuleTime
	{
		public ModuleTime(ILogger<ModuleTime> logger, IPluginContext pluginContext)
		{
			this.Logger = logger;
			this.plugin = (pluginContext.Plugin as Plugin)!;
			this.Config = plugin.Config;
		}

		public void Initialize(bool hotReload)
		{
			this.Logger.LogInformation("Initializing '{0}'", this.GetType().Name);

			//** ? Register Module Parts */

			Initialize_Events();
			Initialize_Commands();
		}

		public void Release(bool hotReload)
		{
			this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}