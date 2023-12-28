namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Plugin;
	using CounterStrikeSharp.API.Modules.Timers;

	using Microsoft.Extensions.Logging;
	using MySqlConnector;
	using System.Text.RegularExpressions;

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
			this.Database = plugin.Database;
			this.ModuleDirectory = plugin._ModuleDirectory;

			//** ? Initialize Database */

			if (!plugin.InitializeDatabase("k4ranks", $@"
			CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4ranks` (
				`id` INT AUTO_INCREMENT PRIMARY KEY,
				`steam_id` VARCHAR(32) UNIQUE NOT NULL,
				`name` VARCHAR(255) NOT NULL,
				`rank` VARCHAR(255) NOT NULL,
				`points` INT NOT NULL DEFAULT 0,
				UNIQUE (`steam_id`)
			);"))
			{
				return;
			}

			//** ? Register Module Parts */

			Initialize_Config();
			Initialize_Events(plugin);
			Initialize_Commands(plugin);

			//** ? Hot Reload Events */

			if (hotReload)
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();

				var loadTasks = players
					.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV)
					.Select(player => LoadRankData(player.Slot, player.PlayerName, player.SteamID.ToString()))
					.ToList();

				Task.Run(async () =>
				{
					await Task.WhenAll(loadTasks);
				});

				plugin.AddTimer(Config.PointSettings.PlaytimeMinutes * 60, () =>
				{
					List<CCSPlayerController> players = Utilities.GetPlayers();

					foreach (CCSPlayerController player in players)
					{
						if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
							continue;

						if (!rankCache.ContainsPlayer(player))
							continue;

						ModifyPlayerPoints(player, Config.PointSettings.PlaytimePoints, "Playtime");
					}
				}, TimerFlags.STOP_ON_MAPCHANGE | TimerFlags.REPEAT);
			}
		}

		public void Release(bool hotReload)
		{
			this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);

			//** ? Save Player Caches */

			SaveAllPlayerCache(true);
		}
	}
}