
namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Admin;
	using System.Data;
	using K4System.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public CommandInfo.CommandCallback CallbackAnonymizer(Action<CCSPlayerController?, CommandInfo> action)
		{
			return new CommandInfo.CommandCallback(action);
		}

		public string ApplyPrefixColors(string msg)
		{
			var chatColors = typeof(ChatColors).GetFields().Select(f => (f.Name, Value: f.GetValue(null)?.ToString()));
			foreach (var (name, value) in chatColors)
			{
				msg = msg.Replace(name, value, StringComparison.OrdinalIgnoreCase);
			}

			return msg;
		}

		public bool CommandHelper(CCSPlayerController? player, CommandInfo info, CommandUsage usage, int argCount = 0, string? help = null, string? permission = null)
		{
			switch (usage)
			{
				case CommandUsage.CLIENT_ONLY:
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
					{
						info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandclientonly"]}");
						return false;
					}
					break;
				case CommandUsage.SERVER_ONLY:
					if (player != null)
					{
						info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandserveronly"]}");
						return false;
					}
					break;
				case CommandUsage.CLIENT_AND_SERVER:
					if (!(player == null || (player != null && player.IsValid && player.PlayerPawn.Value != null)))
						return false;
					break;
			}

			if (permission != null && permission.Length > 0)
			{
				if (player != null && !AdminManager.PlayerHasPermissions(player, permission))
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandnoperm"]}");
					return false;
				}
			}

			if (argCount > 0 && help != null)
			{
				int checkArgCount = argCount + 1;
				if (info.ArgCount < checkArgCount)
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandhelp", info.ArgByIndex(0), help]}");
					return false;
				}
			}

			return true;
		}

		public K4Player? GetK4Player(ulong steamID)
		{
			return K4Players.FirstOrDefault(player => player.SteamID == steamID);
		}

		public K4Player? GetK4Player(CCSPlayerController playerController)
		{
			return K4Players.FirstOrDefault(player => player.Controller == playerController);
		}

		public static bool PlayerHasPermission(K4Player k4player, string permission)
		{
			switch (permission[0])
			{
				case '@':
					return AdminManager.PlayerHasPermissions(k4player.Controller, permission);
				case '#':
					return AdminManager.PlayerInGroup(k4player.Controller, permission);
				default:
					return AdminManager.PlayerHasCommandOverride(k4player.Controller, permission);
			}
		}
	}
}