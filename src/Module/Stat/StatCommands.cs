namespace K4System
{
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleStat : IModuleStat
	{
		public void Initialize_Commands(Plugin plugin)
		{
			CommandSettings commands = Config.CommandSettings;

			commands.StatCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check your statistics",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					if (!statCache.ContainsPlayer(player))
					{
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} Your data is not yet loaded. Please try again later...");
						return;
					}

					StatData playerData = statCache[player];

					info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{player!.PlayerName}'s Statistics:");
					info.ReplyToCommand($"--- {ChatColors.Silver}Kills: {ChatColors.Lime}{playerData.StatFields["kills"]} {ChatColors.Silver}| Firstblood: {ChatColors.Lime}{playerData.StatFields["firstblood"]} {ChatColors.Silver}| Assists: {ChatColors.Lime}{playerData.StatFields["assists"]}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Hits Given: {ChatColors.Lime}{playerData.StatFields["hits_given"]} {ChatColors.Silver}| Hits Taken: {ChatColors.Lime}{playerData.StatFields["hits_taken"]} {ChatColors.Silver}| Deaths: {ChatColors.Lime}{playerData.StatFields["deaths"]} {ChatColors.Silver}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Headshots: {ChatColors.Lime}{playerData.StatFields["headshots"]} {ChatColors.Silver}| Headshot Percentage: {ChatColors.Lime}{((playerData.StatFields["shoots"] > 0) ? playerData.StatFields["headshots"] / playerData.StatFields["shoots"] * 100 : 0)}% {ChatColors.Silver}| Grenades Thrown: {ChatColors.Lime}{playerData.StatFields["grenades"]}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Round Wins: {ChatColors.Lime}{playerData.StatFields["round_win"]} {ChatColors.Silver}| Round Loses: {ChatColors.Lime}{playerData.StatFields["round_lose"]} {ChatColors.Silver}| Chance: {ChatColors.Lime}{((playerData.StatFields["round_win"] + playerData.StatFields["round_lose"] > 0) ? playerData.StatFields["round_win"] / (playerData.StatFields["round_win"] + playerData.StatFields["round_lose"]) * 100 : 0)}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Game Wins: {ChatColors.Lime}{playerData.StatFields["game_win"]} {ChatColors.Silver}| Game Loses: {ChatColors.Lime}{playerData.StatFields["game_lose"]} {ChatColors.Silver}| Chance: {ChatColors.Lime}{((playerData.StatFields["game_win"] + playerData.StatFields["game_lose"] > 0) ? playerData.StatFields["game_win"] / (playerData.StatFields["game_win"] + playerData.StatFields["game_lose"]) * 100 : 0)}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Shoots: {ChatColors.Lime}{playerData.StatFields["shoots"]} {ChatColors.Silver}| Accuracy: {ChatColors.Lime}{((playerData.StatFields["shoots"] > 0) ? playerData.StatFields["hits_given"] / playerData.StatFields["shoots"] * 100 : 0)}%");
					info.ReplyToCommand($"--- {ChatColors.Silver}KDA: {ChatColors.Lime}{playerData.KDA} {ChatColors.Silver}| MVPs: {ChatColors.Lime}{playerData.StatFields["mvp"]}");
				});
			});
		}
	}
}