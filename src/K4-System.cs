using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Nexd.MySQL;

namespace K4ryuuSystem
{
	[MinimumApiVersion(30)]
	public partial class K4System : BasePlugin
	{
		public override string ModuleName => "K4-System";
		public override string ModuleVersion => "v1.0.1";
		public override string ModuleAuthor => "K4ryuu";
		public static string? _ModuleDirectory { get; set; }

		public override void Load(bool hotReload)
		{
			_ModuleDirectory = ModuleDirectory;

			CheckConfig(ModuleDirectory);

			MySql = new MySqlDb(config.DatabaseHost!, config.DatabaseUser!, config.DatabasePassword!, config.DatabaseName!, config.DatabasePort);
			MySql.ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `k4ranks` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(32) UNIQUE NOT NULL, `name` VARCHAR(255) NOT NULL, `rank` VARCHAR(255) NOT NULL, `points` INT NOT NULL DEFAULT 0, UNIQUE (`steam_id`));");
			MySql.ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `k4times` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(32) UNIQUE NOT NULL, `name` VARCHAR(255) NOT NULL, `all` INT NOT NULL DEFAULT 0, `ct` INT NOT NULL DEFAULT 0, `t` INT NOT NULL DEFAULT 0, `spec` INT NOT NULL DEFAULT 0, `dead` INT NOT NULL DEFAULT 0, `alive` INT NOT NULL DEFAULT 0);");
			MySql.ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `k4stats` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(32) UNIQUE NOT NULL, `name` VARCHAR(255) NOT NULL, `kills` INT NOT NULL DEFAULT 0, `deaths` INT NOT NULL DEFAULT 0, `hits` INT NOT NULL DEFAULT 0, `headshots` INT NOT NULL DEFAULT 0, `grenades` INT NOT NULL DEFAULT 0, `mvp` INT NOT NULL DEFAULT 0, `round_win` INT NOT NULL DEFAULT 0, `round_lose` INT NOT NULL DEFAULT 0);");

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

			LoadRanksFromConfig();
			SetupGameEvents();

			Log($"{ModuleName} [{ModuleVersion}] by {ModuleAuthor} has been loaded.");
		}
	}
}