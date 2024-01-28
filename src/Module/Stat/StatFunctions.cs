namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;

	using MySqlConnector;
	using Nexd.MySQL;
	using Microsoft.Extensions.Logging;
	using System.Text;
	using CounterStrikeSharp.API.Modules.Entities;

	public partial class ModuleStat : IModuleStat
	{
		public bool IsStatsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return globalGameRules != null && (!globalGameRules.WarmupPeriod || Config.StatisticSettings.WarmupStats) && (Config.StatisticSettings.MinPlayers <= notBots);
		}

		public async Task LoadStatData(int slot, string name, string steamid)
		{
			string escapedName = MySqlHelper.EscapeString(name);

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, `lastseen`)
				VALUES (
					'{escapedName}',
					'{steamid}',
					CURRENT_TIMESTAMP
				)
				ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}',
					`lastseen` = CURRENT_TIMESTAMP;
			");

			MySqlQueryResult result = await Database.ExecuteQueryAsync($@"
				SELECT *
				FROM `{Config.DatabaseSettings.TablePrefix}k4stats`
				WHERE `steam_id` = '{steamid}';
			");

			Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

			string[] statFieldNames = { "kills", "shoots", "firstblood", "deaths", "hits_given", "hits_taken", "headshots", "grenades", "mvp", "round_win", "round_lose", "game_win", "game_lose", "assists" };

			foreach (string statField in statFieldNames)
			{
				NewStatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
			}

			StatData playerData = new StatData
			{
				StatFields = NewStatFields,
			};

			statCache[slot] = playerData;
		}

		public void SavePlayerStatCache(CCSPlayerController player, bool remove)
		{
			int savedSlot = player.Slot;
			string savedName = player.PlayerName;

			SteamID steamid = new SteamID(player.SteamID);

			Task.Run(async () =>
			{
				await SavePlayerStatCacheAsync(savedSlot, savedName, steamid, remove);
			});
		}

		public async Task SavePlayerStatCacheAsync(int slot, string name, SteamID steamid, bool remove)
		{
			if (!statCache.ContainsKey(slot))
			{
				Logger.LogWarning($"SavePlayerStatCache > Player is not loaded to the cache ({name})");
				return;
			}

			StatData playerData = statCache[slot];

			string escapedName = MySqlHelper.EscapeString(name);

			StringBuilder queryBuilder = new StringBuilder();
			queryBuilder.Append($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`steam_id`, `name`, `lastseen`");

			foreach (var field in playerData.StatFields)
			{
				queryBuilder.Append($", `{field.Key}`");
			}

			queryBuilder.Append($@")
				VALUES ('{steamid.SteamId64}', '{escapedName}', CURRENT_TIMESTAMP");

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
				WHERE `steam_id` = '{steamid.SteamId64}';");
			}

			string insertOrUpdateQuery = queryBuilder.ToString();

			MySqlQueryResult result = await Database.ExecuteQueryAsync(insertOrUpdateQuery);

			if (Config.GeneralSettings.LevelRanksCompatibility)
			{
				// ? STEAM_0:0:12345678 -> STEAM_1:0:12345678 just to match lvlranks as we can
				string lvlSteamID = steamid.SteamId2.Replace("STEAM_0", "STEAM_1");

				try
				{
					await Database.ExecuteNonQueryAsync($@"
						INSERT INTO `{Config.DatabaseSettings.LvLRanksTableName}`
						(`steam`, `name`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `lastconnect`)
						VALUES
						('{lvlSteamID}', '{escapedName}', {playerData.StatFields["kills"]}, {playerData.StatFields["deaths"]}, {playerData.StatFields["shoots"]}, {playerData.StatFields["hits_given"]}, {playerData.StatFields["headshots"]}, {playerData.StatFields["assists"]}, {playerData.StatFields["round_win"]}, {playerData.StatFields["round_lose"]}, {DateTimeOffset.UtcNow.ToUnixTimeSeconds()})
						ON DUPLICATE KEY UPDATE
						`name` = '{escapedName}',
						`kills` = {playerData.StatFields["kills"]},
						`deaths` = {playerData.StatFields["deaths"]},
						`shoots` = {playerData.StatFields["shoots"]},
						`hits` = {playerData.StatFields["hits_given"]},
						`headshots` = {playerData.StatFields["headshots"]},
						`assists` = {playerData.StatFields["assists"]},
						`round_win` = {playerData.StatFields["round_win"]},
						`round_lose` = {playerData.StatFields["round_lose"]},
						`lastconnect` = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()};
					");
				}
				catch(Exception ex)
				{
					Logger.LogError($"SavePlayerStatCacheAsync > LevelRanks Query error: {ex.Message}");
				}
			}

			if (!remove)
			{
				Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

				var allKeys = playerData.StatFields.Keys.ToList();

				foreach (string statField in allKeys)
				{
					NewStatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
				}

				statCache[slot].StatFields = NewStatFields;
			}
			else
			{
				statCache.Remove(slot);
			}
		}

		public void LoadAllPlayerCache()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			List<Task> loadTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV)
				.Select(player => LoadStatData(player.Slot, player.PlayerName, player.SteamID.ToString()))
				.ToList();

			Task.Run(async () =>
			{
				await Task.WhenAll(loadTasks);
			});
		}

		public void SaveAllPlayerCache(bool clear)
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			List<Task> saveTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV && statCache.ContainsPlayer(player))
				.Select(player => SavePlayerStatCacheAsync(player.Slot, player.PlayerName, new SteamID(player.SteamID), clear))
				.ToList();

			Task.Run(async () =>
			{
				await Task.WhenAll(saveTasks);

				if (clear)
					statCache.Clear();
			});
		}

		public void ModifyPlayerStats(CCSPlayerController player, string field, int amount)
		{
			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
				return;

			if (player.IsBot || player.IsHLTV)
				return;

			if (!statCache.ContainsPlayer(player))
				return;

			StatData playerData = statCache[player];
			playerData.StatFields[field] += amount;
		}
	}
}
