namespace K4System
{
	using System.Text.RegularExpressions;
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Plugin;

	using Microsoft.Extensions.Logging;
	using MySqlConnector;

	public partial class ModuleStat : IModuleStat
	{
		public ModuleStat(ILogger<ModuleStat> logger, IPluginContext pluginContext)
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

			//** ? Initialize Database */

			if (!plugin.InitializeDatabase("k4stats", $@"CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4stats` (
				`id` INT AUTO_INCREMENT PRIMARY KEY,
				`steam_id` VARCHAR(32) COLLATE 'utf8_unicode_ci' UNIQUE NOT NULL,
				`name` VARCHAR(255) COLLATE 'utf8_unicode_ci' NOT NULL,
				`lastseen` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
				`kills` INT NOT NULL DEFAULT 0,
				`firstblood` INT NOT NULL DEFAULT 0,
				`deaths` INT NOT NULL DEFAULT 0,
				`assists` INT NOT NULL DEFAULT 0,
				`shoots` INT NOT NULL DEFAULT 0,
				`hits_taken` INT NOT NULL DEFAULT 0,
				`hits_given` INT NOT NULL DEFAULT 0,
				`headshots` INT NOT NULL DEFAULT 0,
				`grenades` INT NOT NULL DEFAULT 0,
				`mvp` INT NOT NULL DEFAULT 0,
				`round_win` INT NOT NULL DEFAULT 0,
				`round_lose` INT NOT NULL DEFAULT 0,
				`game_win` INT NOT NULL DEFAULT 0,
				`game_lose` INT NOT NULL DEFAULT 0,
				`kda` DECIMAL(5, 2) NOT NULL DEFAULT 0,
				UNIQUE (`steam_id`)
			);"))
			{
				return;
			}

			//** ? Register Module Parts */

			Initialize_Events(plugin);
			Initialize_Commands(plugin);

			//** ? Hot Reload Events */

			if (hotReload)
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();

				var loadTasks = players
					.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV)
					.Select(player => LoadStatData(player.Slot, player.PlayerName, player.SteamID.ToString()))
					.ToList();

				Task.Run(async () =>
				{
					await Task.WhenAll(loadTasks);
				});
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