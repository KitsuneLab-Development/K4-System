namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleUtils : IModuleUtils
	{
		public void Initialize_Commands()
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

			List<string> adminList = new List<string>();

			foreach (K4Player k4player in plugin.K4Players)
			{
				if (!k4player.IsValid || !k4player.IsPlayer)
					continue;

				foreach (AdminSettingsEntry entry in Config.GeneralSettings.AdminSettingsList)
				{
					if (entry.ListColor == null)
						continue;

					if (Plugin.PlayerHasPermission(k4player, entry.Permission))
					{
						adminList.Add($"{plugin.ApplyPrefixColors(entry.ListColor ?? "default")}{k4player.PlayerName}");
						break;
					}
				}
			}

			if (adminList.Count > 0)
			{
				string onlineAdmins = string.Join($"{ChatColors.Silver},", adminList);

				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.adminlist.title"]}");
				info.ReplyToCommand($" {onlineAdmins}");
			}
			else
				info.ReplyToCommand($" {plugin.Localizer["k4.adminlist.no-admins"]}");
		}
	}
}