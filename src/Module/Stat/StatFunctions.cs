namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;

	using MySqlConnector;
	using Nexd.MySQL;
	using Microsoft.Extensions.Logging;
	using System.Text;

	public partial class ModuleStat : IModuleStat
	{
		public CCSGameRules GameRules()
		{
			if (globalGameRules is null)
				globalGameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;

			return globalGameRules;
		}

		public bool IsStatsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return (!GameRules().WarmupPeriod || Config.StatisticSettings.WarmupStats) && (Config.StatisticSettings.MinPlayers <= notBots);
		}

		public void EnsurePlayerLoaded(CCSPlayerController player)
		{

		}

		public async Task LoadStatData(CCSPlayerController player)
		{
			if (player is null || !player.IsValid)
			{
				Logger.LogWarning("LoadStatData > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"LoadStatData > Player controller is BOT or HLTV");
				return;
			}

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, `lastseen`)
				VALUES (
					'{escapedName}',
					'{player.SteamID}',
					CURRENT_TIMESTAMP
				)
				ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}',
					`lastseen` = CURRENT_TIMESTAMP;
			");

			MySqlQueryResult result = await Database.ExecuteQueryAsync($@"
				SELECT *
				FROM `{Config.DatabaseSettings.TablePrefix}k4stats`
				WHERE `steam_id` = '{player.SteamID}';
			");

			Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

			string[] statFieldNames = { "kills", "deaths", "hits_given", "hits_taken", "headshots", "grenades", "mvp", "round_win", "round_lose", "assists" };

			foreach (string statField in statFieldNames)
			{
				NewStatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
			}

			StatData playerData = new StatData
			{
				StatFields = NewStatFields,
				KDA = result.Rows > 0 ? result.Get<decimal>(0, "kda") : 0
			};

			statCache[player] = playerData;
		}

		public async Task SavePlayerStatCache(CCSPlayerController player, bool remove)
		{
			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				Logger.LogWarning("SavePlayerStatCache > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"SavePlayerStatCache > Player controller is BOT or HLTV");
				return;
			}

			if (!statCache.ContainsPlayer(player))
			{
				Logger.LogWarning($"SavePlayerStatCache > Player is not loaded to the cache ({player.PlayerName})");
				return;
			}

			StatData playerData = statCache[player];

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			StringBuilder queryBuilder = new StringBuilder();
			queryBuilder.Append($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`steam_id`, `name`, `lastseen`");

			foreach (var field in playerData.StatFields)
			{
				queryBuilder.Append($", `{field.Key}`");
			}

			queryBuilder.Append($@")
				VALUES ('{player.SteamID}', '{escapedName}', CURRENT_TIMESTAMP");

			foreach (var field in playerData.StatFields)
			{
				queryBuilder.Append($", {field.Value}");
			}

			queryBuilder.Append($@")
				ON DUPLICATE KEY UPDATE");

			foreach (var field in playerData.StatFields)
			{
				queryBuilder.Append($"`{field.Key}` = VALUES(`{field.Key}`), ");
			}

			queryBuilder.Append($@"`lastseen` = CURRENT_TIMESTAMP;");

			if (!remove)
			{
				queryBuilder.Append($@"

				SELECT * FROM `{Config.DatabaseSettings.TablePrefix}k4stats`
				WHERE `steam_id` = '{player.SteamID}';");
			}

			string insertOrUpdateQuery = queryBuilder.ToString();

			MySqlQueryResult result = await Database.ExecuteQueryAsync(insertOrUpdateQuery);

			if (!remove)
			{
				Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

				var allKeys = playerData.StatFields.Keys.ToList();

				foreach (string statField in allKeys)
				{
					NewStatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
				}

				statCache[player].StatFields = NewStatFields;
				statCache[player].KDA = result.Rows > 0 ? result.Get<decimal>(0, "kda") : 0;
			}
			else
			{
				statCache.RemovePlayer(player);
			}
		}

		public async Task SaveAllPlayerCache(bool clear)
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			var saveTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV && statCache.ContainsPlayer(player))
				.Select(player => SavePlayerStatCache(player, clear))
				.ToList();

			await Task.WhenAll(saveTasks);

			if (clear)
				statCache.Clear();
		}

		public void ModifyPlayerStats(CCSPlayerController player, string field, int amount)
		{
			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				Logger.LogWarning("ModifyPlayerStats > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"ModifyPlayerStats > Player controller is BOT or HLTV");
				return;
			}

			if (!statCache.ContainsPlayer(player))
			{
				Logger.LogWarning($"ModifyPlayerStats > Player is not loaded to the cache ({player.PlayerName})");
				return;
			}

			StatData playerData = statCache[player];
			playerData.StatFields[field] += amount;

			string[] kdaCheck = { "deaths", "kills", "assists" };

			if (kdaCheck.Contains(field))
			{
				int deaths = playerData.StatFields["deaths"];
				playerData.KDA = deaths != 0 ? (playerData.StatFields["kills"] + playerData.StatFields["assists"]) / deaths : playerData.StatFields["kills"] + playerData.StatFields["assists"];
			}
		}
	}
}