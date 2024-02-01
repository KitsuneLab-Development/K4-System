namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleTime : IModuleTime
	{
		public void Initialize_Commands(Plugin plugin)
		{
			CommandSettings commands = Config.CommandSettings;

			commands.TimeCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check your playtime", plugin.CallbackAnonymizer(OnCommandTime));
			});
		}

		public void OnCommandTime(CCSPlayerController? player, CommandInfo info)
		{
			Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			if (!timeCache.ContainsPlayer(player!))
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
				return;
			}

			TimeData playerData = timeCache[player!];

			DateTime now = DateTime.UtcNow;

			playerData.TimeFields["all"] += (int)Math.Round((now - playerData.Times["Connect"]).TotalSeconds);
			playerData.TimeFields[GetFieldForTeam((CsTeam)player!.TeamNum)] += (int)Math.Round((now - playerData.Times["Team"]).TotalSeconds);

			if ((CsTeam)player.TeamNum > CsTeam.Spectator)
				playerData.TimeFields[player.PawnIsAlive ? "alive" : "dead"] += (int)Math.Round((now - playerData.Times["Death"]).TotalSeconds);

			info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.times.title", player.PlayerName]}");
			info.ReplyToCommand($" {plugin.Localizer["k4.times.line1", FormatPlaytime(playerData.TimeFields["all"])]}");
			info.ReplyToCommand($" {plugin.Localizer["k4.times.line2", FormatPlaytime(playerData.TimeFields["ct"]), FormatPlaytime(playerData.TimeFields["t"])]}");
			info.ReplyToCommand($" {plugin.Localizer["k4.times.line3", FormatPlaytime(playerData.TimeFields["spec"])]}");
			info.ReplyToCommand($" {plugin.Localizer["k4.times.line4", FormatPlaytime(playerData.TimeFields["alive"]), FormatPlaytime(playerData.TimeFields["dead"])]}");
			playerData.Times = new Dictionary<string, DateTime>
			{
				{ "Connect", now },
				{ "Team", now },
				{ "Death", now }
			};
		}
	}
}