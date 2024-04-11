namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core.Plugin;
	using CounterStrikeSharp.API.Modules.Timers;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleRank : IModuleRank
	{
		public ModuleRank(ILogger<ModuleRank> logger, IPluginContext pluginContext)
		{
			this.Logger = logger;
			this.plugin = (pluginContext.Plugin as Plugin)!;
			this.Config = plugin.Config;
		}

		public void Initialize(bool hotReload)
		{
			this.Logger.LogInformation("Initializing '{0}'", this.GetType().Name);

			//** ? Register Module Parts */

			Initialize_Config();
			Initialize_Menus();
			Initialize_Events();
			Initialize_Commands();

			//** ? Register Timers */

			plugin.AddTimer(Config.PointSettings.PlaytimeMinutes * 60, () =>
			{
				foreach (K4Player k4player in plugin.K4Players)
				{
					if (!k4player.IsValid || !k4player.IsPlayer)
						continue;

					if (k4player.Controller.Team == CsTeam.Terrorist)
						ModifyPlayerPoints(k4player, Config.PointSettings.PlaytimePoints, "k4.phrases.playtime");
				}
			}, TimerFlags.REPEAT);
		}

		public void Release(bool hotReload)
		{
			this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}