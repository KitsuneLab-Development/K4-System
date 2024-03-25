
namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleTime : IModuleTime
	{
		public void Initialize_Events(Plugin plugin)
		{
			plugin.RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!PlayerCache.Instance.ContainsPlayer(player))
					return HookResult.Continue;

				TimeData? playerData = PlayerCache.Instance.GetPlayerData(player).timeData;

				if (playerData is null)
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;

				playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
				playerData.Times["Connect"] = now;

				if ((CsTeam)@event.Oldteam != CsTeam.None)
				{
					playerData.TimeFields[GetFieldForTeam((CsTeam)@event.Oldteam)] += (int)(now - playerData.Times["Team"]).TotalSeconds;
				}

				playerData.Times["Team"] = now;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!PlayerCache.Instance.ContainsPlayer(player))
					return HookResult.Continue;

				TimeData? playerData = PlayerCache.Instance.GetPlayerData(player).timeData;

				if (playerData is null)
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;
				playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
				playerData.Times["Connect"] = now;

				playerData.TimeFields["dead"] += (int)(now - playerData.Times["Death"]).TotalSeconds;
				playerData.Times["Death"] = DateTime.UtcNow;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!PlayerCache.Instance.ContainsPlayer(player))
					return HookResult.Continue;

				TimeData? playerData = PlayerCache.Instance.GetPlayerData(player).timeData;

				if (playerData is null)
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;
				playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
				playerData.Times["Connect"] = now;

				playerData.TimeFields["alive"] += (int)(now - playerData.Times["Death"]).TotalSeconds;
				playerData.Times["Death"] = now;

				return HookResult.Continue;
			});
		}
	}
}