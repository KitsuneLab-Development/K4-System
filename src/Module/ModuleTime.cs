namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Plugin;

	using Microsoft.Extensions.Logging;

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
			this.Database = plugin.Database;

			//** ? Initialize Database */

			this.Database.ExecuteNonQueryAsync(
				@$"CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4times` (
					`id` INT AUTO_INCREMENT PRIMARY KEY,
					`steam_id` VARCHAR(32) UNIQUE NOT NULL,
					`name` VARCHAR(255) NOT NULL,
					`all` INT NOT NULL DEFAULT 0,
					`ct` INT NOT NULL DEFAULT 0,
					`t` INT NOT NULL DEFAULT 0,
					`spec` INT NOT NULL DEFAULT 0,
					`dead` INT NOT NULL DEFAULT 0,
					`alive` INT NOT NULL DEFAULT 0,
					UNIQUE (`steam_id`)
				);"
			);

			//** ? Register Module Parts */

			Initialize_Events(plugin);
			Initialize_Commands(plugin);

			//** ? Hot Reload Events */

			if (hotReload)
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();

				var loadTasks = players
					.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV)
					.Select(player => LoadTimeData(player.Slot, player.PlayerName, player.SteamID.ToString()))
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