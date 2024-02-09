namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core.Plugin;
	using CounterStrikeSharp.API.Modules.Timers;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleRank : IModuleRank
	{
		public ModuleRank(ILogger<ModuleRank> logger, IPluginContext pluginContext)
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
			this.ModuleDirectory = plugin._ModuleDirectory;

			//** ? Register Module Parts */

			Initialize_Config(plugin);
			Initialize_Menus(plugin);
			Initialize_Events(plugin);
			Initialize_Commands(plugin);

			//** ? Register Timers */

			plugin.AddTimer(Config.PointSettings.PlaytimeMinutes * 60, () =>
			{
				Utilities.GetPlayers().Where(p => p.TeamNum == (int)CsTeam.Terrorist)
					.ToList()
					.ForEach(p => ModifyPlayerPoints(p, Config.PointSettings.PlaytimePoints, "k4.phrases.playtime"));
			}, TimerFlags.REPEAT);
		}

		public void Release(bool hotReload)
		{
			this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}