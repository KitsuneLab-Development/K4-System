
namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Admin;
	using System.Data;
	using K4System.Models;
	using MaxMind.GeoIP2;
	using Microsoft.Extensions.Logging;
	using MaxMind.GeoIP2.Exceptions;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using CounterStrikeSharp.API;

	public sealed partial class Plugin : BasePlugin
	{
		private static readonly Dictionary<string, char> PredefinedColors = typeof(ChatColors)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.ToDictionary(field => $"{{{field.Name}}}", field => (char)(field.GetValue(null) ?? '\x01'));

		public string ApplyPrefixColors(string msg)
		{
			var chatColors = typeof(ChatColors).GetFields()
				.Select(f => new { f.Name, Value = f.GetValue(null)?.ToString() })
				.OrderByDescending(c => c.Name.Length);

			foreach (var color in chatColors)
			{
				if (color.Value != null)
				{
					msg = Regex.Replace(msg, $@"\b{color.Name}\b", color.Value, RegexOptions.IgnoreCase);
				}
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
			return K4Players.ToList().FirstOrDefault(player => player.SteamID == steamID);
		}

		public K4Player? GetK4Player(CCSPlayerController? playerController)
		{
			return K4Players.ToList().FirstOrDefault(player => player.Controller == playerController);
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

		public async Task<string> GetPlayerCountryCodeAsync(string? playerIp)
		{
			if (playerIp == null)
				return "??";

			string[] parts = playerIp.Split(':');
			string realIP = parts.Length == 2 ? parts[0] : playerIp;

			string filePath = Path.Combine(ModuleDirectory, "GeoLite2-Country.mmdb");
			if (!File.Exists(filePath))
			{
				Logger.LogError($"GeoLite2-Country.mmdb not found in {filePath}. Download it from https://github.com/P3TERX/GeoLite.mmdb/releases and place it in the same directory as the plugin.");
				return "??";
			}

			return await Task.Run(() =>
			{
				using (DatabaseReader reader = new DatabaseReader(filePath))
				{
					try
					{
						MaxMind.GeoIP2.Responses.CountryResponse response = reader.Country(realIP);
						return response.Country.IsoCode ?? "??";
					}
					catch (AddressNotFoundException)
					{
						Logger.LogError($"The address {realIP} is not in the database.");
						return "??";
					}
					catch (GeoIP2Exception ex)
					{
						Logger.LogError($"Error: {ex.Message}");
						return "??";
					}
				}
			});
		}

		public void ReplacePlaceholders(K4Player k4player, string text, Action<string> callback)
		{
			if (k4player == null)
			{
				callback(text);
				return;
			}

			Dictionary<string, string> placeholders = new Dictionary<string, string>
			{
				{ "name", k4player.PlayerName },
				{ "steamid", k4player.SteamID.ToString() },
				{ "clantag", k4player.ClanTag },
				{ "rank", k4player.rankData?.Rank?.Name ?? "Unranked" },
				{ "points", k4player.rankData?.Points.ToString() ?? "0" },
				{ "topplacement", k4player.rankData?.TopPlacement.ToString() ?? "0" },
				{ "playtime", k4player.timeData?.TimeFields["all"].ToString() ?? "0" },
			};

			string? playerIp = k4player.Controller.IpAddress;

			Task.Run(async () =>
			{
				string country = await GetPlayerCountryCodeAsync(playerIp);
				placeholders.Add("country", country);

				callback(ReplaceTextPlaceholders(text, placeholders));
			});
		}

		private string ReplaceTextPlaceholders(string text, Dictionary<string, string> placeholders)
		{
			foreach (var placeholder in placeholders)
			{
				text = text.Replace($"{{{placeholder.Key}}}", placeholder.Value);
			}
			return text;
		}
	}
}