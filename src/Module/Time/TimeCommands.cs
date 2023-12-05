namespace K4System
{
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleTime : IModuleTime
	{
		public void Initialize_Commands(Plugin plugin)
		{
			CommandSettings commands = Config.CommandSettings;

			commands.TimeCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check your playtime",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					if (!timeCache.ContainsPlayer(player))
					{
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} Your data is not yet loaded. Please try again later...");
						return;
					}

					TimeData playerData = timeCache[player];

					DateTime now = DateTime.UtcNow;

					playerData.TimeFields["all"] += (int)Math.Round((now - playerData.Times["Connect"]).TotalSeconds);
					playerData.TimeFields[GetFieldForTeam((CsTeam)player.TeamNum)] += (int)Math.Round((now - playerData.Times["Team"]).TotalSeconds);

					if ((CsTeam)player.TeamNum > CsTeam.Spectator)
						playerData.TimeFields[player.PawnIsAlive ? "alive" : "dead"] += (int)Math.Round((now - playerData.Times["Death"]).TotalSeconds);

					info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{player.PlayerName}'s PlayTime:");
					info.ReplyToCommand($"--- {ChatColors.Silver}Total: {ChatColors.Lime}{FormatPlaytime(playerData.TimeFields["all"])}");
					info.ReplyToCommand($"--- {ChatColors.Silver}CT: {ChatColors.Lime}{FormatPlaytime(playerData.TimeFields["ct"])} {ChatColors.Silver}| T: {ChatColors.Lime}{FormatPlaytime(playerData.TimeFields["t"])}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Spectator: {ChatColors.Lime}{FormatPlaytime(playerData.TimeFields["spec"])}");
					info.ReplyToCommand($"--- {ChatColors.Silver}Alive: {ChatColors.Lime}{FormatPlaytime(playerData.TimeFields["alive"])} {ChatColors.Silver}| Dead: {ChatColors.Lime}{FormatPlaytime(playerData.TimeFields["dead"])}");

					playerData.Times = new Dictionary<string, DateTime>
					{
						{ "Connect", now },
						{ "Team", now },
						{ "Death", now }
					};
				});
			});
		}
	}
}