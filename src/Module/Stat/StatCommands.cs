namespace K4System
{
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;

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

					int kills = playerData.StatFields["kills"];
					int firstblood = playerData.StatFields["firstblood"];
					int assists = playerData.StatFields["assists"];
					int hitsGiven = playerData.StatFields["hits_given"];
					int hitsTaken = playerData.StatFields["hits_taken"];
					int deaths = playerData.StatFields["deaths"];
					int headshots = playerData.StatFields["headshots"];
					int grenadesThrown = playerData.StatFields["grenades"];
					int roundWin = playerData.StatFields["round_win"];
					int roundLose = playerData.StatFields["round_lose"];
					int gameWin = playerData.StatFields["game_win"];
					int gameLose = playerData.StatFields["game_lose"];
					int shoots = playerData.StatFields["shoots"];
					int mvp = playerData.StatFields["mvp"];

					float roundedHeadshotPercentage = (float)Math.Round((float)headshots / hitsGiven * 100, 1);
					float roundChance = (roundWin + roundLose) > 0 ? (float)Math.Round((float)roundWin / (roundWin + roundLose) * 100, 1) : 0;
					float gameChance = (gameWin + gameLose) > 0 ? (float)Math.Round((float)gameWin / (gameWin + gameLose) * 100, 1) : 0;
					float accuracy = shoots > 0 ? (float)Math.Round((float)hitsGiven / shoots * 100, 1) : 0;

					info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{player!.PlayerName}'s Statistics:");
					info.ReplyToCommand($"--- {ChatColors.Silver}Kills: {ChatColors.Lime}{kills} {ChatColors.Silver}| Firstblood: {ChatColors.Lime}{firstblood} {ChatColors.Silver}| Assists: {ChatColors.Lime}{assists}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Hits Given: {ChatColors.Lime}{hitsGiven} {ChatColors.Silver}| Hits Taken: {ChatColors.Lime}{hitsTaken} {ChatColors.Silver}| Deaths: {ChatColors.Lime}{deaths} {ChatColors.Silver}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Headshots: {ChatColors.Lime}{headshots} {ChatColors.Silver}| Headshot Percentage: {ChatColors.Lime}{roundedHeadshotPercentage}% {ChatColors.Silver}| Grenades Thrown: {ChatColors.Lime}{grenadesThrown}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Round Wins: {ChatColors.Lime}{roundWin} {ChatColors.Silver}| Round Loses: {ChatColors.Lime}{roundLose} {ChatColors.Silver}| Chance: {ChatColors.Lime}{roundChance}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Game Wins: {ChatColors.Lime}{gameWin} {ChatColors.Silver}| Game Loses: {ChatColors.Lime}{gameLose} {ChatColors.Silver}| Chance: {ChatColors.Lime}{gameChance}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Shoots: {ChatColors.Lime}{shoots} {ChatColors.Silver}| Accuracy: {ChatColors.Lime}{accuracy}%");
					info.ReplyToCommand($"--- {ChatColors.Silver}KDA: {ChatColors.Lime}{playerData.KDA} {ChatColors.Silver}| MVPs: {ChatColors.Lime}{mvp}");
				});
			});
		}
	}
}