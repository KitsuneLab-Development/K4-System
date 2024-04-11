namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleTime : IModuleTime
	{
		public void Initialize_Commands()
		{
			CommandSettings commands = Config.CommandSettings;

			commands.TimeCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check your playtime", plugin.CallbackAnonymizer(OnCommandTime));
			});
		}

		public void OnCommandTime(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			K4Player? k4player = plugin.GetK4Player(player!);

			if (k4player is null)
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
				return;
			}

			TimeData? playerData = k4player.timeData;

			if (playerData is null)
				return;

			DateTime now = DateTime.UtcNow;

			playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
			playerData.TimeFields[GetFieldForTeam((CsTeam)player!.TeamNum)] += (int)(now - playerData.Times["Team"]).TotalSeconds;

			if ((CsTeam)player.TeamNum > CsTeam.Spectator)
				playerData.TimeFields[player.PawnIsAlive ? "alive" : "dead"] += (int)(now - playerData.Times["Death"]).TotalSeconds;

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