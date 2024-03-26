
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
	using System.Data;
	using MaxMind.GeoIP2;
	using MaxMind.GeoIP2.Exceptions;

	public class PlayerData
	{
		public required string PlayerName { get; set; }
		public required string SteamId { get; set; }
		public required string lvlSteamId { get; set; }
		public required PlayerCacheData cacheData { get; set; }
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
			List<CCSPlayerController> players = Utilities.GetPlayers()
				.Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && PlayerCache.Instance.ContainsPlayer(p))
				.ToList();

			foreach (CCSPlayerController player in players)
			{
				try
				{
					SteamID steamId = new SteamID(player.SteamID);

					if (!steamId.IsValid())
						continue;

					string playerSteamId = steamId.SteamId64.ToString();

					PlayerData data = new PlayerData
					{
						PlayerName = player.PlayerName,
						SteamId = playerSteamId,
						lvlSteamId = steamId.SteamId2.Replace("STEAM_0", "STEAM_1"),
						cacheData = PlayerCache.Instance.GetPlayerData(player)
					};

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
					if (playerData.cacheData.rankData != null)
						await ExecuteRankUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.cacheData.rankData);

					if (playerData.cacheData.statData != null)
						await ExecuteStatUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.cacheData.statData);

					if (playerData.cacheData.timeData != null)
						await ExecuteTimeUpdateAsync(playerData.PlayerName, playerData.SteamId, playerData.cacheData.timeData);

					if (Config.GeneralSettings.LevelRanksCompatibility)
						await ExecuteLvlRanksUpdateAsync(playerData.PlayerName, playerData.lvlSteamId, playerData.cacheData);
				}
			});
		}

		public void SavePlayerCache(CCSPlayerController player, bool remove)
		{
			PlayerCacheData cacheData = PlayerCache.Instance.GetPlayerData(player);

			string playerName = player.PlayerName;
			string lvlSteamId = new SteamID(player.SteamID).SteamId2.Replace("STEAM_0", "STEAM_1");
			string playerSteamId = player.SteamID.ToString();

			_ = SavePlayerDataAsync(playerName, playerSteamId, lvlSteamId, cacheData, remove);
		}

		private async Task SavePlayerDataAsync(string playerName, string steamId, string lvlSteamId, PlayerCacheData cacheData, bool remove)
		{
			await Database.Instance.ExecuteWithTransactionAsync(async (connection, transaction) =>
			{
				if (cacheData.rankData != null)
					await ExecuteRankUpdateAsync(playerName, steamId, cacheData.rankData);

				if (cacheData.statData != null)
					await ExecuteStatUpdateAsync(playerName, steamId, cacheData.statData);

				if (cacheData.timeData != null)
					await ExecuteTimeUpdateAsync(playerName, steamId, cacheData.timeData);

				if (Config.GeneralSettings.LevelRanksCompatibility)
					await ExecuteLvlRanksUpdateAsync(playerName, lvlSteamId, cacheData);
			});

			if (remove)
				PlayerCache.Instance.RemovePlayer(ulong.Parse(steamId));
		}

		private async Task ExecuteLvlRanksUpdateAsync(string playerName, string lvlSteamId, PlayerCacheData cacheData)
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
				new MySqlParameter("@kills", cacheData.statData?.StatFields["kills"] ?? 0),
				new MySqlParameter("@deaths", cacheData.statData?.StatFields["deaths"] ?? 0),
				new MySqlParameter("@shoots", cacheData.statData?.StatFields["shoots"] ?? 0),
				new MySqlParameter("@hits", cacheData.statData?.StatFields["hits_given"] ?? 0),
				new MySqlParameter("@headshots", cacheData.statData?.StatFields["headshots"] ?? 0),
				new MySqlParameter("@assists", cacheData.statData?.StatFields["assists"] ?? 0),
				new MySqlParameter("@roundWin", cacheData.statData?.StatFields["round_win"] ?? 0),
				new MySqlParameter("@roundLose", cacheData.statData?.StatFields["round_lose"] ?? 0),
				new MySqlParameter("@points", cacheData.rankData?.Points ?? 0),
				new MySqlParameter("@rank", cacheData.rankData?.Rank?.Id ?? -1),
				new MySqlParameter("@playtime", cacheData.timeData?.TimeFields["all"] ?? 0),
			};

			await Database.Instance.ExecuteNonQueryAsync(query, parameters);
		}

		private async Task ExecuteRankUpdateAsync(string playerName, string steamId, RankData rankData)
		{
			string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks` (`name`, `steam_id`, `rank`, `points`, `lastseen`)
                      VALUES (@playerName, @steamId, @rank, @points, CURRENT_TIMESTAMP)
                      ON DUPLICATE KEY UPDATE `name` = @playerName, `points` = @points, `lastseen` = CURRENT_TIMESTAMP;";

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

			string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, `lastseen`, {fieldsForInsert})
                      VALUES (@playerName, @steamId, CURRENT_TIMESTAMP, {valuesForInsert})
                      ON DUPLICATE KEY UPDATE `name` = @playerName, `lastseen` = CURRENT_TIMESTAMP, {onDuplicateKeyUpdate};";

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

			string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`name`, `steam_id`, `lastseen`, {fieldsForInsert})
                      VALUES (@playerName, @steamId, CURRENT_TIMESTAMP, {valuesForInsert})
                      ON DUPLICATE KEY UPDATE `name` = @playerName, `lastseen` = CURRENT_TIMESTAMP, {onDuplicateKeyUpdate};";

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
			string combinedQuery = $@"
					INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks` (`name`, `steam_id`, `rank`, `points`, `lastseen`)
					VALUES (
						@escapedName,
						@steamid,
						@noneRankName,
						@startPoints,
						CURRENT_TIMESTAMP
					)
					ON DUPLICATE KEY UPDATE
						`name` = @escapedName,
						`lastseen` = CURRENT_TIMESTAMP;

					INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, `lastseen`)
					VALUES (
						@escapedName,
						@steamid,
						CURRENT_TIMESTAMP
					)
					ON DUPLICATE KEY UPDATE
						`name` = @escapedName,
						`lastseen` = CURRENT_TIMESTAMP;

					INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`name`, `steam_id`, `lastseen`)
					VALUES (
						@escapedName,
						@steamid,
						CURRENT_TIMESTAMP
					)
					ON DUPLICATE KEY UPDATE
						`name` = @escapedName,
						`lastseen` = CURRENT_TIMESTAMP;

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

			ulong steamID = player.SteamID;

			MySqlParameter[] parameters = new MySqlParameter[]
			{
				new MySqlParameter("@escapedName", player.PlayerName),
				new MySqlParameter("@steamid", steamID),
				new MySqlParameter("@noneRankName", ModuleRank.GetNoneRank()?.Name ?? "none"),
				new MySqlParameter("@startPoints", Config.RankSettings.StartPoints)
			};

			_ = LoadPlayerCacheAsync(steamID, combinedQuery, parameters);
		}

		public async Task LoadPlayerCacheAsync(ulong steamID, string combinedQuery, MySqlParameter[] parameters)
		{
			try
			{
				using (MySqlCommand command = new MySqlCommand(combinedQuery))
				{
					DataTable dataTable = await Database.Instance.ExecuteReaderAsync(combinedQuery, parameters);

					if (dataTable.Rows.Count > 0)
					{
						foreach (DataRow row in dataTable.Rows)
						{
							LoadPlayerRowToCache(steamID, row);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"A problem occurred while loading single player cache: {ex.Message}");
			}
		}

		private void LoadAllPlayersCache()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers().Where(player => player?.IsValid == true && player.PlayerPawn?.IsValid == true && !player.IsBot && !player.IsHLTV && player.SteamID.ToString().Length == 17).ToList();

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

			try
			{
				_ = LoadAllPlayersCacheAsync(combinedQuery);
			}
			catch (Exception ex)
			{
				Logger.LogError($"LoadAllPlayersCache > {ex.Message}");
			}
		}

		public async Task LoadAllPlayersCacheAsync(string combinedQuery)
		{
			try
			{
				using (MySqlCommand command = new MySqlCommand(combinedQuery))
				{
					DataTable dataTable = await Database.Instance.ExecuteReaderAsync(command.CommandText);

					if (dataTable.Rows.Count > 0)
					{
						foreach (DataRow row in dataTable.Rows)
						{
							ulong steamID = Convert.ToUInt64(row["steam_id"]);

							if (steamID == 0)
								continue;

							LoadPlayerRowToCache(steamID, row);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"A problem occurred while loading all players cache: {ex.Message}");
			}
		}

		public void LoadPlayerRowToCache(ulong steamID, DataRow row)
		{
			/** ? Load Rank to Cache */
			RankData? rankData = null;

			if (Config.GeneralSettings.ModuleRanks)
			{
				int points = Convert.ToInt32(row["points"]);

				rankData = new RankData
				{
					Points = points,
					Rank = ModuleRank.GetPlayerRank(points),
					PlayedRound = false,
					RoundPoints = 0,
					HideAdminTag = false,
					MuteMessages = false
				};
			}

			/** ? Load Stat to Cache */
			StatData? statData = null;

			if (Config.GeneralSettings.ModuleStats)
			{
				Dictionary<string, int> NewStatFields = new Dictionary<string, int>();

				string[] statFieldNames = { "kills", "shoots", "firstblood", "deaths", "hits_given", "hits_taken", "headshots", "grenades", "mvp", "round_win", "round_lose", "game_win", "game_lose", "assists" };

				foreach (string statField in statFieldNames)
				{
					NewStatFields[statField] = Convert.ToInt32(row[statField]);
				}

				statData = new StatData
				{
					StatFields = NewStatFields,
				};
			}

			/** ? Load Time to Cache */
			TimeData? timeData = null;

			if (Config.GeneralSettings.ModuleTimes)
			{
				Dictionary<string, int> TimeFields = new Dictionary<string, int>();

				string[] timeFieldNames = { "all", "ct", "t", "spec", "alive", "dead" };

				foreach (string timeField in timeFieldNames)
				{
					TimeFields[timeField] = Convert.ToInt32(row[timeField]);
				}

				DateTime now = DateTime.UtcNow;

				timeData = new TimeData
				{
					TimeFields = TimeFields,
					Times = new Dictionary<string, DateTime>
					{
						{ "Connect", now },
						{ "Team", now },
						{ "Death", now }
					}
				};
			}

			PlayerCache.Instance.AddOrUpdatePlayer(steamID, new PlayerCacheData
			{
				rankData = rankData,
				statData = statData,
				timeData = timeData
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

		public async Task PurgeTableRows()
		{
			if (Config.GeneralSettings.TablePurgeDays <= 0)
				return;

			await Database.Instance.ExecuteWithTransactionAsync(async (connection, transaction) =>
			{
				MySqlParameter[] parameters = new MySqlParameter[]
				{
					new MySqlParameter("@days", Config.GeneralSettings.TablePurgeDays)
				};

				string query = $@"DELETE FROM `{this.Config.DatabaseSettings.TablePrefix}k4times` WHERE `lastseen` < NOW() - INTERVAL @days DAY;";
				await Database.Instance.ExecuteNonQueryAsync(query, parameters);

				query = $@"DELETE FROM `{this.Config.DatabaseSettings.TablePrefix}k4stats` WHERE `lastseen` < NOW() - INTERVAL @days DAY;";
				await Database.Instance.ExecuteNonQueryAsync(query, parameters);

				query = $@"DELETE FROM `{this.Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `lastseen` < NOW() - INTERVAL @days DAY;";
				await Database.Instance.ExecuteNonQueryAsync(query, parameters);

				if (Config.GeneralSettings.LevelRanksCompatibility)
				{
					query = $@"DELETE FROM `{Config.DatabaseSettings.LvLRanksTableName}` WHERE `lastconnect` < UNIX_TIMESTAMP(NOW() - INTERVAL @days DAY);";
					await Database.Instance.ExecuteNonQueryAsync(query, parameters);
				}
			});
		}

		public string GetPlayerCountryCode(CCSPlayerController player)
		{
			string? playerIp = player.IpAddress;

			if (playerIp == null)
				return "??";

			string[] parts = playerIp.Split(':');
			string realIP = parts.Length == 2 ? parts[0] : playerIp;

			using DatabaseReader reader = new DatabaseReader(Path.Combine(ModuleDirectory, "GeoLite2-Country.mmdb"));
			{
				try
				{
					MaxMind.GeoIP2.Responses.CountryResponse response = reader.Country(realIP);
					return response.Country.IsoCode ?? "??";
				}
				catch (AddressNotFoundException)
				{
					Console.WriteLine($"The address {realIP} is not in the database.");
					return "??";
				}
				catch (GeoIP2Exception ex)
				{
					Console.WriteLine($"Error: {ex.Message}");
					return "??";
				}
			}
		}
	}
}