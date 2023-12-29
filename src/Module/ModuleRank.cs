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
			this.Database = plugin.Database;
			this.ModuleDirectory = plugin._ModuleDirectory;

			//** ? Initialize Database */

			if (!plugin.InitializeDatabase("k4ranks", $@"
			CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4ranks` (
				`id` INT AUTO_INCREMENT PRIMARY KEY,
				`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' UNIQUE NOT NULL,
				`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
				`rank` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
				`points` INT NOT NULL DEFAULT 0,
				UNIQUE (`steam_id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;"))
			{
				return;
			}

			//** ? Register Module Parts */

			Initialize_Config(plugin);
			Initialize_Menus(plugin);
			Initialize_Events(plugin);
			Initialize_Commands(plugin);

			//** ? Hot Reload Events */

			if (hotReload)
			{
				LoadAllPlayerCache();

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

			//** ? Save Player Caches */

			SaveAllPlayerCache(true);
		}
	}
}