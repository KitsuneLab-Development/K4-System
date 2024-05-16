
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using K4System.Models;

	public partial class ModuleUtils : IModuleUtils
	{
		public void Initialize_Events()
		{
			if (Config.UtilSettings.ConnectMessageEnable)
			{
				plugin.RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
				{
					K4Player? k4player = plugin.GetK4Player(@event.Userid);

					if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
						return HookResult.Continue;

					Server.PrintToChatAll(ReplacePlaceholders(k4player, plugin.ApplyPrefixColors(Config.UtilSettings.ConnectMessage)));
					return HookResult.Continue;
				});
			}

			if (Config.UtilSettings.DisconnectMessageEnable)
			{
				plugin.RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
				{
					K4Player? k4player = plugin.GetK4Player(@event.Userid);

					if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
						return HookResult.Continue;

					Server.PrintToChatAll(ReplacePlaceholders(k4player, plugin.ApplyPrefixColors(Config.UtilSettings.DisconnectMessage)));
					return HookResult.Continue;
				});
			}
		}

		public string ReplacePlaceholders(K4Player k4player, string text)
		{
			Dictionary<string, string> placeholders = new Dictionary<string, string>
			{
				{ "{name}", k4player.PlayerName },
				{ "{steamid}", k4player.SteamID.ToString() },
				{ "{clantag}", k4player.ClanTag },
				{ "{rank}", k4player.rankData?.Rank.Name ?? "Unranked" },
				{ "{country}", plugin.GetPlayerCountryCode(k4player.Controller) },
				{ "{points}", k4player.rankData?.Points.ToString() ?? "0" },
				{ "{topplacement}", k4player.rankData?.TopPlacement.ToString() ?? "0" },
				{ "{playtime}", k4player.timeData?.TimeFields["all"].ToString() ?? "0" },
			};

			foreach (var placeholder in placeholders)
			{
				text = text.Replace(placeholder.Key, placeholder.Value);
			}

			return text;
		}
	}
}