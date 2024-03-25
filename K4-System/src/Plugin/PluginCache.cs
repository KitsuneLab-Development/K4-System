
namespace K4System
{
	using CounterStrikeSharp.API.Core;

	using static K4System.ModuleRank;
	using static K4System.ModuleStat;
	using static K4System.ModuleTime;

	public class PlayerCacheData
	{
		public RankData? rankData { get; set; }
		public StatData? statData { get; set; }
		public TimeData? timeData { get; set; }
	}

	public class PlayerCache
	{
		private static readonly Lazy<PlayerCache> lazy = new Lazy<PlayerCache>(() => new PlayerCache());
		public static PlayerCache Instance => lazy.Value;

		private Dictionary<ulong, PlayerCacheData> cache = new Dictionary<ulong, PlayerCacheData>();

		private PlayerCache() { }

		public Dictionary<ulong, PlayerCacheData> Cache
		{
			get { return cache; }
		}

		private bool IsValidPlayer(CCSPlayerController player)
		{
			return player is { IsValid: true, IsHLTV: false, IsBot: false, UserId: not null };
		}

		public bool ContainsPlayer(CCSPlayerController player)
		{
			if (IsValidPlayer(player))
			{
				return ContainsPlayer(player.SteamID);
			}
			else
				throw new ArgumentException("The player is invalid");
		}

		public bool ContainsPlayer(ulong steamID)
		{
			if (steamID.ToString().Length != 17)
				return false;

			return cache.ContainsKey(steamID);
		}

		public PlayerCacheData GetPlayerData(CCSPlayerController player)
		{
			if (IsValidPlayer(player))
			{
				return GetPlayerData(player.SteamID);
			}
			else
				throw new ArgumentException("The player is invalid");
		}

		public PlayerCacheData GetPlayerData(ulong steamID)
		{
			if (steamID.ToString().Length != 17)
				throw new ArgumentException("The player is invalid");

			return cache[steamID];
		}

		public void AddOrUpdatePlayer(CCSPlayerController player, PlayerCacheData data)
		{
			if (IsValidPlayer(player))
			{
				AddOrUpdatePlayer(player.SteamID, data);
			}
			else
				throw new ArgumentException("The player is invalid");
		}

		public void AddOrUpdatePlayer(ulong steamID, PlayerCacheData data)
		{
			if (steamID.ToString().Length != 17)
				return;

			cache[steamID] = data;
		}

		public bool RemovePlayer(CCSPlayerController player)
		{
			if (IsValidPlayer(player))
			{
				return RemovePlayer(player.SteamID);
			}
			else
				throw new ArgumentException("The player is invalid");
		}

		public bool RemovePlayer(ulong steamID)
		{
			if (steamID.ToString().Length != 17)
				return false;

			return cache.Remove(steamID);
		}
	}
}