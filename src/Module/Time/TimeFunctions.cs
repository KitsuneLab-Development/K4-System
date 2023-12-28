namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	using MySqlConnector;
	using Nexd.MySQL;
	using Microsoft.Extensions.Logging;
	using System.Text;

	public partial class ModuleTime : IModuleTime
	{
		public async Task LoadTimeData(int slot, string name, string steamid)
		{
			string escapedName = MySqlHelper.EscapeString(name);

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`name`, `steam_id`)
				VALUES (
					'{escapedName}',
					'{steamid}'
				)
				ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}';
			");

			MySqlQueryResult result = await Database.ExecuteQueryAsync($@"
				SELECT *
				FROM `{Config.DatabaseSettings.TablePrefix}k4times`
				WHERE `steam_id` = '{steamid}';
			");

			Dictionary<string, int> NewTimeFields = new Dictionary<string, int>();

			string[] timeFieldNames = { "all", "ct", "t", "spec", "alive", "dead" };

			foreach (var timeField in timeFieldNames)
			{
				NewTimeFields[timeField] = result.Rows > 0 ? result.Get<int>(0, timeField) : 0;
			}

			DateTime now = DateTime.UtcNow;

			TimeData playerData = new TimeData
			{
				TimeFields = NewTimeFields,
				Times = new Dictionary<string, DateTime>
				{
					{ "Connect", now },
					{ "Team", now },
					{ "Death", now }
				}
			};

			timeCache[slot] = playerData;
		}

		public void SavePlayerTimeCache(CCSPlayerController player, bool remove)
		{
			var savedSlot = player.Slot;
			var savedStat = timeCache[player];
			var savedName = player.PlayerName;
			var savedSteam = player.SteamID.ToString();

			Task.Run(async () =>
			{
				await SavePlayerTimeCacheAsync(savedSlot, savedStat, savedName, savedSteam, remove);
			});
		}

		public async Task SavePlayerTimeCacheAsync(int slot, TimeData playerData, string name, string steamid, bool remove)
		{
			if (!timeCache.ContainsKey(slot))
			{
				Logger.LogWarning($"SavePlayerTimeCache > Player is not loaded to the cache ({name})");
				return;
			}

			string escapedName = MySqlHelper.EscapeString(name);

			StringBuilder queryBuilder = new StringBuilder();
			queryBuilder.Append($@"
   				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`steam_id`, `name`");

			foreach (var field in playerData.TimeFields)
			{
				queryBuilder.Append($", `{field.Key}`");
			}

			queryBuilder.Append($@")
				VALUES ('{steamid}', '{escapedName}'");

			foreach (var field in playerData.TimeFields)
			{
				queryBuilder.Append($", {field.Value}");
			}

			queryBuilder.Append($@")
				ON DUPLICATE KEY UPDATE");

			// Here, we modify the loop to correctly handle the comma
			int fieldCount = playerData.TimeFields.Count;
			int i = 0;
			foreach (var field in playerData.TimeFields)
			{
				queryBuilder.Append($"`{field.Key}` = VALUES(`{field.Key}`)");
				if (++i < fieldCount)
				{
					queryBuilder.Append(", ");
				}
			}

			queryBuilder.Append(";");

			if (!remove)
			{
				queryBuilder.Append($@"

				SELECT * FROM `{Config.DatabaseSettings.TablePrefix}k4times`
				WHERE `steam_id` = '{steamid}';");
			}

			string insertOrUpdateQuery = queryBuilder.ToString();

			MySqlQueryResult result = await Database.ExecuteQueryAsync(insertOrUpdateQuery);

			if (Config.GeneralSettings.LevelRanksCompatibility)
			{
				await Database.ExecuteNonQueryAsync($@"
					INSERT INTO `lvl_base`
					(`steam`, `name`, `playtime`, `lastconnect`)
					VALUES
					('{steamid}', '{escapedName}', {playerData.TimeFields["all"]}, CURRENT_TIMESTAMP)
					ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}',
					`playtime` = (`playtime` + {playerData.TimeFields["all"]}),
					`lastconnect` = CURRENT_TIMESTAMP;
				");
			}

			if (!remove)
			{
				Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

				var allKeys = playerData.TimeFields.Keys.ToList();

				foreach (string statField in allKeys)
				{
					NewStatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
				}

				timeCache[slot].TimeFields = NewStatFields;
			}
			else
			{
				timeCache.Remove(slot);
			}
		}

		public void SaveAllPlayerCache(bool clear)
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			var saveTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV && timeCache.ContainsPlayer(player))
				.Select(player => SavePlayerTimeCacheAsync(player.Slot, timeCache[player], player.PlayerName, player.SteamID.ToString(), clear))
				.ToList();

			Task.Run(async () =>
			{
				await Task.WhenAll(saveTasks);
			});

			if (clear)
				timeCache.Clear();
		}

		public string GetFieldForTeam(CsTeam team)
		{
			switch (team)
			{
				case CsTeam.Terrorist:
					return "t";
				case CsTeam.CounterTerrorist:
					return "ct";
				default:
					return "spec";
			}
		}

		public string FormatPlaytime(int totalSeconds)
		{
			string[] units = { "y", "mo", "d", "h", "m", "s" };
			int[] values = { totalSeconds / 31536000, totalSeconds % 31536000 / 2592000, totalSeconds % 2592000 / 86400, totalSeconds % 86400 / 3600, totalSeconds % 3600 / 60, totalSeconds % 60 };

			StringBuilder formattedTime = new StringBuilder();

			bool addedValue = false;

			for (int i = 0; i < units.Length; i++)
			{
				if (values[i] > 0)
				{
					formattedTime.Append($"{values[i]}{units[i]}, ");
					addedValue = true;
				}
			}

			if (!addedValue)
			{
				formattedTime.Append("0s");
			}

			return formattedTime.ToString().TrimEnd(' ', ',');
		}
	}
}