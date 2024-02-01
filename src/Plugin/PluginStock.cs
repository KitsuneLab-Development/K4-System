
namespace K4System
{
	using System.Reflection;
	using MySqlConnector;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;

	using static K4System.ModuleRank;
	using static K4System.ModuleStat;
	using static K4System.ModuleTime;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Admin;

	public class PlayerData
	{
		public required string PlayerName { get; set; }
		public required string SteamId { get; set; }
		public RankData? RankData { get; set; }
		public StatData? StatData { get; set; }
		public TimeData? TimeData { get; set; }
	}

	public sealed partial class Plugin : BasePlugin
	{
		public CommandInfo.CommandCallback CallbackAnonymizer(Action<CCSPlayerController?, CommandInfo> action)
		{
			return new CommandInfo.CommandCallback(action);
		}

		public string ApplyPrefixColors(string msg)
		{
			string modifiedValue = msg;
			Type chatColorsType = typeof(ChatColors);

			foreach (FieldInfo field in chatColorsType.GetFields())
			{
				if (modifiedValue.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(field.Name, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			return modifiedValue;
		}

		public List<PlayerData> PreparePlayersData()
		{
			List<PlayerData> playersData = new List<PlayerData>();
			List<CCSPlayerController> players = Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV).ToList();

			foreach (CCSPlayerController player in players)
			{
				try
				{
					PlayerData data = new PlayerData
					{
						PlayerName = player.PlayerName,
						SteamId = player.SteamID.ToString()
					};

					if (rankCache.ContainsKey(player.Slot))
						data.RankData = rankCache[player.Slot];

					if (statCache.ContainsKey(player.Slot))
						data.StatData = statCache[player.Slot];

					if (timeCache.ContainsKey(player.Slot))
						data.TimeData = timeCache[player.Slot];

					playersData.Add(data);
				}
				catch (Exception ex) { Logger.LogError($"PreparePlayersData > {player.PlayerName} > {ex.Message}"); }

			}

			return playersData;
		}

		public void SaveAllPlayersCacheAsync(bool wait = false)
		{
			List<PlayerData> playersData = PreparePlayersData();

			Task.Run(async () =>
			{
				try
				{
					await Database.Instance.BeginTransactionAsync();

					foreach (PlayerData playerData in playersData)
					{
						if (playerData.RankData != null)
							await ExecuteRankUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.RankData);

						if (playerData.StatData != null)
							await ExecuteStatUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.StatData);

						if (playerData.TimeData != null)
							await ExecuteTimeUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.TimeData);
					}

					await Database.Instance.CommitTransactionAsync();
				}
				catch (Exception ex)
				{
					await Database.Instance.RollbackTransactionAsync();
					Logger.LogError($"Error saving all player caches: {ex.Message}");
				}
			});

			if (wait)
			{
				Task.WaitAll();
			}
		}

		public void SavePlayerCache(CCSPlayerController player, bool delete = false)
		{
			string steamId = player.SteamID.ToString();
			string playerName = player.PlayerName;

			RankData? rankData = rankCache.ContainsKey(player.Slot) ? rankCache[player.Slot] : null;
			StatData? statData = statCache.ContainsKey(player.Slot) ? statCache[player.Slot] : null;
			TimeData? timeData = timeCache.ContainsKey(player.Slot) ? timeCache[player.Slot] : null;

			if (delete)
			{
				Task.Run(() => SavePlayerDataAsync(playerName, steamId, rankData, statData, timeData)).Wait();

			}
			else
				Task.Run(() => SavePlayerDataAsync(playerName, steamId, rankData, statData, timeData));
		}

		private async Task SavePlayerDataAsync(string playerName, string steamId, RankData? rankData, StatData? statData, TimeData? timeData)
		{
			try
			{
				await Database.Instance.BeginTransactionAsync();

				if (rankData != null)
					await ExecuteRankUpdateAsync(playerName, steamId, rankData);

				if (statData != null)
					await ExecuteStatUpdateAsync(playerName, steamId, statData);

				if (timeData != null)
					await ExecuteTimeUpdateAsync(playerName, steamId, timeData);

				await Database.Instance.CommitTransactionAsync();
			}
			catch (Exception ex)
			{
				await Database.Instance.RollbackTransactionAsync();
				Logger.LogError($"SavePlayerDataAsync > {ex.Message}");
			}
		}

		private async Task ExecuteRankUpdateAsync(string playerName, string steamId, RankData rankData)
		{
			string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks` (`name`, `steam_id`, `rank`, `points`)
                      VALUES (@playerName, @steamId, @rank, @points)
                      ON DUPLICATE KEY UPDATE `name` = @playerName, `points` = @points;";

			MySqlParameter[] parameters = new MySqlParameter[]
			{
				new MySqlParameter("@playerName", playerName),
				new MySqlParameter("@steamId", steamId),
				new MySqlParameter("@rank", rankData.Rank.Name),
				new MySqlParameter("@points", rankData.Points)
			};

			await Database.Instance.ExecuteNonQueryAsync(query, parameters);
		}

		private async Task ExecuteStatUpdateAsync(string playerName, string steamId, StatData statData)
		{
			string fieldsForInsert = string.Join(", ", statData.StatFields.Select(f => $"`{f.Key}`"));
			string valuesForInsert = string.Join(", ", statData.StatFields.Select(f => $"@{f.Key}"));
			string onDuplicateKeyUpdate = string.Join(", ", statData.StatFields.Select(f => $"`{f.Key}` = @{f.Key}"));

			string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, {fieldsForInsert})
                      VALUES (@playerName, @steamId, {valuesForInsert})
                      ON DUPLICATE KEY UPDATE `name` = @playerName, {onDuplicateKeyUpdate};";

			List<MySqlParameter> parameters = new List<MySqlParameter>
			{
				new MySqlParameter("@playerName", playerName),
				new MySqlParameter("@steamId", steamId)
			};

			parameters.AddRange(statData.StatFields.Select(f => new MySqlParameter($"@{f.Key}", f.Value)));

			await Database.Instance.ExecuteNonQueryAsync(query, parameters.ToArray());
		}

		private async Task ExecuteTimeUpdateAsync(string playerName, string steamId, TimeData timeData)
		{
			string fieldsForInsert = string.Join(", ", timeData.TimeFields.Select(f => $"`{f.Key}`"));
			string valuesForInsert = string.Join(", ", timeData.TimeFields.Select(f => $"@{f.Key}"));
			string onDuplicateKeyUpdate = string.Join(", ", timeData.TimeFields.Select(f => $"`{f.Key}` = @{f.Key}"));

			string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`name`, `steam_id`, {fieldsForInsert})
                      VALUES (@playerName, @steamId, {valuesForInsert})
                      ON DUPLICATE KEY UPDATE `name` = @playerName, {onDuplicateKeyUpdate};";

			List<MySqlParameter> parameters = new List<MySqlParameter>
			{
				new MySqlParameter("@playerName", playerName),
				new MySqlParameter("@steamId", steamId)
			};

			parameters.AddRange(timeData.TimeFields.Select(f => new MySqlParameter($"@{f.Key}", f.Value)));

			await Database.Instance.ExecuteNonQueryAsync(query, parameters.ToArray());
		}

		private void LoadPlayerCache(CCSPlayerController player)
		{
			int slot = player.Slot;
			string steamId = player.SteamID.ToString();

			string combinedQuery = $@"
					INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks` (`name`, `steam_id`, `rank`, `points`)
					VALUES (
						@escapedName,
						@steamid,
						@noneRankName,
						@startPoints
					)
					ON DUPLICATE KEY UPDATE
						`name` = @escapedName;

					INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, `lastseen`)
					VALUES (
						@escapedName,
						@steamid,
						CURRENT_TIMESTAMP
					)
					ON DUPLICATE KEY UPDATE
						`name` = @escapedName,
						`lastseen` = CURRENT_TIMESTAMP;

					INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`name`, `steam_id`)
					VALUES (
						@escapedName,
						@steamid
					)
					ON DUPLICATE KEY UPDATE
						`name` = @escapedName;

					SELECT
						r.`points`,
						s.`kills`,
						s.`shoots`,
						s.`firstblood`,
						s.`deaths`,
						s.`hits_given`,
						s.`hits_taken`,
						s.`headshots`,
						s.`grenades`,
						s.`mvp`,
						s.`round_win`,
						s.`round_lose`,
						s.`game_win`,
						s.`game_lose`,
						s.`assists`,
						t.`all`,
						t.`ct`,
						t.`t`,
						t.`spec`,
						t.`alive`,
						t.`dead`
					FROM
						`{Config.DatabaseSettings.TablePrefix}k4ranks` AS r
					LEFT JOIN
						`{Config.DatabaseSettings.TablePrefix}k4stats` AS s ON r.`steam_id` = s.`steam_id`
					LEFT JOIN
						`{Config.DatabaseSettings.TablePrefix}k4times` AS t ON r.`steam_id` = t.`steam_id`
					WHERE
						r.`steam_id` = @steamid;
				";

			var parameters = new MySqlParameter[]
			{
					new MySqlParameter("@escapedName", player.PlayerName),
					new MySqlParameter("@steamid", player.SteamID),
					new MySqlParameter("@noneRankName", ModuleRank.GetNoneRank().Name),
					new MySqlParameter("@startPoints", Config.RankSettings.StartPoints)
			};

			Task.Run(async () =>
			{
				using (var reader = await Database.Instance.ExecuteReaderAsync(combinedQuery, parameters))
				{
					if (reader != null && reader.HasRows)
					{
						while (reader.Read())
						{
							/** ? Load Rank to Cache */

							int points = reader.GetInt32("points");
							ModuleRank.LoadRankData(slot, points);

							/** ? Load Stat to Cache */

							Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

							string[] statFieldNames = { "kills", "shoots", "firstblood", "deaths", "hits_given", "hits_taken", "headshots", "grenades", "mvp", "round_win", "round_lose", "game_win", "game_lose", "assists" };

							foreach (string statField in statFieldNames)
							{
								NewStatFields[statField] = reader.GetInt32(statField);
							}

							ModuleStat.LoadStatData(slot, NewStatFields);

							/** ? Load Time to Cache */

							Dictionary<string, int> TimeFields = new Dictionary<string, int>();

							string[] timeFieldNames = { "all", "ct", "t", "spec", "alive", "dead" };

							foreach (string timeField in timeFieldNames)
							{
								TimeFields[timeField] = reader.GetInt32(timeField);
							}

							ModuleTime.LoadTimeData(slot, TimeFields);
						}
					}
				}
			});
		}

		private void LoadAllPlayersCache()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV).ToList();

			Dictionary<string, int> steamIdToSlot = players.ToDictionary(player => player.SteamID.ToString(), player => player.Slot);

			if (players.Count == 0)
				return;

			string combinedQuery = $@"SELECT
					r.`steam_id`,
					r.`points`,
					s.`kills`,
					s.`shoots`,
					s.`firstblood`,
					s.`deaths`,
					s.`hits_given`,
					s.`hits_taken`,
					s.`headshots`,
					s.`grenades`,
					s.`mvp`,
					s.`round_win`,
					s.`round_lose`,
					s.`game_win`,
					s.`game_lose`,
					s.`assists`,
					t.`all`,
					t.`ct`,
					t.`t`,
					t.`spec`,
					t.`alive`,
					t.`dead`
				FROM
					`{Config.DatabaseSettings.TablePrefix}k4ranks` AS r
				LEFT JOIN
					`{Config.DatabaseSettings.TablePrefix}k4stats` AS s ON r.`steam_id` = s.`steam_id`
				LEFT JOIN
					`{Config.DatabaseSettings.TablePrefix}k4times` AS t ON r.`steam_id` = t.`steam_id`
				WHERE
					r.`steam_id` IN (" + string.Join(",", players.Select(player => $"'{player.SteamID}'")) + ");";

			Task.Run(async () =>
			{
				using (var reader = await Database.Instance.ExecuteReaderAsync(combinedQuery))
				{
					if (reader != null && reader.HasRows)
					{
						while (reader.Read())
						{
							string steamId = reader["steam_id"].ToString()!;

							// A Slot információk lekérése a SteamID alapján a Dictionary-ből
							int slot = steamIdToSlot[steamId];

							/** ? Load Rank to Cache */

							int points = reader.GetInt32("points");
							ModuleRank.LoadRankData(slot, points);

							/** ? Load Stat to Cache */

							Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

							string[] statFieldNames = { "kills", "shoots", "firstblood", "deaths", "hits_given", "hits_taken", "headshots", "grenades", "mvp", "round_win", "round_lose", "game_win", "game_lose", "assists" };

							foreach (string statField in statFieldNames)
							{
								NewStatFields[statField] = reader.GetInt32(statField);
							}

							ModuleStat.LoadStatData(slot, NewStatFields);

							/** ? Load Time to Cache */

							Dictionary<string, int> TimeFields = new Dictionary<string, int>();

							string[] timeFieldNames = { "all", "ct", "t", "spec", "alive", "dead" };

							foreach (string timeField in timeFieldNames)
							{
								TimeFields[timeField] = reader.GetInt32(timeField);
							}

							ModuleTime.LoadTimeData(slot, TimeFields);
						}
					}
					else
					{
						Console.WriteLine("LoadAllPlayersCache > No rows found.");
					}
				}
			});
		}

		public bool CommandHelper(CCSPlayerController? player, CommandInfo info, CommandUsage usage, int argCount = 0, string? help = null, string? permission = null)
		{
			switch (usage)
			{
				case CommandUsage.CLIENT_ONLY:
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
					{
						info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandclientonly"]}");
						return false;
					}
					break;
				case CommandUsage.SERVER_ONLY:
					if (player != null)
					{
						info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandserveronly"]}");
						return false;
					}
					break;
				case CommandUsage.CLIENT_AND_SERVER:
					if (!(player == null || (player != null && player.IsValid && player.PlayerPawn.Value != null)))
						return false;
					break;
			}

			if (permission != null && permission.Length > 0)
			{
				if (player != null && !AdminManager.PlayerHasPermissions(player, permission))
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandnoperm"]}");
					return false;
				}
			}

			if (argCount > 0 && help != null)
			{
				int checkArgCount = argCount + 1;
				if (info.ArgCount < checkArgCount)
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandhelp", info.ArgByIndex(0), help]}");
					return false;
				}
			}

			return true;
		}
	}
}