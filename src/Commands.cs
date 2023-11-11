using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Admin;

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

			command.ReplyToCommand($" {config.ChatPrefix} {PlayerSummaries[player!].RankColor}{player!.PlayerName} {ChatColors.White}has {ChatColors.Red}{PlayerSummaries[player].Points} {ChatColors.White}points and is currently {PlayerSummaries[player].RankColor}{PlayerSummaries[player].Rank}");
		}

		[ConsoleCommand("resetmyrank", "Resets the player's own points to zero")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandResetMyRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!PlayerSummaries.ContainsPlayer(player!))
				LoadPlayerData(player!);

			MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = 0 WHERE `steam_id` = {player!.SteamID};");

			Server.PrintToChatAll($" {config.ChatPrefix} {ChatColors.Red}{player.PlayerName} has reset their rank and points.");
		}

		[ConsoleCommand("top", "Check the top 5 players by points")]
		[ConsoleCommand("top5", "Check the top 5 players by points")]
		[ConsoleCommand("ranktop", "Check the top 5 players by points")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckRankTopFive(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			PrintTopXPlayers(player!, 5);
		}

		[ConsoleCommand("top10", "Check the top 10 players by points")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckRankTopTen(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
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

			MySqlQueryResult result = MySql!.Table("k4stats").Where(MySqlQueryCondition.New("steam_id", "=", player!.SteamID.ToString())).Select();

			if (result.Rows > 0)
			{
				command.ReplyToCommand($" {config.ChatPrefix} {ChatColors.LightRed}{player.PlayerName}'s Statistics:");
				command.ReplyToCommand($" {ChatColors.Blue}Kills: {ChatColors.LightRed}{result.Get<int>(0, "kills")} {ChatColors.Blue}| Headshots: {ChatColors.LightRed}{result.Get<int>(0, "headshots")}");
				command.ReplyToCommand($" {ChatColors.Blue}Deaths: {ChatColors.LightRed}{result.Get<int>(0, "deaths")}");
				command.ReplyToCommand($" {ChatColors.Blue}Hits: {ChatColors.LightRed}{result.Get<int>(0, "hits")} {ChatColors.Blue}| Grenades Thrown: {ChatColors.LightRed}{result.Get<int>(0, "grenades")}");
			}
			else command.ReplyToCommand($" {config.ChatPrefix} {ChatColors.LightRed}We don't have your statistics data at the moment. Please check again later!");

		}

		[ConsoleCommand("resetrank", "Resets the targeted player's points to zero")]
		[CommandHelper(minArgs: 1, usage: "[SteamID64]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandResetOtherRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			List<CCSPlayerController> players = Utilities.GetPlayers();
			foreach (CCSPlayerController target in players)
			{
				if (!target.IsBot && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
				{
					if (!PlayerSummaries.ContainsPlayer(target!))
						LoadPlayerData(target!);

					MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = 0 WHERE `steam_id` = {target.SteamID};");

					Server.PrintToChatAll($" {config.ChatPrefix} {ChatColors.Red}{target.PlayerName}'s rank and points has been reset by {player!.PlayerName}.");
					Log($"{player.PlayerName} has reset {target.PlayerName}'s points.");

					PlayerSummaries[player].Points = 0;
					CheckNewRank(player);

					return;
				}
			}
		}

		[ConsoleCommand("setpoints", "Sets the targeted player's points to the given value")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandSetPoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
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

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = {parsedInt} WHERE `steam_id` = {target.SteamID};");

						Server.PrintToChatAll($" {config.ChatPrefix} {ChatColors.Red}{target.PlayerName}'s points has been set to {parsedInt} by {player!.PlayerName}.");
						Log($"{player.PlayerName} has set {target.PlayerName}'s points to {parsedInt}.");

						PlayerSummaries[player].Points = parsedInt;
						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				command.ReplyToCommand($" {config.ChatPrefix} {ChatColors.Red}The given amount is invalid.");
			}
		}

		[ConsoleCommand("givepoints", "Gives points to the targeted player")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandGivePoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
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

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {parsedInt}) WHERE `steam_id` = {target.SteamID};");

						Server.PrintToChatAll($" {config.ChatPrefix} {ChatColors.Red}{player!.PlayerName} has given {parsedInt} points to {target.PlayerName}.");
						Log($"{player.PlayerName} has given {parsedInt} points to {target.PlayerName}.");

						PlayerSummaries[player].Points += parsedInt;
						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				command.ReplyToCommand($" {config.ChatPrefix} {ChatColors.Red}The given amount is invalid.");
			}
		}

		[ConsoleCommand("removepoints", "Removes points from the targeted player")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandRemovePoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
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

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` - {parsedInt}) WHERE `steam_id` = {target.SteamID};");

						Server.PrintToChatAll($" {config.ChatPrefix} {ChatColors.Red}{player!.PlayerName} has removed {parsedInt} points from {target.PlayerName}.");
						Log($"{player.PlayerName} has removed {parsedInt} points from {target.PlayerName}.");

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
				command.ReplyToCommand($" {config.ChatPrefix} {ChatColors.Red}The given amount is invalid.");
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

			SaveClientTime(player);

			MySqlQueryResult result = MySql!.Table("k4times").Where($"`steam_id` = '{player.SteamID}'").Select();

			if (result.Rows > 0)
			{
				command.ReplyToCommand($" {config.ChatPrefix} {ChatColors.LightRed}{player.PlayerName}'s Playtime Statistics:");
				command.ReplyToCommand($" {ChatColors.Blue}Total: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "all"))}");
				command.ReplyToCommand($" {ChatColors.Blue}CT: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "ct"))} {ChatColors.Blue}| T: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "t"))}");
				command.ReplyToCommand($" {ChatColors.Blue}Spectator: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "spec"))}");
				command.ReplyToCommand($" {ChatColors.Blue}Alive: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "alive"))} {ChatColors.Blue}| Dead: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "dead"))}");
			}
			else command.ReplyToCommand($" {config.ChatPrefix} {ChatColors.LightRed}We don't have your playtime data at the moment. Please check again later!");
		}

		[ConsoleCommand("k4", "More informations about K4-System")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckK4(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || !player.IsValid)
				return;

			command.ReplyToCommand($" {config.ChatPrefix} Available Commands:");
			command.ReplyToCommand($" {ChatColors.Blue}PlayTime Commands: !time, !mytime, !playtime");
			command.ReplyToCommand($" {ChatColors.Blue}Rank Commands: !rank, !resetmyrank");
			command.ReplyToCommand($" {ChatColors.Blue}Statistic Commands: !stat, !statistics");
			command.ReplyToCommand($" {ChatColors.Blue}Toplist Commands: !ranktop, !top, !top5, !top10");
		}
	}
}