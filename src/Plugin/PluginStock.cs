
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
	using CounterStrikeSharp.API.Modules.Entities;

	public class PlayerData
	{
		public required string PlayerName { get; set; }
		public required string SteamId { get; set; }
		public required string lvlSteamId { get; set; }
		public RankData? RankData { get; set; }
		public StatData? StatData { get; set; }
		public TimeData? TimeData { get; set; }
	}

	public sealed partial class Plugin : BasePlugin
	{
		public void AdjustDatabasePooling()
		{
			Database.Instance.AdjustPoolingBasedOnPlayerCount(Utilities.GetPlayers().Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV).Count());
		}

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
			List<CCSPlayerController> players = Utilities.GetPlayers()
				.Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
				.ToList();

			foreach (CCSPlayerController player in players)
			{
				try
				{
					if (!ulong.TryParse(player.SteamID.ToString(), out ulong steamIdValue))
					{
						Logger.LogError($"Invalid SteamID for {player.PlayerName} > {player.SteamID}");
						continue;
					}

					SteamID steamId = new SteamID(steamIdValue);

					if (!steamId.IsValid())
						continue;

					string playerSteamId = steamId.SteamId64.ToString();

					PlayerData data = new PlayerData
					{
						PlayerName = player.PlayerName,
						SteamId = playerSteamId,
						lvlSteamId = steamId.SteamId2.Replace("STEAM_0", "STEAM_1")
					};

					if (rankCache.ContainsKey(player.Slot))
						data.RankData = rankCache[player.Slot];

					if (statCache.ContainsKey(player.Slot))
						data.StatData = statCache[player.Slot];

					if (timeCache.ContainsKey(player.Slot))
						data.TimeData = timeCache[player.Slot];

					playersData.Add(data);
				}
				catch (Exception ex)
				{
					Logger.LogError($"PreparePlayersData > {player.PlayerName} > {ex.Message}");
				}
			}

			return playersData;
		}

		public void SaveAllPlayersCache()
		{
			List<PlayerData> playersData = PreparePlayersData();
			_ = SaveAllPlayersCacheAsync(playersData);
		}

		public async Task SaveAllPlayersCacheAsync(List<PlayerData> playersData)
		{
			await Database.Instance.ExecuteWithTransactionAsync(async (connection, transaction) =>
			{
				foreach (PlayerData playerData in playersData)
				{
					if (playerData.RankData != null)
						await ExecuteRankUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.RankData);

					if (playerData.StatData != null)
						await ExecuteStatUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.StatData);

					if (playerData.TimeData != null)
						await ExecuteTimeUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.TimeData);

					if (Config.GeneralSettings.LevelRanksCompatibility)
						await ExecuteLvlRanksUpdateAsync(playerData.PlayerName, playerData.lvlSteamId, playerData.RankData, playerData.StatData, playerData.TimeData);
				}
			});
		}

		public void SavePlayerCache(CCSPlayerController player)
		{
			string playerSteamId = player.SteamID.ToString();
			string playerName = player.PlayerName;

			RankData? rankData = rankCache.ContainsKey(player.Slot) ? rankCache[player.Slot] : null;
			StatData? statData = statCache.ContainsKey(player.Slot) ? statCache[player.Slot] : null;
			TimeData? timeData = timeCache.ContainsKey(player.Slot) ? timeCache[player.Slot] : null;

			string lvlSteamId = new SteamID(player.SteamID).SteamId2.Replace("STEAM_0", "STEAM_1");

			_ = SavePlayerDataAsync(playerName, playerSteamId, lvlSteamId, rankData, statData, timeData);
		}

		private async Task SavePlayerDataAsync(string playerName, string steamId, string lvlSteamId, RankData? rankData, StatData? statData, TimeData? timeData)
		{
			await Database.Instance.ExecuteWithTransactionAsync(async (connection, transaction) =>
			{
				if (rankData != null)
					await ExecuteRankUpdateAsync(playerName, steamId, rankData);

				if (statData != null)
					await ExecuteStatUpdateAsync(playerName, steamId, statData);

				if (timeData != null)
					await ExecuteTimeUpdateAsync(playerName, steamId, timeData);

				if (Config.GeneralSettings.LevelRanksCompatibility)
					await ExecuteLvlRanksUpdateAsync(playerName, lvlSteamId, rankData, statData, timeData);
			});
		}

		private async Task ExecuteLvlRanksUpdateAsync(string playerName, string lvlSteamId, RankData? rankData, StatData? statData, TimeData? timeData)
		{
			string query = $@"
				INSERT INTO `{Config.DatabaseSettings.LvLRanksTableName}`
				(`steam`, `name`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `lastconnect`, `value`, `rank`, `playtime`)
				VALUES
				(@steamid, @playerName, @kills, @deaths, @shoots, @hits, @headshots, @assists, @roundWin, @roundLose, {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}, @points, @rank, @playtime)
				ON DUPLICATE KEY UPDATE
				`name` = @playerName,
				`kills` = @kills,
				`deaths` = @deaths,
				`shoots` = @shoots,
				`hits` = @hits,
				`headshots` = @headshots,
				`assists` = @assists,
				`round_win` = @roundWin,
				`round_lose` = @roundLose,
				`lastconnect` = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()},
				`value` = @points,
				`rank` = @rank,
				`playtime` = @playtime;
			";

			MySqlParameter[] parameters = new MySqlParameter[]
			{
				new MySqlParameter("@playerName", playerName),
				new MySqlParameter("@steamId", lvlSteamId),
				new MySqlParameter("@kills", statData?.StatFields["kills"] ?? 0),
				new MySqlParameter("@deaths", statData?.StatFields["deaths"] ?? 0),
				new MySqlParameter("@shoots", statData?.StatFields["shoots"] ?? 0),
				new MySqlParameter("@hits", statData?.StatFields["hits_given"] ?? 0),
				new MySqlParameter("@headshots", statData?.StatFields["headshots"] ?? 0),
				new MySqlParameter("@assists", statData?.StatFields["assists"] ?? 0),
				new MySqlParameter("@roundWin", statData?.StatFields["round_win"] ?? 0),
				new MySqlParameter("@roundLose", statData?.StatFields["round_lose"] ?? 0),
				new MySqlParameter("@points", rankData?.Points ?? 0),
				new MySqlParameter("@rank", rankData?.Rank?.Id ?? -1),
				new MySqlParameter("@playtime", timeData?.TimeFields["all"] ?? 0),
			};

			await Database.Instance.ExecuteNonQueryAsync(query, parameters);
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

			MySqlParameter[] parameters = new MySqlParameter[]
			{
				new MySqlParameter("@escapedName", player.PlayerName),
				new MySqlParameter("@steamid", player.SteamID),
				new MySqlParameter("@noneRankName", ModuleRank.GetNoneRank()?.Name ?? "none"),
				new MySqlParameter("@startPoints", Config.RankSettings.StartPoints)
			};

			_ = LoadPlayerCacheAsync(slot, combinedQuery, parameters);
		}

		public async Task LoadPlayerCacheAsync(int slot, string combinedQuery, MySqlParameter[] parameters)
		{
			using (MySqlDataReader? reader = await Database.Instance.ExecuteReaderAsync(combinedQuery, parameters))
			{
				if (reader != null && reader.HasRows)
				{
					while (await reader.ReadAsync())
					{
						/** ? Load Rank to Cache */

						if (Config.GeneralSettings.ModuleRanks)
						{
							int points = reader.GetInt32("points");
							ModuleRank.LoadRankData(slot, points);
						}

						/** ? Load Stat to Cache */

						if (Config.GeneralSettings.ModuleStats)
						{
							Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

							string[] statFieldNames = { "kills", "shoots", "firstblood", "deaths", "hits_given", "hits_taken", "headshots", "grenades", "mvp", "round_win", "round_lose", "game_win", "game_lose", "assists" };

							foreach (string statField in statFieldNames)
							{
								NewStatFields[statField] = reader.GetInt32(statField);
							}

							ModuleStat.LoadStatData(slot, NewStatFields);
						}

						/** ? Load Time to Cache */

						if (Config.GeneralSettings.ModuleTimes)
						{
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
			}
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

			_ = LoadAllPlayersCacheAsync(combinedQuery, steamIdToSlot);
		}

		public async Task LoadAllPlayersCacheAsync(string combinedQuery, Dictionary<string, int> steamIdToSlot)
		{
			using (var reader = await Database.Instance.ExecuteReaderAsync(combinedQuery))
			{
				if (reader != null && reader.HasRows)
				{
					while (await reader.ReadAsync())
					{
						string steamId = reader["steam_id"].ToString()!;

						// A Slot információk lekérése a SteamID alapján a Dictionary-ből
						int slot = steamIdToSlot[steamId];

						/** ? Load Rank to Cache */

						if (Config.GeneralSettings.ModuleRanks)
						{
							int points = reader.GetInt32("points");
							ModuleRank.LoadRankData(slot, points);
						}

						/** ? Load Stat to Cache */

						if (Config.GeneralSettings.ModuleStats)
						{
							Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

							string[] statFieldNames = { "kills", "shoots", "firstblood", "deaths", "hits_given", "hits_taken", "headshots", "grenades", "mvp", "round_win", "round_lose", "game_win", "game_lose", "assists" };

							foreach (string statField in statFieldNames)
							{
								NewStatFields[statField] = reader.GetInt32(statField);
							}

							ModuleStat.LoadStatData(slot, NewStatFields);
						}

						/** ? Load Time to Cache */

						if (Config.GeneralSettings.ModuleTimes)
						{
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
			}
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