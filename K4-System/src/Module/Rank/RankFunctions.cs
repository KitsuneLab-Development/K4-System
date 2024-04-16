namespace K4System
{
    using MySqlConnector;

    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Modules.Admin;
    using CounterStrikeSharp.API.Modules.Utils;
    using Microsoft.Extensions.Logging;
    using System.Data;
    using K4SharedApi;
    using K4System.Models;
    using Dapper;
    using MaxMind.GeoIP2;
    using MaxMind.GeoIP2.Exceptions;

    public partial class ModuleRank : IModuleRank
    {
        public bool IsPointsAllowed()
        {
            if (plugin.GameRules == null)
                return false;

            int notBots = Utilities.GetPlayers().Count(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.SteamID.ToString().Length == 17 && p.Connected == PlayerConnectedState.PlayerConnected);

            return (!plugin.GameRules.WarmupPeriod || Config.RankSettings.WarmupPoints) && (Config.RankSettings.MinPlayers <= notBots);
        }

        public Rank GetNoneRank()
        {
            return noneRank!;
        }

        public void BeforeRoundEnd(int winnerTeam)
        {
            foreach (K4Player k4player in plugin.K4Players)
            {
                if (!k4player.IsValid || !k4player.IsPlayer)
                    continue;

                if (!k4player.Controller.PawnIsAlive || Config.RankSettings.KillstreakResetOnRoundEnd)
                    k4player.KillStreak = (0, DateTime.Now);

                RankData? playerData = k4player.rankData;

                if (playerData is null)
                    continue;

                if (!playerData.PlayedRound)
                    continue;

                if (k4player.Controller.TeamNum <= (int)CsTeam.Spectator)
                    continue;

                if (winnerTeam == k4player.Controller.TeamNum)
                {
                    ModifyPlayerPoints(k4player, Config.PointSettings.RoundWin, "k4.phrases.roundwin");
                }
                else
                {
                    ModifyPlayerPoints(k4player, Config.PointSettings.RoundLose, "k4.phrases.roundlose");
                }

                if (!playerData.MuteMessages && Config.RankSettings.RoundEndPoints)
                {
                    if (playerData.RoundPoints > 0)
                    {
                        k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.gain", playerData.RoundPoints]}");
                    }
                    else if (playerData.RoundPoints < 0)
                    {
                        k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.loss", Math.Abs(playerData.RoundPoints)]}");
                    }
                    else
                        k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.nochange"]}");
                }

                playerData.RoundPoints = 0;
            }
        }

        public Rank GetPlayerRank(int points)
        {
            return rankDictionary.LastOrDefault(kv => points >= kv.Value.Point).Value ?? noneRank!;
        }

        public void ModifyPlayerPointsConnector(CCSPlayerController player, int amount, string reason, string? extraInfo = null)
        {
            K4Player? k4player = plugin.GetK4Player(player);
            if (k4player is null)
                return;

            ModifyPlayerPoints(k4player, amount, reason, extraInfo);
        }

        public void ModifyPlayerPoints(K4Player k4player, int amount, string reason, string? extraInfo = null)
        {
            if (!IsPointsAllowed())
                return;

            if (!k4player.IsValid || !k4player.IsPlayer)
                return;

            RankData? playerData = k4player.rankData;

            if (playerData is null)
                return;

            if (Config.RankSettings.RoundEndPoints && plugin.GameRules != null && !plugin.GameRules.WarmupPeriod)
                playerData.RoundPoints += amount;

            if (amount == 0)
                return;

            if (amount > 0 && AdminManager.PlayerHasPermissions(k4player.Controller, "@k4system/vip/points-multiplier"))
            {
                amount = (int)Math.Round(amount * Config.RankSettings.VipMultiplier);
            }

            playerData.Points += amount;

            Server.NextFrame(() =>
            {
                if (!k4player.IsValid || !k4player.IsPlayer)
                    return;

                if (!playerData.MuteMessages && (!Config.RankSettings.RoundEndPoints || plugin.GameRules == null || plugin.GameRules.WarmupPeriod))
                {
                    if (amount > 0)
                    {
                        if (extraInfo != null)
                        {
                            k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.gain", playerData.Points, amount, plugin.Localizer[reason]]}{extraInfo}");
                        }
                        else
                        {
                            k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.gain", playerData.Points, amount, plugin.Localizer[reason]]}");
                        }
                    }
                    else if (amount < 0)
                    {
                        if (extraInfo != null)
                        {
                            k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.loss", playerData.Points, Math.Abs(amount), plugin.Localizer[reason]]}{extraInfo}");
                        }
                        else
                        {
                            k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.loss", playerData.Points, Math.Abs(amount), plugin.Localizer[reason]]}");
                        }
                    }
                }
            });

            if (playerData.Points < 0)
                playerData.Points = 0;

            if (Config.RankSettings.ScoreboardScoreSync)
                k4player.Controller.Score = playerData.Points;

            Rank newRank = GetPlayerRank(playerData.Points);

            if (playerData.Rank.Name != newRank.Name)
            {
                k4player.Controller.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer[playerData.Rank.Point > newRank.Point ? "k4.ranks.demote" : "k4.ranks.promote", newRank.Color, newRank.Name]}");

                if (playerData.Rank.Permissions != null && playerData.Rank.Permissions.Count > 0)
                {
                    foreach (Permission permission in playerData.Rank.Permissions)
                    {
                        AdminManager.RemovePlayerPermissions(k4player.Controller, permission.PermissionName);
                    }
                }

                if (newRank.Permissions != null)
                {
                    foreach (Permission permission in newRank.Permissions)
                    {
                        AdminManager.AddPlayerPermissions(k4player.Controller, permission.PermissionName);
                    }
                }

                playerData.Rank = newRank;
            }

            SetPlayerClanTag(k4player);
        }

        public async Task<(int playerPlace, int totalPlayers)> GetPlayerPlaceAndCountAsync(K4Player k4player)
        {
            string query = $@"SELECT
                (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`
                WHERE `points` >
                    (SELECT `points` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `steam_id` = @SteamId)) AS playerPlace,
                (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`) AS totalPlayers";

            try
            {
                using (var connection = plugin.CreateConnection(Config))
                {
                    await connection.OpenAsync();

                    var result = await connection.QueryFirstOrDefaultAsync(query, new { SteamId = k4player.SteamID });

                    if (result != null)
                    {
                        int playerPlace = (int)result.playerPlace + 1;
                        int totalPlayers = (int)result.totalPlayers;
                        return (playerPlace, totalPlayers);
                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => Logger.LogError($"A problem occurred while fetching player place and count: {ex.Message}"));
            }

            return (0, 0);
        }

        public int CalculateDynamicPoints(K4Player from, K4Player to, int amount)
        {
            if (!Config.RankSettings.DynamicDeathPoints)
                return amount;

            if (!to.IsPlayer || !from.IsPlayer)
                return amount;

            RankData? fromCache = from.rankData;
            RankData? toCache = to.rankData;

            if (fromCache is null || toCache is null)
                return amount;

            if (toCache.Points <= 0 || fromCache.Points <= 0)
                return amount;

            double pointsRatio = Math.Clamp((double)toCache.Points / fromCache.Points, Config.RankSettings.DynamicDeathPointsMinMultiplier, Config.RankSettings.DynamicDeathPointsMaxMultiplier);
            double result = pointsRatio * amount;
            return (int)Math.Round(result);
        }

        public void SetPlayerClanTag(K4Player k4player)
        {
            string tag = string.Empty;

            if (Config.RankSettings.ScoreboardClantags && k4player.rankData != null)
            {
                tag = k4player.rankData.Rank.Tag ?? $"[{k4player.rankData.Rank.Name}]";
            }

            if (k4player.rankData?.HideAdminTag == false)
            {
                foreach (AdminSettingsEntry adminSettings in Config.GeneralSettings.AdminSettingsList)
                {
                    if (adminSettings.ClanTag == null)
                        continue;

                    if (Plugin.PlayerHasPermission(k4player, adminSettings.Permission))
                    {
                        tag = adminSettings.ClanTag;
                        break;
                    }
                }
            }

            if (Config.RankSettings.CountryTagEnabled)
            {
                string countryTag = GetPlayerCountryCode(k4player.Controller);
                tag = tag.Length > 0 ? $"{countryTag} | {tag}" : countryTag;
            }

            k4player.ClanTag = tag;
        }

        public string GetPlayerCountryCode(CCSPlayerController player)
        {
            string? playerIp = player.IpAddress;

            if (playerIp == null)
                return "??";

            string[] parts = playerIp.Split(':');
            string realIP = parts.Length == 2 ? parts[0] : playerIp;

            string filePath = Path.Combine(plugin.ModuleDirectory, "GeoLite2-Country.mmdb");
            if (!File.Exists(filePath))
            {
                Logger.LogError($"GeoLite2-Country.mmdb not found in {filePath}. Download it from https://github.com/P3TERX/GeoLite.mmdb/releases and place it in the same directory as the plugin.");
                return "??";
            }

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
        }
    }
}
