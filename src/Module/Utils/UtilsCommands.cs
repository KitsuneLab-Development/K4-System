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
				plugin.AddCommand($"css_{commandString}", "Check online admins",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					List<string> onlineAdmins = new List<string>();
					List<CCSPlayerController> players = Utilities.GetPlayers();

					foreach (CCSPlayerController adminPlayer in players)
					{
						foreach (AdminSettingsEntry adminSettings in Config.GeneralSettings.AdminSettingsList)
						{
							if (adminSettings.ListColor == null)
								continue;

							if (AdminManager.PlayerHasPermissions(adminPlayer, adminSettings.Permission))
							{
								string adminName = $"{adminSettings.ListColor}{adminPlayer.PlayerName}";
								onlineAdmins.Add(adminName);
							}
						}
					}

					if (onlineAdmins.Count > 0)
					{
						string adminList = string.Join($"{ChatColors.Silver},", onlineAdmins);

						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.adminlist.title"]}");
						info.ReplyToCommand($" {adminList}");
					}
					else
						info.ReplyToCommand($" {plugin.Localizer["k4.adminlist.no-admins"]}");
				});
			});
		}
	}
}