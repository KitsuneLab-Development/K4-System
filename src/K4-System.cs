using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Nexd.MySQL;


namespace K4ryuuSystem
{
	[MinimumApiVersion(50)]
	public partial class K4System : BasePlugin, IPluginConfig<MyConfig>
	{
		public override string ModuleName => "K4-System";
		public override string ModuleVersion => "v1.3.3";
		public override string ModuleAuthor => "K4ryuu";
		public int ModuleConfigVersion => 2;

		public override void Load(bool hotReload)
		{
			Log($"{ModuleName} [{ModuleVersion}] by {ModuleAuthor} is starting to load.", LogLevel.Debug, hotReload);

			try
			{
				MySql = new MySqlDb(Config.DatabaseSettings.Host, Config.DatabaseSettings.Username, Config.DatabaseSettings.Password, Config.DatabaseSettings.Database, Config.DatabaseSettings.Port);

				Log("Database connection established successfully.", LogLevel.Debug, hotReload);

				MySql.ExecuteNonQueryAsync(@$"CREATE TABLE IF NOT EXISTS `{TablePrefix}k4ranks` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(32) UNIQUE NOT NULL, `name` VARCHAR(255) NOT NULL, `rank` VARCHAR(255) NOT NULL, `points` INT NOT NULL DEFAULT 0, UNIQUE (`steam_id`));");
				MySql.ExecuteNonQueryAsync(@$"CREATE TABLE IF NOT EXISTS `{TablePrefix}k4times` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(32) UNIQUE NOT NULL, `name` VARCHAR(255) NOT NULL, `all` INT NOT NULL DEFAULT 0, `ct` INT NOT NULL DEFAULT 0, `t` INT NOT NULL DEFAULT 0, `spec` INT NOT NULL DEFAULT 0, `dead` INT NOT NULL DEFAULT 0, `alive` INT NOT NULL DEFAULT 0);");
				MySql.ExecuteNonQueryAsync(@$"CREATE TABLE IF NOT EXISTS `{TablePrefix}k4stats` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(32) UNIQUE NOT NULL, `name` VARCHAR(255) NOT NULL, `lastseen` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, `kills` INT NOT NULL DEFAULT 0, `deaths` INT NOT NULL DEFAULT 0, `hits` INT NOT NULL DEFAULT 0, `headshots` INT NOT NULL DEFAULT 0, `grenades` INT NOT NULL DEFAULT 0, `mvp` INT NOT NULL DEFAULT 0, `round_win` INT NOT NULL DEFAULT 0, `round_lose` INT NOT NULL DEFAULT 0);");

				LoadRanksFromConfig();

				if (hotReload)
				{
					List<CCSPlayerController> players = Utilities.GetPlayers();

					foreach (CCSPlayerController player in players)
					{
						if (player.IsBot)
							continue;

						LoadPlayerData(player);
					}
				}

				SetupGameEvents();

				Log($"{ModuleName} [{ModuleVersion}] by {ModuleAuthor} has been loaded.", LogLevel.Debug, hotReload);
			}
			catch (Exception ex)
			{
				Log($"Error while loading: {ex.Message}", LogLevel.Error);
			}
		}

		public override void Unload(bool hotReload)
		{
			Log($"{ModuleName} [{ModuleVersion}] by {ModuleAuthor} is starting to unload.", LogLevel.Debug);

			try
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();

				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot)
						continue;

					if (PlayerSummaries.ContainsPlayer(player))
					{
						if (Config.GeneralSettings.ModuleTimes)
							SaveClientTime(player);

						if (Config.GeneralSettings.ModuleStats)
							SaveClientStats(player);

						if (Config.GeneralSettings.ModuleRanks)
							SaveClientRank(player);

						PlayerSummaries.RemovePlayer(player);
					}
				}

				Log($"{ModuleName} [{ModuleVersion}] by {ModuleAuthor} has been unloaded. Database saved.", LogLevel.Debug);
			}
			catch (Exception ex)
			{
				Log($"Error during plugin unload: {ex.Message}", LogLevel.Error);
			}
		}
	}
}