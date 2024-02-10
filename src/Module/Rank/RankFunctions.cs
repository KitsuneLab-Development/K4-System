namespace K4System
{
    using MySqlConnector;

    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Modules.Admin;
    using CounterStrikeSharp.API.Modules.Utils;

    public partial class ModuleRank : IModuleRank
    {
        public bool IsPointsAllowed()
        {
            Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

            if (plugin.GameRules == null)
                return false;

            int notBots = Utilities.GetPlayers().Count(p => !p.IsBot && p.SteamID.ToString().Length == 17);

            return (!plugin.GameRules.WarmupPeriod || Config.RankSettings.WarmupPoints) && (Config.RankSettings.MinPlayers <= notBots);
        }

        public Rank GetNoneRank()
        {
            return noneRank!;
        }

        public void BeforeRoundEnd(int winnerTeam)
        {
            List<CCSPlayerController> players = Utilities.GetPlayers().Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && rankCache.ContainsPlayer(p) && p.SteamID.ToString().Length == 17).ToList();

            foreach (CCSPlayerController player in players)
            {
                if (!player.PawnIsAlive)
                    playerKillStreaks[player.Slot] = (0, DateTime.Now);

                RankData playerData = rankCache[player];

                if (!playerData.PlayedRound)
                    continue;

                if (player.TeamNum <= (int)CsTeam.Spectator)
                    continue;

                if (winnerTeam == player.TeamNum)
                {
                    ModifyPlayerPoints(player, Config.PointSettings.RoundWin, "k4.phrases.roundwin");
                }
                else
                {
                    ModifyPlayerPoints(player, Config.PointSettings.RoundLose, "k4.phrases.roundlose");
                }

                Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

                if (Config.RankSettings.RoundEndPoints)
                {
                    if (playerData.RoundPoints > 0)
                    {
                        player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.gain", playerData.RoundPoints]}");
                    }
                    else if (playerData.RoundPoints < 0)
                    {
                        player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.loss", Math.Abs(playerData.RoundPoints)]}");
                    }
                    else
                        player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.nochange"]}");
                }
            }
        }

        public void LoadRankData(int slot, int points)
        {
            RankData playerData = new RankData
            {
                Points = points,
                Rank = GetPlayerRank(points),
                PlayedRound = false,
                RoundPoints = 0
            };

            rankCache[slot] = playerData;
        }

        public Rank GetPlayerRank(int points)
        {
            return rankDictionary.LastOrDefault(kv => points >= kv.Value.Point).Value ?? noneRank!;
        }

        public void ModifyPlayerPoints(CCSPlayerController player, int amount, string reason, string? extraInfo = null)
        {
            if (!IsPointsAllowed())
                return;

            if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
                return;

            if (player.IsBot || player.IsHLTV)
                return;

            if (player.SteamID.ToString().Length == 17)
                return;

            if (!rankCache.ContainsPlayer(player))
                return;

            RankData playerData = rankCache[player];

            Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

            if (Config.RankSettings.RoundEndPoints && plugin.GameRules != null && !plugin.GameRules.WarmupPeriod)
                playerData.RoundPoints += amount;

            if (amount == 0)
                return;

            if (amount > 0 && AdminManager.PlayerHasPermissions(player, "@k4system/vip/points-multiplier"))
            {
                amount = (int)Math.Round(amount * Config.RankSettings.VipMultiplier);
            }

            int oldPoints = playerData.Points;
            Server.NextFrame(() =>
            {
                if (!Config.RankSettings.RoundEndPoints || plugin.GameRules == null || plugin.GameRules.WarmupPeriod)
                {
                    if (amount > 0)
                    {
                        if (extraInfo != null)
                        {
                            player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.gain", oldPoints, amount, plugin.Localizer[reason]]}{extraInfo}");
                        }
                        else
                        {
                            player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.gain", oldPoints, amount, plugin.Localizer[reason]]}");
                        }
                    }
                    else if (amount < 0)
                    {
                        if (extraInfo != null)
                        {
                            player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.loss", oldPoints, Math.Abs(amount), plugin.Localizer[reason]]}{extraInfo}");
                        }
                        else
                        {
                            player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.points.loss", oldPoints, Math.Abs(amount), plugin.Localizer[reason]]}");
                        }
                    }
                }
            });

            playerData.Points += amount;
            playerData.RoundPoints += amount;

            if (playerData.Points < 0)
                playerData.Points = 0;

            if (Config.RankSettings.ScoreboardScoreSync)
                player.Score = playerData.Points;

            Rank newRank = GetPlayerRank(playerData.Points);

            if (playerData.Rank.Name != newRank.Name)
            {
                player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer[playerData.Rank.Point > newRank.Point ? "k4.ranks.demote" : "k4.ranks.promote", newRank.Color, newRank.Name]}");

                if (playerData.Rank.Permissions != null && playerData.Rank.Permissions.Count > 0)
                {
                    foreach (Permission permission in playerData.Rank.Permissions)
                    {
                        AdminManager.RemovePlayerPermissions(Utilities.GetPlayerFromSlot(player.Slot), permission.PermissionName);
                    }
                }

                if (newRank.Permissions != null)
                {
                    foreach (Permission permission in newRank.Permissions)
                    {
                        AdminManager.AddPlayerPermissions(Utilities.GetPlayerFromSlot(player.Slot), permission.PermissionName);
                    }
                }

                playerData.Rank = newRank;
            }

            if (Config.RankSettings.ScoreboardRanks)
            {
                string tag = playerData.Rank.Tag ?? $"[{playerData.Rank.Name}]";
                SetPlayerClanTag(player, tag);
            }
        }

        public async Task<(int playerPlace, int totalPlayers)> GetPlayerPlaceAndCountAsync(string steamID)
        {
            string query = $@"SELECT
                (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`
                WHERE `points` >
                    (SELECT `points` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `steam_id` = @steamId)) AS playerPlace,
                (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`) AS totalPlayers";

            try
            {
                using (MySqlCommand command = new MySqlCommand(query))
                {
                    command.Parameters.AddWithValue("@steamId", steamID);

                    using (MySqlDataReader? reader = await Database.Instance.ExecuteReaderAsync(command.CommandText, command.Parameters.Cast<MySqlParameter>().ToArray()))
                    {
                        if (reader != null && reader.HasRows && await reader.ReadAsync())
                        {
                            int playerPlace = reader.GetInt32(0) + 1;
                            int totalPlayers = reader.GetInt32(1);
                            return (playerPlace, totalPlayers);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while executing GetPlayerPlaceAndCountAsync: {ex.Message}");
            }

            return (0, 0);
        }

        public int CalculateDynamicPoints(CCSPlayerController from, CCSPlayerController to, int amount)
        {
            if (!Config.RankSettings.DynamicDeathPoints || to.IsBot || from.IsBot || rankCache[to].Points <= 0 || rankCache[from].Points <= 0)
            {
                return amount;
            }

            double pointsRatio = Math.Clamp((double)rankCache[to].Points / rankCache[from].Points, Config.RankSettings.DynamicDeathPointsMinMultiplier, Config.RankSettings.DynamicDeathPointsMaxMultiplier);
            double result = pointsRatio * amount;
            return (int)Math.Round(result);
        }

        public void SetPlayerClanTag(CCSPlayerController player, string tag)
        {
            foreach (AdminSettingsEntry adminSettings in Config.GeneralSettings.AdminSettingsList)
            {
                if (adminSettings.ClanTag == null)
                    continue;

                switch (adminSettings.Permission[0])
                {
                    case '@':
                        if (AdminManager.PlayerHasPermissions(player, adminSettings.Permission))
                            tag = adminSettings.ClanTag;
                        break;
                    case '#':
                        if (AdminManager.PlayerInGroup(player, adminSettings.Permission))
                            tag = adminSettings.ClanTag;
                        break;
                    default:
                        if (AdminManager.PlayerHasCommandOverride(player, adminSettings.Permission))
                            tag = adminSettings.ClanTag;
                        break;
                }
            }

            player.Clan = tag;

            /*Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

            plugin.AddTimer(0.2f, () =>
            {
                Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
                Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
            });*/
        }
    }
}
