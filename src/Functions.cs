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
		""Exp"": 250, // From this amount of experience, the player is Silver
		""Color"": ""LightBlue"" // Color code for the rank. Find color names here: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Utils/ChatColors.cs
	},
	""Gold"": {
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
				Log("An error occurred: " + ex.Message);
			}
		}

		public async void PrintTopXPlayers(CCSPlayerController player, int number)
		{
			MySqlQueryResult result = await MySql!.Table("k4ranks").ExecuteQueryAsync($"SELECT `points`, `name` FROM `k4ranks` ORDER BY `points` DESC LIMIT {number};");

			if (result.Count > 0)
			{
				player!.PrintToChat($" {config.ChatPrefix} Top 5 Players:");

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
				player!.PrintToChat($" {config.ChatPrefix} No players found in the top {number}.");
			}
		}

		public void LoadPlayerData(CCSPlayerController player)
		{
			User newUser = new User
			{
				Points = 0,
				Rank = noneRank,
				RankColor = $"{ChatColors.Default}",
				RankPoints = -1
			};
			PlayerSummaries[player] = newUser;

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			MySqlQueryValue values = new MySqlQueryValue()
										.Add("name", escapedName)
										.Add("steam_id", player.SteamID.ToString());

			MySql!.Table("k4times").InsertIfNotExist(values, $"`name` = '{escapedName}'");
			MySql!.Table("k4stats").InsertIfNotExist(values, $"`name` = '{escapedName}'");
			MySql!.Table("k4ranks").InsertIfNotExist(values.Add("`rank`", MySqlHelper.EscapeString(noneRank)), $"`name` = '{escapedName}'");

			MySqlQueryResult result = MySql!.Table("k4ranks").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select("points");

			PlayerSummaries[player].Points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

			if (config.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

			string newRank = noneRank;
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
				}
				else
					break;
			}

			if (setRank == null)
				return;

			if (config.ScoreboardRanks)
				player.Clan = newRank;

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

			PlayerSummaries[player].RankColor = modifiedValue;
		}

		public void ModifyClientPoints(CCSPlayerController player, CHANGE_MODE mode, int amount, string reason)
		{
			if (!player.IsValidPlayer())
				return;

			if (!IsPointsAllowed() || amount == 0)
				return;

			if (!PlayerSummaries.ContainsPlayer(player))
				LoadPlayerData(player);

			if (AdminManager.PlayerHasPermissions(player, "@k4ranks/vip/points-multiplier"))
			{
				amount = (int)Math.Round(amount * config.VipPointMultiplier);
			}

			switch (mode)
			{
				case CHANGE_MODE.SET:
					{
						player.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Gold}{PlayerSummaries[player].Points} [={amount} {reason}]");
						PlayerSummaries[player].Points = amount;
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = {amount} WHERE `steam_id` = {player.SteamID};");
						break;
					}
				case CHANGE_MODE.GIVE:
					{
						player.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[player].Points} [+{amount} {reason}]");
						PlayerSummaries[player].Points += amount;
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {amount}) WHERE `steam_id` = {player.SteamID};");
						break;
					}
				case CHANGE_MODE.REMOVE:
					{
						player.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Red}{PlayerSummaries[player].Points} [-{amount} {reason}]");
						PlayerSummaries[player].Points -= amount;

						if (PlayerSummaries[player].Points < 0)
							PlayerSummaries[player].Points = 0;

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = GREATEST(`points` - {amount}, 0) WHERE `steam_id` = {player.SteamID};");
						break;
					}
				default:
					{
						Log($"Invalid operation at the point modification function: {mode}");
						break;
					}
			}

			if (config.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

			string newRank = noneRank;
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
				}
				else
					break;
			}

			if (setRank == null || newRank == noneRank || newRank == PlayerSummaries[player].Rank)
				return;

			if (config.ScoreboardRanks)
				player.Clan = newRank;

			Server.PrintToChatAll($" {ChatColors.Red}{config.ChatPrefix} {ChatColors.Gold}{player.PlayerName} has been {(setRank.Exp > PlayerSummaries[player].RankPoints ? "promoted" : "demoted")} to {newRank}.");

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

			PlayerSummaries[player].RankColor = modifiedValue;
		}

		public void CheckNewRank(CCSPlayerController player)
		{
			if (config.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

			string newRank = noneRank;
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
				}
				else
					break;
			}

			if (setRank == null)
			{
				PlayerSummaries[player].Rank = noneRank;
				PlayerSummaries[player].RankPoints = 0;
				PlayerSummaries[player].RankColor = $"{ChatColors.Default}";
				return;
			}

			if (config.ScoreboardRanks)
				player.Clan = newRank;

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

			PlayerSummaries[player].RankColor = modifiedValue;
		}

		public bool IsPointsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return (!K4ryuu.GameRules().WarmupPeriod || config.WarmupPoints) && (config.MinPlayersPoints <= notBots);
		}

		public bool IsStatsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return (!K4ryuu.GameRules().WarmupPeriod || config.WarmupStats) && (config.MinPlayersStats <= notBots);
		}

		private void UpdatePlayerData(CCSPlayerController player, string field, double value)
		{
			MySql!.ExecuteNonQueryAsync($"UPDATE `player_stats` SET `{field}` = `{field}` + {(int)Math.Round(value)} WHERE `steam_id` = {player.SteamID};");
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
			int[] values = { totalSeconds / 31536000, (totalSeconds % 31536000) / 2592000, (totalSeconds % 2592000) / 86400, (totalSeconds % 86400) / 3600, (totalSeconds % 3600) / 60, totalSeconds % 60 };

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

		public void SaveClientTime(CCSPlayerController player)
		{
			DateTime now = DateTime.UtcNow;

			if (!player.IsValidPlayer())
				return;

			if (!PlayerSummaries.ContainsPlayer(player))
				LoadPlayerData(player);

			int allSeconds = (int)Math.Round((now - PlayerSummaries[player].Times["Connect"]).TotalSeconds);
			int teamSeconds = (int)Math.Round((now - PlayerSummaries[player].Times["Team"]).TotalSeconds);

			string updateQuery = $@"UPDATE `player_stats`
                           SET `all` = `all` + {allSeconds}";

			switch ((CsTeam)player.TeamNum)
			{
				case CsTeam.Terrorist:
					{
						updateQuery += $", `t` = `t` + {teamSeconds}";
						break;
					}
				case CsTeam.CounterTerrorist:
					{
						updateQuery += $", `ct` = `ct` + {teamSeconds}";
						break;
					}
				default:
					{
						updateQuery += $", `spec` = `spec` + {teamSeconds}";
						break;
					}
			}

			string field = player.PawnIsAlive ? "alive" : "dead";
			int deathSeconds = (int)Math.Round((now - PlayerSummaries[player].Times["Death"]).TotalSeconds);
			updateQuery += $", `{field}` = `{field}` + {deathSeconds}";

			updateQuery += $@" WHERE `steamid` = {player.SteamID}";

			MySql!.ExecuteNonQueryAsync(updateQuery);

			PlayerSummaries[player].Times["Connect"] = now;
			PlayerSummaries[player].Times["Team"] = now;
			PlayerSummaries[player].Times["Death"] = now;
		}
	}
}