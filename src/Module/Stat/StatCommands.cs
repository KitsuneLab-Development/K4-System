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
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {plugin.Localizer["k4.general.loading"]}");
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

					info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {plugin.Localizer["k4.stats.title", player!.PlayerName]}");
					info.ReplyToCommand(plugin.Localizer["k4.stats.line1", kills, firstblood, assists]);
					info.ReplyToCommand(plugin.Localizer["k4.stats.line2", hitsGiven, hitsTaken, deaths]);
					info.ReplyToCommand(plugin.Localizer["k4.stats.line3", headshots, roundedHeadshotPercentage, grenadesThrown]);
					info.ReplyToCommand(plugin.Localizer["k4.stats.line4", roundWin, roundLose, roundChance]);
					info.ReplyToCommand(plugin.Localizer["k4.stats.line5", gameWin, gameLose, gameChance]);
					info.ReplyToCommand(plugin.Localizer["k4.stats.line6", shoots, accuracy]);
					info.ReplyToCommand(plugin.Localizer["k4.stats.line7", playerData.KDA, mvp]);
				});
			});
		}
	}
}