namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleUtils : IModuleUtils
	{
		public void Initialize_Commands(Plugin plugin)
		{
			CommandSettings commands = Config.CommandSettings;

			commands.AdminListCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check online admins", plugin.CallbackAnonymizer(OnCommandAdmins));
			});
		}

		public void OnCommandAdmins(CCSPlayerController? player, CommandInfo info)
		{
			if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
				return;

			Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

			List<string> onlineAdmins = Utilities.GetPlayers()
				.SelectMany(adminPlayer => Config.GeneralSettings.AdminSettingsList.Where(adminSettings =>
				{
					if (adminSettings.ListColor == null)
						return false;

					switch (adminSettings.Permission[0])
					{
						case '@':
							return AdminManager.PlayerHasPermissions(adminPlayer, adminSettings.Permission);
						case '#':
							return AdminManager.PlayerInGroup(adminPlayer, adminSettings.Permission);
						default:
							return AdminManager.PlayerHasCommandOverride(adminPlayer, adminSettings.Permission);
					}
				}).Select(adminSettings => new { adminPlayer, adminSettings }))
				.Select(x => $"{plugin.ApplyPrefixColors(x.adminSettings.ListColor ?? "default")}{x.adminPlayer.PlayerName}")
				.ToList();

			if (onlineAdmins.Count > 0)
			{
				string adminList = string.Join($"{ChatColors.Silver},", onlineAdmins);

				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.adminlist.title"]}");
				info.ReplyToCommand($" {adminList}");
			}
			else
				info.ReplyToCommand($" {plugin.Localizer["k4.adminlist.no-admins"]}");
		}
	}
}