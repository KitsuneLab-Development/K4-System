using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Admin;
using System.Reflection;

namespace K4ryuuSystem
{
	public partial class K4System
	{
		[ConsoleCommand("rank", "Check the current rank and points")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			string playerName = player!.PlayerName;
			int playerPoints = PlayerSummaries[player].Points;

			string nextRank = noneRank;
			string nextRankColor = "";
			int pointsUntilNextRank = 0;

			foreach (var kvp in ranks.OrderBy(r => r.Value.Exp))
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (playerPoints < rank.Exp)
				{
					nextRank = level;
					pointsUntilNextRank = rank.Exp - playerPoints;
					nextRankColor = rank.Color;
					break;
				}
			}

			string modifiedValue = nextRankColor;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (nextRankColor.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			// Find the player's place in the top list
			(int playerPlace, int totalPlayers) = GetPlayerPlaceAndCount(playerName);

			// Count higher ranks
			int higherRanksCount = ranks.Count(kv => kv.Value.Exp > playerPoints);

			player.PrintToChat($" {Config.GeneralSettings.Prefix} {PlayerSummaries[player].RankColor}{playerName}");
			player.PrintToChat($" {ChatColors.Blue}You have {ChatColors.Gold}{playerPoints} {ChatColors.Blue}points and are currently {PlayerSummaries[player].RankColor}{PlayerSummaries[player].Rank} ({ranks.Count - higherRanksCount} out of {ranks.Count})");
			player.PrintToChat($" {ChatColors.Blue}Next rank: {modifiedValue}{nextRank}");
			player.PrintToChat($" {ChatColors.Blue}Points until next rank: {ChatColors.Gold}{pointsUntilNextRank}");
			player.PrintToChat($" {ChatColors.Blue}Place in top list: {ChatColors.Gold}{playerPlace} out of {totalPlayers}");
		}


		[ConsoleCommand("resetmyrank", "Resets the player's own points to zero")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandResetMyRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			if (!PlayerSummaries.ContainsPlayer(player!))
				LoadPlayerData(player!);

			PlayerSummaries[player!].Points = 0;

			Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Red}{player!.PlayerName} has reset their rank and points.");
		}

		[ConsoleCommand("top", "Check the top 5 players by points")]
		[ConsoleCommand("top5", "Check the top 5 players by points")]
		[ConsoleCommand("ranktop", "Check the top 5 players by points")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckRankTopFive(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			PrintTopXPlayers(player!, 5);
		}

		[ConsoleCommand("top10", "Check the top 10 players by points")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckRankTopTen(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			PrintTopXPlayers(player!, 10);
		}

		[ConsoleCommand("stat", "Check your statistics")]
		[ConsoleCommand("statistics", "Check your statistics")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckStatistics(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleStats)
				return;

			command.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.LightRed}{player!.PlayerName}'s Statistics:");
			command.ReplyToCommand($" {ChatColors.Blue}Kills: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["kills"]} {ChatColors.Blue}| Deaths: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["deaths"]}");
			command.ReplyToCommand($" {ChatColors.Blue}Headshots: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["headshots"]} {ChatColors.Blue}| MVPs: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["mvp"]}");
			command.ReplyToCommand($" {ChatColors.Blue}Hits: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["hits"]} {ChatColors.Blue}| Grenades Thrown: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["grenades"]}");
			command.ReplyToCommand($" {ChatColors.Blue}Round Wins: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["round_win"]} {ChatColors.Blue}| Round Loses: {ChatColors.LightRed}{PlayerSummaries[player].StatFields["round_lose"]}");
		}

		[ConsoleCommand("resetrank", "Resets the targeted player's points to zero")]
		[CommandHelper(minArgs: 1, usage: "[SteamID64]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4system/admin")]
		public void OnCommandResetOtherRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			List<CCSPlayerController> players = Utilities.GetPlayers();
			foreach (CCSPlayerController target in players)
			{
				if (!target.IsBot && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
				{
					if (!PlayerSummaries.ContainsPlayer(target!))
						LoadPlayerData(target!);

					Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Red}{target.PlayerName}'s rank and points has been reset by {player!.PlayerName}.");
					Log($"{player.PlayerName} has reset {target.PlayerName}'s points.", LogLevel.Warning);

					PlayerSummaries[player].Points = 0;
					CheckNewRank(player);

					return;
				}
			}
		}

		[ConsoleCommand("setpoints", "Sets the targeted player's points to the given value")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4system/admin")]
		public void OnCommandSetPoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			if (int.TryParse(command.ArgByIndex(2), out int parsedInt))
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (!target.IsBot && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
					{
						if (!PlayerSummaries.ContainsPlayer(target!))
							LoadPlayerData(target!);

						Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Red}{target.PlayerName}'s points has been set to {parsedInt} by {player!.PlayerName}.");
						Log($"{player.PlayerName} has set {target.PlayerName}'s points to {parsedInt}.", LogLevel.Warning);

						PlayerSummaries[player].Points = parsedInt;
						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				command.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Red}The given amount is invalid.");
			}
		}

		[ConsoleCommand("givepoints", "Gives points to the targeted player")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4system/admin")]
		public void OnCommandGivePoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			if (int.TryParse(command.ArgByIndex(2), out int parsedInt))
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (!target.IsBot && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
					{
						if (!PlayerSummaries.ContainsPlayer(target!))
							LoadPlayerData(target!);

						Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Red}{player!.PlayerName} has given {parsedInt} points to {target.PlayerName}.");
						Log($"{player.PlayerName} has given {parsedInt} points to {target.PlayerName}.", LogLevel.Warning);

						PlayerSummaries[player].Points += parsedInt;
						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				command.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Red}The given amount is invalid.");
			}
		}

		[ConsoleCommand("removepoints", "Removes points from the targeted player")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4system/admin")]
		public void OnCommandRemovePoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!Config.GeneralSettings.ModuleRanks)
				return;

			if (int.TryParse(command.ArgByIndex(2), out int parsedInt))
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (!target.IsBot && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
					{
						if (!PlayerSummaries.ContainsPlayer(target!))
							LoadPlayerData(target!);

						Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Red}{player!.PlayerName} has removed {parsedInt} points from {target.PlayerName}.");
						Log($"{player.PlayerName} has removed {parsedInt} points from {target.PlayerName}.", LogLevel.Warning);

						PlayerSummaries[player].Points -= parsedInt;

						if (PlayerSummaries[player].Points < 0)
							PlayerSummaries[player].Points = 0;

						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				command.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Red}The given amount is invalid.");
			}
		}

		[ConsoleCommand("playtime", "Check the current playtime")]
		[ConsoleCommand("time", "Check the current playtime")]
		[ConsoleCommand("mytime", "Check the current playtime")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckPlaytime(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || !player.IsValid)
				return;

			if (!Config.GeneralSettings.ModuleTimes)
				return;

			DateTime now = DateTime.UtcNow;

			PlayerSummaries[player].TimeFields["all"] += (int)Math.Round((now - PlayerSummaries[player].Times["Connect"]).TotalSeconds);
			PlayerSummaries[player].TimeFields[GetFieldForTeam((CsTeam)player.TeamNum)] += (int)Math.Round((now - PlayerSummaries[player].Times["Team"]).TotalSeconds);
			PlayerSummaries[player].TimeFields[player.PawnIsAlive ? "alive" : "dead"] = (int)Math.Round((now - PlayerSummaries[player].Times["Death"]).TotalSeconds);

			command.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.LightRed}{player.PlayerName}'s Playtime Statistics:");
			command.ReplyToCommand($" {ChatColors.Blue}Total: {ChatColors.LightRed}{FormatPlaytime(PlayerSummaries[player].TimeFields["all"])}");
			command.ReplyToCommand($" {ChatColors.Blue}CT: {ChatColors.LightRed}{FormatPlaytime(PlayerSummaries[player].TimeFields["ct"])} {ChatColors.Blue}| T: {ChatColors.LightRed}{FormatPlaytime(PlayerSummaries[player].TimeFields["t"])}");
			command.ReplyToCommand($" {ChatColors.Blue}Spectator: {ChatColors.LightRed}{FormatPlaytime(PlayerSummaries[player].TimeFields["spec"])}");
			command.ReplyToCommand($" {ChatColors.Blue}Alive: {ChatColors.LightRed}{FormatPlaytime(PlayerSummaries[player].TimeFields["alive"])} {ChatColors.Blue}| Dead: {ChatColors.LightRed}{FormatPlaytime(PlayerSummaries[player].TimeFields["dead"])}");

			PlayerSummaries[player].Times["Connect"] = PlayerSummaries[player].Times["Team"] = PlayerSummaries[player].Times["Death"] = now;
		}

		[ConsoleCommand("k4", "More informations about K4-System")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckK4(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || !player.IsValid)
				return;

			command.ReplyToCommand($" {Config.GeneralSettings.Prefix} Available Commands:");
			command.ReplyToCommand($" {ChatColors.Blue}PlayTime Commands: {ChatColors.Gold}!time{ChatColors.Blue}, {ChatColors.Gold}!mytime{ChatColors.Blue}, {ChatColors.Gold}!playtime");
			command.ReplyToCommand($" {ChatColors.Blue}Rank Commands: {ChatColors.Gold}!rank{ChatColors.Blue}, {ChatColors.Gold}!resetmyrank");
			command.ReplyToCommand($" {ChatColors.Blue}Statistic Commands: {ChatColors.Gold}!stat{ChatColors.Blue}, {ChatColors.Gold}!statistics");
			command.ReplyToCommand($" {ChatColors.Blue}Toplist Commands: {ChatColors.Gold}!ranktop{ChatColors.Blue}, {ChatColors.Gold}!top{ChatColors.Blue}, {ChatColors.Gold}!top5{ChatColors.Blue}, {ChatColors.Gold}!top10");
		}
	}
}