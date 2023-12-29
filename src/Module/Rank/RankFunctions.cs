namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;

	using MySqlConnector;
	using Nexd.MySQL;
	using Microsoft.Extensions.Logging;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Entities;

	public partial class ModuleRank : IModuleRank
	{
		public CCSGameRules GameRules()
		{
			if (globalGameRules is null)
				globalGameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;

			return globalGameRules;
		}

		public bool IsPointsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return (!GameRules().WarmupPeriod || Config.RankSettings.WarmupPoints) && (Config.RankSettings.MinPlayers <= notBots);
		}

		public async Task LoadRankData(int slot, string name, string steamid)
		{
			string escapedName = MySqlHelper.EscapeString(name);

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks` (`name`, `steam_id`, `rank`)
				VALUES (
					'{escapedName}',
					'{steamid}',
					'{noneRank!.Name}'
				)
				ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}';
			");

			MySqlQueryResult result = await Database.ExecuteQueryAsync($@"
				SELECT `points`
				FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`
				WHERE `steam_id` = '{steamid}';
			");

			int points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

			RankData playerData = new RankData
			{
				Points = points,
				Rank = GetPlayerRank(points),
				PlayedRound = false,
				RoundPoints = 0
			};

			rankCache[slot] = playerData;
		}

		public Rank GetPlayerRank(int points)
		{
			return rankDictionary.LastOrDefault(kv => points >= kv.Value.Point).Value ?? noneRank!;
		}

		public void ModifyPlayerPoints(CCSPlayerController player, int amount, string reason)
		{
			if (!IsPointsAllowed())
				return;

			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				Logger.LogWarning("ModifyPlayerPoints > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"ModifyPlayerPoints > Player controller is BOT or HLTV");
				return;
			}

			if (!rankCache.ContainsPlayer(player))
			{
				Logger.LogWarning($"ModifyPlayerPoints > Player is not loaded to the cache ({player.PlayerName})");
				return;
			}

			RankData playerData = rankCache[player];

			if (Config.RankSettings.RoundEndPoints)
				playerData.RoundPoints += amount;

			if (amount == 0)
				return;

			if (amount > 0 && AdminManager.PlayerHasPermissions(player, "@k4system/vip/points-multiplier"))
			{
				amount = (int)Math.Round(amount * Config.RankSettings.VipMultiplier);
			}

			Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

			int oldPoints = playerData.Points;
			Server.NextFrame(() =>
			{
				if (!Config.RankSettings.RoundEndPoints)
				{
					if (amount > 0)
					{
						player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.gain", oldPoints, amount, plugin.Localizer[reason]]}");
					}
					else if (amount < 0)
					{
						player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.loss", oldPoints, Math.Abs(amount), plugin.Localizer[reason]]}");
					}
				}
			});

			playerData.Points += amount;
			playerData.RoundPoints += amount;

			if (playerData.Points < 0)
				playerData.Points = 0;

			if (Config.RankSettings.ScoreboardScoreSync)
				player.Score = playerData.Points;

			Rank newRank = GetPlayerRank(playerData.Points);

			if (playerData.Rank.Name != newRank.Name)
			{
				player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer[playerData.Rank.Point > newRank.Point ? "k4.ranks.demote" : "k4.ranks.promote", newRank.Color, newRank.Name]}");

				if (playerData.Rank.Permissions != null && playerData.Rank.Permissions.Count > 0)
				{
					foreach (Permission permission in playerData.Rank.Permissions)
					{
						AdminManager.RemovePlayerPermissions(Utilities.GetPlayerFromSlot(player.Slot), permission.PermissionName);
					}
				}

				if (newRank.Permissions != null)
				{
					foreach (Permission permission in newRank.Permissions)
					{
						AdminManager.AddPlayerPermissions(Utilities.GetPlayerFromSlot(player.Slot), permission.PermissionName);
					}
				}

				playerData.Rank = newRank;
			}

			if (Config.RankSettings.ScoreboardRanks)
			{
				string tag = playerData.Rank.Tag ?? $"[{playerData.Rank.Name}]";
				SetPlayerClanTag(player, tag);
			}
		}

		public void SavePlayerRankCache(CCSPlayerController player, bool remove)
		{
			var savedSlot = player.Slot;
			var savedRank = rankCache[player];
			var savedName = player.PlayerName;

			SteamID steamID = new SteamID(player.SteamID);

			Task.Run(async () =>
			{
				await SavePlayerRankCacheAsync(savedSlot, savedRank, savedName, steamID, remove);
			});
		}

		public async Task SavePlayerRankCacheAsync(int slot, RankData playerData, string name, SteamID steamid, bool remove)
		{
			if (!rankCache.ContainsKey(slot))
			{
				Logger.LogWarning($"SavePlayerRankCache > Player is not loaded to the cache ({name})");
				return;
			}

			string escapedName = MySqlHelper.EscapeString(name);

			int setPoints = playerData.RoundPoints;

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks`
				(`steam_id`, `name`, `rank`, `points`)
				VALUES
				('{steamid.SteamId64}', '{escapedName}', '{playerData.Rank.Name}',
				CASE
					WHEN (`points` + {setPoints}) < 0 THEN 0
					ELSE (`points` + {setPoints})
				END)
				ON DUPLICATE KEY UPDATE
				`name` = '{escapedName}',
				`rank` = '{playerData.Rank.Name}',
				`points` =
				CASE
					WHEN (`points` + {setPoints}) < 0 THEN 0
					ELSE (`points` + {setPoints})
				END;
			");

			if (Config.GeneralSettings.LevelRanksCompatibility)
			{
				// ? STEAM_0:0:12345678 -> STEAM_1:0:12345678 just to match lvlranks as we can
				string lvlSteamID = steamid.SteamId2.Replace("STEAM_0", "STEAM_1");

				await Database.ExecuteNonQueryAsync($@"
					INSERT INTO `lvl_base`
					(`steam`, `name`, `rank`, `lastconnect`, `value`)
					VALUES
					('{lvlSteamID}', '{escapedName}', '{playerData.Rank.Id}', CURRENT_TIMESTAMP,
					CASE
						WHEN (`value` + {setPoints}) < 0 THEN 0
						ELSE (`value` + {setPoints})
					END)
					ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}',
					`rank` = '{playerData.Rank.Id}',
					`lastconnect` = CURRENT_TIMESTAMP,
					`value` =
					CASE
						WHEN (`value` + {setPoints}) < 0 THEN 0
						ELSE (`value` + {setPoints})
					END;
				");
			}

			if (!remove)
			{
				MySqlQueryResult result = await Database.ExecuteQueryAsync($@"
					SELECT `points`
					FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`
					WHERE `steam_id` = '{steamid.SteamId64}';
				");

				playerData.Points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

				playerData.RoundPoints = 0;
				playerData.Rank = GetPlayerRank(playerData.Points);
			}
			else
			{
				rankCache.Remove(slot);
			}
		}

		public void LoadAllPlayerCache()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			var loadTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV)
				.Select(player => LoadRankData(player.Slot, player.PlayerName, player.SteamID.ToString()))
				.ToList();

			Task.Run(async () =>
			{
				await Task.WhenAll(loadTasks);
			});
		}

		public void SaveAllPlayerCache(bool clear)
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			var saveTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV && rankCache.ContainsPlayer(player))
				.Select(player => SavePlayerRankCacheAsync(player.Slot, rankCache[player], player.PlayerName, new SteamID(player.SteamID), clear))
				.ToList();

			Task.Run(async () =>
			{
				await Task.WhenAll(saveTasks);
			});

			if (clear)
				rankCache.Clear();
		}

		public async Task<(int playerPlace, int totalPlayers)> GetPlayerPlaceAndCount(string steamID)
		{
			MySqlQueryResult result = await Database.Table($"{Config.DatabaseSettings.TablePrefix}k4ranks")
				.ExecuteQueryAsync($"SELECT (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `points` > (SELECT `points` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `steam_id` = '{steamID}')) AS playerCount, COUNT(*) AS totalPlayers FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`;")!;

			if (result.Count > 0)
			{
				int playersWithMorePoints = result.Get<int>(0, "playerCount");
				int totalPlayers = result.Get<int>(0, "totalPlayers");

				return (playersWithMorePoints + 1, totalPlayers);
			}

			return (0, 0);
		}

		public int CalculateDynamicPoints(CCSPlayerController from, CCSPlayerController to, int amount)
		{
			if (!Config.RankSettings.DynamicDeathPoints || to.IsBot || from.IsBot || rankCache[to].Points <= 0 || rankCache[from].Points <= 0)
			{
				return amount;
			}

			double pointsRatio = Math.Clamp(rankCache[to].Points / rankCache[from].Points, Config.RankSettings.DynamicDeathPointsMinMultiplier, Config.RankSettings.DynamicDeathPointsMaxMultiplier);
			double result = pointsRatio * amount;
			return (int)Math.Round(result);
		}

		public void SetPlayerClanTag(CCSPlayerController player, string tag)
		{
			Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

			player.Clan = tag;

			// ? It just works...

			plugin.AddTimer(0.2f, () =>
			{
				Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
				Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
			});
		}
	}
}