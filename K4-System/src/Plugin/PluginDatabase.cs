
using System.Data;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Dapper;
using K4System.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using static K4System.ModuleRank;
using static K4System.ModuleStat;
using static K4System.ModuleTime;

namespace K4System;

public sealed partial class Plugin : BasePlugin
{
	public MySqlConnection CreateConnection(PluginConfig config)
	{
		DatabaseSettings _settings = config.DatabaseSettings;

		MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = _settings.Host,
			UserID = _settings.Username,
			Password = _settings.Password,
			Database = _settings.Database,
			Port = (uint)_settings.Port,
			SslMode = Enum.Parse<MySqlSslMode>(_settings.Sslmode, true),
		};

		return new MySqlConnection(builder.ToString());
	}

	public async Task PurgeTableRowsAsync()
	{
		if (Config.GeneralSettings.TablePurgeDays <= 0)
			return;

		using (var connection = CreateConnection(Config))
		{
			await connection.OpenAsync();

			var parameters = new DynamicParameters();
			parameters.Add("@days", Config.GeneralSettings.TablePurgeDays);

			string query = $@"
					DELETE FROM `{Config.DatabaseSettings.TablePrefix}k4times` WHERE `lastseen` < NOW() - INTERVAL @days DAY AND `lastseen` != '0000-00-00';
					DELETE FROM `{Config.DatabaseSettings.TablePrefix}k4stats` WHERE `lastseen` < NOW() - INTERVAL @days DAY AND `lastseen` != '0000-00-00';
					DELETE FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `lastseen` < NOW() - INTERVAL @days DAY AND `lastseen` != '0000-00-00';
				";

			if (Config.GeneralSettings.LevelRanksCompatibility)
			{
				query += $@"
						DELETE FROM `{Config.DatabaseSettings.LvLRanksTableName}` WHERE `lastconnect` < UNIX_TIMESTAMP(NOW() - INTERVAL @days DAY);
					";
			}

			await connection.ExecuteAsync(query, parameters);
		}
	}

	public async Task SaveAllPlayersDataAsync()
	{
		using (var connection = CreateConnection(Config))
		{
			await connection.OpenAsync();

			using (var transaction = await connection.BeginTransactionAsync())
			{
				foreach (K4Player k4player in K4Players)
				{
					try
					{
						if (k4player.rankData != null)
							await ExecuteRankUpdateAsync(transaction, k4player);

						if (k4player.statData != null)
							await ExecuteStatUpdateAsync(transaction, k4player);

						if (k4player.timeData != null)
							await ExecuteTimeUpdateAsync(transaction, k4player);

						if (Config.GeneralSettings.LevelRanksCompatibility)
							await ExecuteLvlRanksUpdateAsync(transaction, k4player);

						await transaction.CommitAsync();
					}
					catch (Exception)
					{
						await transaction.RollbackAsync();
						throw;
					}
				}
			}
		}
	}

	public async Task SavePlayerDataAsync(K4Player k4player, bool remove)
	{
		using (var connection = CreateConnection(Config))
		{
			await connection.OpenAsync();

			using (var transaction = await connection.BeginTransactionAsync())
			{
				try
				{
					if (k4player.rankData != null)
						await ExecuteRankUpdateAsync(transaction, k4player);

					if (k4player.statData != null)
						await ExecuteStatUpdateAsync(transaction, k4player);

					if (k4player.timeData != null)
						await ExecuteTimeUpdateAsync(transaction, k4player);

					if (Config.GeneralSettings.LevelRanksCompatibility)
						await ExecuteLvlRanksUpdateAsync(transaction, k4player);

					await transaction.CommitAsync();
				}
				catch (Exception)
				{
					await transaction.RollbackAsync();
					throw;
				}
			}
		}

		if (remove)
			K4Players.Remove(k4player);
	}

	private async Task ExecuteLvlRanksUpdateAsync(MySqlTransaction transaction, K4Player k4player)
	{
		if (transaction.Connection == null)
			throw new InvalidOperationException("The transaction's connection is null.");

		string query = $@"INSERT INTO `{Config.DatabaseSettings.LvLRanksTableName}`
								(`steam`, `name`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `lastconnect`, `value`, `rank`, `playtime`)
								VALUES
								(@SteamId, @PlayerName, @Kills, @Deaths, @Shoots, @Hits, @Headshots, @Assists, @RoundWin, @RoundLose, UNIX_TIMESTAMP(), @Points, @Rank, @Playtime)
								ON DUPLICATE KEY UPDATE
								`name` = @PlayerName,
								`kills` = @Kills,
								`deaths` = @Deaths,
								`shoots` = @Shoots,
								`hits` = @Hits,
								`headshots` = @Headshots,
								`assists` = @Assists,
								`round_win` = @RoundWin,
								`round_lose` = @RoundLose,
								`lastconnect` = UNIX_TIMESTAMP(),
								`value` = @Points,
								`rank` = @Rank,
								`playtime` = @Playtime;";

		var parameters = new
		{
			SteamId = k4player.SteamID.ToString().Replace("STEAM_0", "STEAM_1"),
			k4player.PlayerName,
			Kills = k4player.statData?.StatFields["kills"] ?? 0,
			Deaths = k4player.statData?.StatFields["deaths"] ?? 0,
			Shoots = k4player.statData?.StatFields["shoots"] ?? 0,
			Hits = k4player.statData?.StatFields["hits_given"] ?? 0,
			Headshots = k4player.statData?.StatFields["headshots"] ?? 0,
			Assists = k4player.statData?.StatFields["assists"] ?? 0,
			RoundWin = k4player.statData?.StatFields["round_win"] ?? 0,
			RoundLose = k4player.statData?.StatFields["round_lose"] ?? 0,
			Points = k4player.rankData?.Points ?? 0,
			Rank = k4player.rankData?.Rank?.Id ?? -1,
			Playtime = k4player.timeData?.TimeFields["all"] ?? 0
		};

		await transaction.Connection.ExecuteAsync(query, parameters, transaction);
	}

	private async Task ExecuteRankUpdateAsync(MySqlTransaction transaction, K4Player k4player)
	{
		if (transaction.Connection == null)
			throw new InvalidOperationException("The transaction's connection is null.");

		string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks` (`name`, `steam_id`, `rank`, `points`, `lastseen`)
                        VALUES (@PlayerName, @SteamId, @Rank, @Points, CURRENT_TIMESTAMP)
                        ON DUPLICATE KEY UPDATE `name` = @PlayerName, `points` = @Points, `lastseen` = CURRENT_TIMESTAMP, `rank` = @Rank;";

		var parameters = new
		{
			k4player.PlayerName,
			SteamId = k4player.SteamID,
			Rank = k4player.rankData!.Rank.Name,
			k4player.rankData.Points
		};

		await transaction.Connection.ExecuteAsync(query, parameters, transaction);
	}

	private async Task ExecuteStatUpdateAsync(MySqlTransaction transaction, K4Player k4player)
	{
		if (transaction.Connection == null)
			throw new InvalidOperationException("The transaction's connection is null.");

		string fieldsForInsert = string.Join(", ", k4player.statData!.StatFields.Select(f => $"`{f.Key}`"));
		string valuesForInsert = string.Join(", ", k4player.statData.StatFields.Keys.Select(f => $"@{f}"));
		string onDuplicateKeyUpdate = string.Join(", ", k4player.statData.StatFields.Select(f => $"`{f.Key}` = @{f.Key}"));

		string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, `lastseen`, {fieldsForInsert})
                      VALUES (@PlayerName, @SteamId, CURRENT_TIMESTAMP, {valuesForInsert})
                      ON DUPLICATE KEY UPDATE `name` = @PlayerName, `lastseen` = CURRENT_TIMESTAMP, {onDuplicateKeyUpdate};";

		var dynamicParameters = new DynamicParameters();
		dynamicParameters.Add("@PlayerName", k4player.PlayerName);
		dynamicParameters.Add("@SteamId", k4player.SteamID);
		foreach (var field in k4player.statData.StatFields)
		{
			dynamicParameters.Add($"@{field.Key}", field.Value);
		}

		await transaction.Connection.ExecuteAsync(query, dynamicParameters, transaction);
	}


	private async Task ExecuteTimeUpdateAsync(MySqlTransaction transaction, K4Player k4player)
	{
		if (transaction.Connection == null)
			throw new InvalidOperationException("The transaction's connection is null.");

		string fieldsForInsert = string.Join(", ", k4player.timeData!.TimeFields.Select(f => $"`{f.Key}`"));
		string valuesForInsert = string.Join(", ", k4player.timeData.TimeFields.Select(f => $"@{f.Key}"));
		string onDuplicateKeyUpdate = string.Join(", ", k4player.timeData.TimeFields.Select(f => $"`{f.Key}` = @{f.Key}"));

		string query = $@"INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4times` (`name`, `steam_id`, `lastseen`, {fieldsForInsert})
                      VALUES (@PlayerName, @SteamId, CURRENT_TIMESTAMP, {valuesForInsert})
                      ON DUPLICATE KEY UPDATE `name` = @PlayerName, `lastseen` = CURRENT_TIMESTAMP, {onDuplicateKeyUpdate};";

		var dynamicParameters = new DynamicParameters();
		dynamicParameters.Add("@PlayerName", k4player.PlayerName);
		dynamicParameters.Add("@SteamId", k4player.SteamID);
		foreach (var field in k4player.timeData.TimeFields)
		{
			dynamicParameters.Add($"@{field.Key}", field.Value);
		}

		await transaction.Connection.ExecuteAsync(query, dynamicParameters, transaction);
	}

	public async Task LoadPlayerCacheAsync(K4Player k4player)
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

					INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4stats` (`name`, `steam_id`, `lastseen`, `kills`, `firstblood`, `deaths`, `assists`, `shoots`, `hits_taken`, `hits_given`, `headshots`, `grenades`, `mvp`, `round_win`, `round_lose`, `game_win`, `game_lose`, `rounds_overall`, `rounds_ct`, `rounds_t`, `bomb_planted`, `bomb_defused`, `hostage_rescued`, `hostage_killed`)
					VALUES (
						@escapedName,
						@steamid,
						CURRENT_TIMESTAMP,
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
						s.`firstblood`,
						s.`deaths`,
						s.`assists`,
						s.`shoots`,
						s.`hits_taken`,
						s.`hits_given`,
						s.`headshots`,
						s.`grenades`,
						s.`mvp`,
						s.`round_win`,
						s.`round_lose`,
						s.`game_win`,
						s.`game_lose`,
						s.`rounds_overall`,
						s.`rounds_ct`,
						s.`rounds_t`,
						s.`bomb_planted`,
						s.`bomb_defused`,
						s.`hostage_rescued`,
						s.`hostage_killed`,
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


		MySqlParameter[] parameters =
		[
			new MySqlParameter("@escapedName", k4player.PlayerName),
				new MySqlParameter("@steamid", k4player.SteamID),
				new MySqlParameter("@noneRankName", ModuleRank.GetNoneRank()?.Name ?? "none"),
				new MySqlParameter("@startPoints", Config.RankSettings.StartPoints)
		];

		try
		{
			using (var connection = CreateConnection(Config))
			{
				await connection.OpenAsync();
				var rows = await connection.QueryAsync(combinedQuery, parameters);

				foreach (var row in rows)
				{
					LoadPlayerRowToCache(k4player, row);
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
		List<CCSPlayerController> players = Utilities.GetPlayers().Where(player => player?.IsValid == true && player.PlayerPawn?.IsValid == true).ToList();

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
			Task.Run(() => LoadAllPlayersCacheAsync(combinedQuery));
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
			using (var connection = CreateConnection(Config))
			{
				var players = await connection.QueryAsync<dynamic>(combinedQuery);

				foreach (var k4player in K4Players)
				{
					await connection.OpenAsync();
					var rows = await connection.QueryAsync(combinedQuery);

					foreach (var row in rows)
					{
						LoadPlayerRowToCache(k4player, row);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogError($"A problem occurred while loading all players cache: {ex.Message}");
		}
	}

	public void LoadPlayerRowToCache(K4Player k4player, DataRow row)
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

			string[] statFieldNames = { "kills", "firstblood", "deaths", "assists", "shoots", "hits_taken", "hits_given", "headshots", "chest_hits", "stomach_hits", "left_arm_hits", "right_arm_hits", "left_leg_hits", "right_leg_hits", "neck_hits", "unused_hits", "gear_hits", "special_hits", "grenades", "mvp", "round_win", "round_lose", "game_win", "game_lose", "rounds_overall", "rounds_ct", "rounds_t", "bomb_planted", "bomb_defused", "hostage_rescued", "hostage_killed", "noscope_kill", "penetrated_kill", "thrusmoke_kill", "flashed_kill", "dominated_kill", "revenge_kill", "assist_flash" };

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

		k4player.rankData = rankData;
		k4player.statData = statData;
		k4player.timeData = timeData;

		K4Players.Add(k4player);
	}
}