namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Plugin;
	using CounterStrikeSharp.API.Modules.Timers;

	using Microsoft.Extensions.Logging;

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

			//** ? Hot Reload Events */

			if (hotReload)
			{
				globalGameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

				plugin.AddTimer(Config.PointSettings.PlaytimeMinutes * 60, () =>
				{
					List<CCSPlayerController> players = Utilities.GetPlayers();

					foreach (CCSPlayerController player in players)
					{
						if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
							continue;

						if (!rankCache.ContainsPlayer(player))
							continue;

						ModifyPlayerPoints(player, Config.PointSettings.PlaytimePoints, "k4.phrases.playtime");
					}
				}, TimerFlags.STOP_ON_MAPCHANGE | TimerFlags.REPEAT);
			}
		}

		public void Release(bool hotReload)
		{
			this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}