namespace K4System
{
	using K4SharedApi;
	using static K4System.ModuleRank;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Capabilities;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;

	public sealed partial class Plugin : BasePlugin
	{
		public static PlayerCapability<IK4SharedApi> Capability_SharedAPI { get; } = new("k4-system:sharedapi");

		public void Initialize_API()
		{
			Capabilities.RegisterPlayerCapability(Capability_SharedAPI, player => new PlayerAPIHandler(player));
		}
	}

	public class PlayerAPIHandler : IK4SharedApi
	{
		private readonly CCSPlayerController _player;
		private readonly PlayerCacheData? _playerCache;

		public PlayerAPIHandler(CCSPlayerController player)
		{
			_player = player;

			if (PlayerCache.Instance.ContainsPlayer(_player))
				_playerCache = PlayerCache.Instance.GetPlayerData(_player);
		}

		public int PlayerPoints
		{
			get => GetPlayerPoints();
		}

		public int PlayerRankID
		{
			get => GetPlayerRankID();
		}

		public int GetPlayerPoints()
		{
			if (_playerCache?.rankData is null)
				return 0;

			return _playerCache.rankData.Points;
		}

		public int GetPlayerRankID()
		{
			if (_playerCache?.rankData is null)
				return 0;

			return _playerCache.rankData.Rank.Id + 1;
		}
	}
}