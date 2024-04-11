namespace K4System
{
    using Microsoft.Extensions.Logging;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;
    using CounterStrikeSharp.API;
    using K4System.Models;
    using Dapper;

    [MinimumApiVersion(200)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public required string _ModuleDirectory { get; set; }
        public CCSGameRules? GameRules = null;

        //** ? MODULES */
        private readonly IModuleRank ModuleRank;
        private readonly IModuleStat ModuleStat;
        private readonly IModuleTime ModuleTime;
        private readonly IModuleUtils ModuleUtils;

        public List<K4Player> K4Players = new List<K4Player>();

        public Plugin(ModuleRank moduleRank, ModuleStat moduleStat, ModuleTime moduleTime, ModuleUtils moduleUtils)
        {
            this.ModuleRank = moduleRank;
            this.ModuleStat = moduleStat;
            this.ModuleTime = moduleTime;
            this.ModuleUtils = moduleUtils;
        }

        public void OnConfigParsed(PluginConfig config)
        {
            if (config.Version < Config.Version)
            {
                base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);
            }

            //** ? Save Config */

            this.Config = config;
        }

        public override void Load(bool hotReload)
        {
            _ModuleDirectory = ModuleDirectory;

            //** ? Core */

            Initialize_API();
            Initialize_Events();
            Initialize_Commands();

            //** ? Initialize Modules */

            if (Config.GeneralSettings.ModuleRanks)
                this.ModuleRank.Initialize(hotReload);

            if (Config.GeneralSettings.ModuleStats)
                this.ModuleStat.Initialize(hotReload);

            if (Config.GeneralSettings.ModuleTimes)
                this.ModuleTime.Initialize(hotReload);


            if (Config.GeneralSettings.ModuleUtils)
                this.ModuleUtils.Initialize(hotReload);

            //** ? Initialize Database tables */

            Task.Run(CreateMultipleTablesAsync).Wait();

            if (hotReload)
            {
                //** ? Load Player Caches */

                LoadAllPlayersCache();

                GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
            }
        }

        public override void Unload(bool hotReload)
        {
            //** ? Save Player Caches */

            Task.Run(SaveAllPlayersDataAsync);

            //** ? Release Modules */

            if (Config.GeneralSettings.ModuleRanks)
                this.ModuleRank.Release(hotReload);

            if (Config.GeneralSettings.ModuleStats)
                this.ModuleStat.Release(hotReload);

            if (Config.GeneralSettings.ModuleTimes)
                this.ModuleTime.Release(hotReload);

            if (Config.GeneralSettings.ModuleUtils)
                this.ModuleUtils.Release(hotReload);

            this.Dispose();
        }

        public async Task<bool> CreateMultipleTablesAsync()
        {
            string timesModuleTable = @$"CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4times` (
					`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' UNIQUE NOT NULL,
					`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
                    `lastseen` DATETIME NOT NULL,
					`all` INT NOT NULL DEFAULT 0,
					`ct` INT NOT NULL DEFAULT 0,
					`t` INT NOT NULL DEFAULT 0,
					`spec` INT NOT NULL DEFAULT 0,
					`dead` INT NOT NULL DEFAULT 0,
					`alive` INT NOT NULL DEFAULT 0,
					UNIQUE (`steam_id`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            string statsModuleTable = $@"CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4stats` (
                `steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' UNIQUE NOT NULL,
                `name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
                `lastseen` DATETIME NOT NULL,
                `kills` INT NOT NULL DEFAULT 0,
                `firstblood` INT NOT NULL DEFAULT 0,
                `deaths` INT NOT NULL DEFAULT 0,
                `assists` INT NOT NULL DEFAULT 0,
                `shoots` INT NOT NULL DEFAULT 0,
                `hits_taken` INT NOT NULL DEFAULT 0,
                `hits_given` INT NOT NULL DEFAULT 0,
                `headshots` INT NOT NULL DEFAULT 0,
                `chest_hits` INT NOT NULL DEFAULT 0,
                `stomach_hits` INT NOT NULL DEFAULT 0,
                `left_arm_hits` INT NOT NULL DEFAULT 0,
                `right_arm_hits` INT NOT NULL DEFAULT 0,
                `left_leg_hits` INT NOT NULL DEFAULT 0,
                `right_leg_hits` INT NOT NULL DEFAULT 0,
                `neck_hits` INT NOT NULL DEFAULT 0,
                `unused_hits` INT NOT NULL DEFAULT 0,
                `gear_hits` INT NOT NULL DEFAULT 0,
                `special_hits` INT NOT NULL DEFAULT 0,
                `grenades` INT NOT NULL DEFAULT 0,
                `mvp` INT NOT NULL DEFAULT 0,
                `round_win` INT NOT NULL DEFAULT 0,
                `round_lose` INT NOT NULL DEFAULT 0,
                `game_win` INT NOT NULL DEFAULT 0,
                `game_lose` INT NOT NULL DEFAULT 0,
                `rounds_overall` INT NOT NULL DEFAULT 0,
                `rounds_ct` INT NOT NULL DEFAULT 0,
                `rounds_t` INT NOT NULL DEFAULT 0,
                `bomb_planted` INT NOT NULL DEFAULT 0,
                `bomb_defused` INT NOT NULL DEFAULT 0,
                `hostage_rescued` INT NOT NULL DEFAULT 0,
                `hostage_killed` INT NOT NULL DEFAULT 0,
                `noscope_kill` INT NOT NULL DEFAULT 0,
                `penetrated_kill` INT NOT NULL DEFAULT 0,
                `thrusmoke_kill` INT NOT NULL DEFAULT 0,
                `flashed_kill` INT NOT NULL DEFAULT 0,
                `dominated_kill` INT NOT NULL DEFAULT 0,
                `revenge_kill` INT NOT NULL DEFAULT 0,
                `assist_flash` INT NOT NULL DEFAULT 0,
                UNIQUE (`steam_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            string ranksModuleTable = $@"CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4ranks` (
                    `steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' UNIQUE NOT NULL,
                    `name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
                    `lastseen` DATETIME NOT NULL,
                    `rank` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
                    `points` INT NOT NULL DEFAULT 0,
                    UNIQUE (`steam_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            string lvlranksModuleTable = @$"CREATE TABLE IF NOT EXISTS `{Config.DatabaseSettings.LvLRanksTableName}` (
                    `steam` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY,
                    `name`  VARCHAR(255) COLLATE 'utf8mb4_unicode_ci',
                    `value` INT NOT NULL DEFAULT 0,
                    `rank` INT NOT NULL DEFAULT 0,
                    `kills` INT NOT NULL DEFAULT 0,
                    `deaths` INT NOT NULL DEFAULT 0,
                    `shoots` INT NOT NULL DEFAULT 0,
                    `hits` INT NOT NULL DEFAULT 0,
                    `headshots` INT NOT NULL DEFAULT 0,
                    `assists` INT NOT NULL DEFAULT 0,
                    `round_win` INT NOT NULL DEFAULT 0,
                    `round_lose` INT NOT NULL DEFAULT 0,
                    `playtime` INT NOT NULL DEFAULT 0,
                    `lastconnect` INT NOT NULL DEFAULT 0
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            using (var connection = CreateConnection(Config))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    await connection.ExecuteAsync(timesModuleTable, transaction: transaction);
                    await connection.ExecuteAsync(statsModuleTable, transaction: transaction);
                    await connection.ExecuteAsync(ranksModuleTable, transaction: transaction);

                    if (Config.GeneralSettings.LevelRanksCompatibility)
                    {
                        await connection.ExecuteAsync(lvlranksModuleTable, transaction: transaction);
                    }

                    await transaction.CommitAsync();
                }
            }

            await PurgeTableRowsAsync();
            return true;
        }
    }
}