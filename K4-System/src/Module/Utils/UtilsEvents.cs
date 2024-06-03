
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using K4System.Models;

	public partial class ModuleUtils : IModuleUtils
	{
		public void Initialize_Events()
		{
			if (Config.UtilSettings.DisconnectMessageEnable)
			{
				plugin.RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
				{
					K4Player? k4player = plugin.GetK4Player(@event.Userid);

					if (k4player is null || !k4player.IsPlayer)
						return HookResult.Continue;

					plugin.ReplacePlaceholders(k4player, plugin.Localizer["k4.announcement.disconnect"], (result) =>
					{
						Server.NextWorldUpdate(() =>
						{
							if (result != null)
								Server.NextWorldUpdate(() => Server.PrintToChatAll(result));
						});
					});

					return HookResult.Continue;
				});
			}
		}
	}
}