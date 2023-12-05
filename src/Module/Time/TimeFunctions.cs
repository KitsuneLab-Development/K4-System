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
		public async Task LoadTimeData(CCSPlayerController player)
		{
			if (player is null || !player.IsValid)
			{
				Logger.LogWarning("LoadTimeData > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"LoadTimeData > Player controller is BOT or HLTV");
				return;
			}

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);
			string steamID = player.SteamID.ToString();

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`name`, `steam_id`)
				VALUES (
					'{escapedName}',
					'{steamID}'
				)
				ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}';
			");

			MySqlQueryResult result = await Database.ExecuteQueryAsync($@"
				SELECT *
				FROM `{Config.DatabaseSettings.TablePrefix}k4times`
				WHERE `steam_id` = '{steamID}';
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

			timeCache[player] = playerData;
		}

		public async Task SavePlayerTimeCache(CCSPlayerController player, bool remove)
		{
			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				Logger.LogWarning("SavePlayerTimeCache > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"SavePlayerTimeCache > Player controller is BOT or HLTV");
				return;
			}

			if (!timeCache.ContainsPlayer(player))
			{
				Logger.LogWarning($"SavePlayerTimeCache > Player is not loaded to the cache ({player.PlayerName})");
				return;
			}

			TimeData playerData = timeCache[player];

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);
			string steamID = player.SteamID.ToString();

			StringBuilder queryBuilder = new StringBuilder();
			queryBuilder.Append($@"
   				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`steam_id`, `name`");

			foreach (var field in playerData.TimeFields)
			{
				queryBuilder.Append($", `{field.Key}`");
			}

			queryBuilder.Append($@")
				VALUES ('{steamID}', '{escapedName}'");

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
				WHERE `steam_id` = '{steamID}';");
			}

			string insertOrUpdateQuery = queryBuilder.ToString();

			MySqlQueryResult result = await Database.ExecuteQueryAsync(insertOrUpdateQuery);

			if (!remove)
			{
				Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

				var allKeys = playerData.TimeFields.Keys.ToList();

				foreach (string statField in allKeys)
				{
					NewStatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
				}

				timeCache[player].TimeFields = NewStatFields;
			}
			else
			{
				timeCache.RemovePlayer(player);
			}
		}

		public async Task SaveAllPlayerCache(bool clear)
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			var saveTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV && timeCache.ContainsPlayer(player))
				.Select(player => SavePlayerTimeCache(player, clear))
				.ToList();

			await Task.WhenAll(saveTasks);

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