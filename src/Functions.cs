using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Nexd.MySQL;
using System.Reflection;
using MySqlConnector;
using System.Text;
using CounterStrikeSharp.API.Modules.Admin;

namespace K4ryuuSystem
{
	public partial class K4System
	{
		public void LoadRanksFromConfig()
		{
			string ranksFilePath = Path.Join(ModuleDirectory, "ranks.jsonc");

			// Default ranks content with comments
			string defaultRanksContent = @"{
	""None"": {
		""Exp"": -1, // Whatever you set to -1 is the default rank
		""Color"": ""Default""
	},
	""Silver"": {
		""Tag"": ""S"", // Clan tag (scoreboard) of the rank. If not set, it uses the key instead, which is currently ""Silver""
		""Exp"": 250, // From this amount of experience, the player is Silver
		""Color"": ""LightBlue"" // Color code for the rank. Find color names here: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Utils/ChatColors.cs
	},
	""Gold"": {
		""Tag"": ""G"",
		""Exp"": 1000,
		""Color"": ""Red""
	}
	// You can add as many as you want
}";

			if (!File.Exists(ranksFilePath))
				File.WriteAllText(ranksFilePath, defaultRanksContent);

			try
			{
				using (FileStream fs = new FileStream(ranksFilePath, FileMode.Open, FileAccess.Read))
				using (StreamReader sr = new StreamReader(fs))
				{
					string jsonContent = Regex.Replace(sr.ReadToEnd(), @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
					ranks = JsonConvert.DeserializeObject<Dictionary<string, Rank>>(jsonContent)!;

					Rank? searchNoneeRank = ranks.Values.FirstOrDefault(rank => rank.Exp == -1);

					if (searchNoneeRank == null)
					{
						noneRank = "None";

						Log("Default rank is not set. You can set by creating a rank with 0 exp.");
					}
					else
						noneRank = ranks.FirstOrDefault(pair => pair.Value == searchNoneeRank).Key;
				}
			}
			catch (Exception ex)
			{
				Log("An error occurred: " + ex.Message, LogLevel.Error);
			}
		}

		public async void PrintTopXPlayers(CCSPlayerController player, int number)
		{
			MySqlQueryResult result = await MySql!.Table($"{TablePrefix}k4ranks").ExecuteQueryAsync($"SELECT `points`, `name` FROM `k4ranks` ORDER BY `points` DESC LIMIT {number};");

			if (result.Count > 0)
			{
				player!.PrintToChat($" {Config.GeneralSettings.Prefix} Top 5 Players:");

				for (int i = 0; i < result.Count; i++)
				{
					int pointChcek = result.Get<int>(i, "points");
					string playerRank = noneRank;

					foreach (var kvp in ranks)
					{
						string level = kvp.Key;
						Rank rank = kvp.Value;

						if (pointChcek >= rank.Exp)
						{
							playerRank = level;
						}
						else
							break;
					}
					player.PrintToChat($" {ChatColors.Gold}{i + 1}. {ChatColors.Blue}[{playerRank}] {ChatColors.Gold}{result.Get<string>(i, "name")} - {ChatColors.Blue}{result.Get<int>(i, "points")} points");
				}
			}
			else
			{
				player!.PrintToChat($" {Config.GeneralSettings.Prefix} No players found in the top {number}.");
			}
		}


		public void LoadPlayerData(CCSPlayerController player)
		{
			User newUser = new User
			{
				Points = 0,
				Rank = noneRank,
				RankPoints = -1
			};

			PlayerSummaries[player] = newUser;

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			MySqlQueryValue values = new MySqlQueryValue()
										.Add("name", escapedName)
										.Add("steam_id", player.SteamID.ToString());

			MySql!.Table($"{TablePrefix}k4times").InsertIfNotExist(values, $"`name` = '{escapedName}'");
			MySql!.Table($"{TablePrefix}k4stats").InsertIfNotExist(values, $"`name` = '{escapedName}'");
			MySql!.Table($"{TablePrefix}k4ranks").InsertIfNotExist(values.Add("`rank`", MySqlHelper.EscapeString(noneRank)), $"`name` = '{escapedName}'");


			if (Config.GeneralSettings.ModuleTimes)
			{
				MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4times").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select();

				string[] timeFieldNames = { "all", "ct", "t", "spec", "alive", "dead" };

				foreach (var timeField in timeFieldNames)
				{
					PlayerSummaries[player].TimeFields[timeField] = result.Rows > 0 ? result.Get<int>(0, timeField) : 0;
				}

				DateTime now = DateTime.UtcNow;
				PlayerSummaries[player].Times["Connect"] = PlayerSummaries[player].Times["Team"] = PlayerSummaries[player].Times["Death"] = now;
			}

			if (Config.GeneralSettings.ModuleStats)
			{
				MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4stats").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select();

				string[] statFieldNames = { "kills", "deaths", "hits", "headshots", "grenades", "mvp", "round_win", "round_lose" };

				foreach (var statField in statFieldNames)
				{
					PlayerSummaries[player].StatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
				}
			}

			if (Config.GeneralSettings.ModuleRanks)
			{
				newUser.RankObject = ranks.ContainsKey(noneRank) ? ranks[noneRank] : new Rank
				{
					Exp = -1,
					Color = $"{ChatColors.Default}"
				};

				MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4ranks").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select("points");

				PlayerSummaries[player].Points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

				if (Config.RankSettings.ScoreboardScoreSync)
					player.Score = PlayerSummaries[player].Points;

				string newRank = noneRank;
				string clanTag = string.Empty;
				Rank? setRank = null;

				foreach (var kvp in ranks)
				{
					string level = kvp.Key;
					Rank rank = kvp.Value;

					if (PlayerSummaries[player].Points >= rank.Exp)
					{
						setRank = rank;
						newRank = level;
						clanTag = rank.Tag ?? level;
					}
					else
						break;
				}

				if (setRank == null)
					return;

				if (Config.RankSettings.ScoreboardRanks)
					player.Clan = $"{clanTag ?? newRank}";

				PlayerSummaries[player].Rank = newRank;
				PlayerSummaries[player].RankPoints = setRank.Exp;

				string modifiedValue = setRank.Color;
				foreach (FieldInfo field in typeof(ChatColors).GetFields())
				{
					string pattern = $"{field.Name}";
					if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
					{
						modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
					}
				}

				PlayerSummaries[player].RankObject!.Color = modifiedValue;
			}
		}

		private (int playerPlace, int totalPlayers) GetPlayerPlaceAndCount(string playerName)
		{
			MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4ranks").ExecuteQuery($"SELECT (SELECT COUNT(*) FROM `k4ranks` WHERE `points` > (SELECT `points` FROM `k4ranks` WHERE `name` = '{playerName}')) AS playerCount, COUNT(*) AS totalPlayers FROM `k4ranks`")!;

			if (result.Count > 0)
			{
				int playersWithMorePoints = result.Get<int>(0, "playerCount");
				int totalPlayers = result.Get<int>(0, "totalPlayers");

				return (playersWithMorePoints + 1, totalPlayers);
			}

			return (0, 0);
		}

		public void ModifyClientPoints(CCSPlayerController player, CHANGE_MODE mode, int amount, string reason)
		{
			if (!player.IsValidPlayer())
				return;

			if (!IsPointsAllowed() || amount == 0)
				return;

			if (!PlayerSummaries.ContainsPlayer(player))
				LoadPlayerData(player);

			if (AdminManager.PlayerHasPermissions(player, "@k4system/vip/points-multiplier"))
			{
				amount = (int)Math.Round(amount * Config.RankSettings.VipMultiplier);
			}

			switch (mode)
			{
				case CHANGE_MODE.SET:
					{
						player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Gold}{PlayerSummaries[player].Points} [={amount} {reason}]");
						PlayerSummaries[player].Points = amount;
						break;
					}
				case CHANGE_MODE.GIVE:
					{
						player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[player].Points} [+{amount} {reason}]");
						PlayerSummaries[player].Points += amount;
						break;
					}
				case CHANGE_MODE.REMOVE:
					{
						player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Red}{PlayerSummaries[player].Points} [-{amount} {reason}]");
						PlayerSummaries[player].Points -= amount;

						if (PlayerSummaries[player].Points < 0)
							PlayerSummaries[player].Points = 0;

						break;
					}
				default:
					{
						Log($"Invalid operation at the point modification function: {mode}", LogLevel.Error);
						break;
					}
			}

			if (Config.RankSettings.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

			string newRank = noneRank;
			string clanTag = string.Empty;
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
					clanTag = rank.Tag ?? level;
				}
				else
					break;
			}

			if (setRank == null || newRank == noneRank || newRank == PlayerSummaries[player].Rank)
				return;

			if (Config.RankSettings.ScoreboardRanks)
				player.Clan = $"{clanTag ?? newRank}";

			Server.PrintToChatAll($" {ChatColors.Red}{Config.GeneralSettings.Prefix} {ChatColors.Gold}{player.PlayerName} has been {(setRank.Exp > PlayerSummaries[player].RankPoints ? "promoted" : "demoted")} to {newRank}.");

			PlayerSummaries[player].Rank = newRank;
			PlayerSummaries[player].RankPoints = setRank.Exp;

			string modifiedValue = setRank.Color;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			PlayerSummaries[player].RankObject!.Color = modifiedValue;
		}

		public void CheckNewRank(CCSPlayerController player)
		{
			if (!Config.GeneralSettings.ModuleRanks)
				return;

			if (Config.RankSettings.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

			string newRank = noneRank;
			string clanTag = string.Empty;
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
					clanTag = rank.Tag ?? level;
				}
				else
					break;
			}

			if (setRank == null)
			{
				PlayerSummaries[player].Rank = noneRank;
				PlayerSummaries[player].RankPoints = 0;
				PlayerSummaries[player].RankObject!.Color = $"{ChatColors.Default}";
				return;
			}

			if (Config.RankSettings.ScoreboardRanks)
				player.Clan = $"{clanTag ?? newRank}";

			PlayerSummaries[player].Rank = newRank;
			PlayerSummaries[player].RankPoints = setRank.Exp;

			string modifiedValue = setRank.Color;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			PlayerSummaries[player].RankObject!.Color = modifiedValue;
		}

		public bool IsPointsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return Config.GeneralSettings.ModuleRanks && (!K4ryuu.GameRules().WarmupPeriod || Config.RankSettings.WarmupPoints) && (Config.RankSettings.MinPlayers <= notBots);
		}

		public bool IsStatsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return Config.GeneralSettings.ModuleStats && (!K4ryuu.GameRules().WarmupPeriod || Config.StatisticSettings.WarmupStats) && (Config.StatisticSettings.MinPlayers <= notBots);
		}

		public void ResetKillStreak(int playerIndex)
		{
			playerKillStreaks[playerIndex] = (1, DateTime.Now);
		}

		private string GetFieldForTeam(CsTeam team)
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

		public void SaveClientRank(CCSPlayerController player)
		{
			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			string updateQuery = @$"INSERT INTO `{TablePrefix}k4ranks`
                                (`steam_id`, `name`, `rank`, `points`)
                                VALUES ('{player.SteamID}', '{escapedName}', '{PlayerSummaries[player].Rank}', {PlayerSummaries[player].Points})
                                ON DUPLICATE KEY UPDATE
								`name` = '{escapedName}',
                                `rank` = '{PlayerSummaries[player].Rank}',
                                `points` = {PlayerSummaries[player].Points};";

			MySql!.ExecuteNonQueryAsync(updateQuery);
		}

		public void SaveClientStats(CCSPlayerController player)
		{
			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			string updateQuery = @$"INSERT INTO `{TablePrefix}k4stats`
                                (`steam_id`, `name`, `kills`, `deaths`, `hits`, `headshots`, `grenades`, `mvp`, `round_win`, `round_lose`)
                                VALUES ('{player.SteamID}', '{escapedName}', {PlayerSummaries[player].StatFields["kills"]}, {PlayerSummaries[player].StatFields["deaths"]}, {PlayerSummaries[player].StatFields["hits"]}, {PlayerSummaries[player].StatFields["headshots"]}, {PlayerSummaries[player].StatFields["grenades"]}, {PlayerSummaries[player].StatFields["mvp"]}, {PlayerSummaries[player].StatFields["round_win"]}, {PlayerSummaries[player].StatFields["round_lose"]})
                                ON DUPLICATE KEY UPDATE
								`name` = '{escapedName}',
                                `kills` = {PlayerSummaries[player].StatFields["kills"]},
                                `deaths` = {PlayerSummaries[player].StatFields["deaths"]},
                                `hits` = {PlayerSummaries[player].StatFields["hits"]},
                                `headshots` = {PlayerSummaries[player].StatFields["headshots"]},
                                `grenades` = {PlayerSummaries[player].StatFields["grenades"]},
								`mvp` = {PlayerSummaries[player].StatFields["mvp"]},
								`round_win` = {PlayerSummaries[player].StatFields["round_win"]},
                                `round_lose` = {PlayerSummaries[player].StatFields["round_lose"]};";

			MySql!.ExecuteNonQueryAsync(updateQuery);
		}

		public void SaveClientTime(CCSPlayerController player)
		{
			DateTime now = DateTime.UtcNow;

			PlayerSummaries[player].TimeFields["all"] += (int)Math.Round((now - PlayerSummaries[player].Times["Connect"]).TotalSeconds);
			PlayerSummaries[player].TimeFields[GetFieldForTeam((CsTeam)player.TeamNum)] += (int)Math.Round((now - PlayerSummaries[player].Times["Team"]).TotalSeconds);
			PlayerSummaries[player].TimeFields[player.PawnIsAlive ? "alive" : "dead"] = (int)Math.Round((now - PlayerSummaries[player].Times["Death"]).TotalSeconds);

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			string updateQuery = @$"INSERT INTO `{TablePrefix}k4times`
                                (`steam_id`, `name`, `all`, `ct`, `t`, `spec`, `dead`, `alive`)
                                VALUES ('{player.SteamID}', '{escapedName}', {PlayerSummaries[player].TimeFields["all"]}, {PlayerSummaries[player].TimeFields["ct"]}, {PlayerSummaries[player].TimeFields["t"]}, {PlayerSummaries[player].TimeFields["spec"]}, {PlayerSummaries[player].TimeFields["dead"]}, {PlayerSummaries[player].TimeFields["alive"]})
                                ON DUPLICATE KEY UPDATE
								`name` = '{escapedName}',
                                `all` = {PlayerSummaries[player].TimeFields["all"]},
                                `ct` = {PlayerSummaries[player].TimeFields["ct"]},
                                `t` = {PlayerSummaries[player].TimeFields["t"]},
                                `spec` = {PlayerSummaries[player].TimeFields["spec"]},
                                `dead` = {PlayerSummaries[player].TimeFields["dead"]},
                                `alive` = {PlayerSummaries[player].TimeFields["alive"]};";

			MySql!.ExecuteNonQueryAsync(updateQuery);

			PlayerSummaries[player].Times["Connect"] = PlayerSummaries[player].Times["Team"] = PlayerSummaries[player].Times["Death"] = now;
		}
	}
}