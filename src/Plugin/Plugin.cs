namespace K4System
{
    using Microsoft.Extensions.Logging;
    using MySqlConnector;

    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;
    using CounterStrikeSharp.API;

    [MinimumApiVersion(153)]
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

            //** ? Database Connection Init */

            DatabaseSettings databaseSettings = config.DatabaseSettings;

            Database.Instance.Initialize(
                server: databaseSettings.Host,
                database: databaseSettings.Database,
                userId: databaseSettings.Username,
                password: databaseSettings.Password,
                port: databaseSettings.Port,
                sslMode: databaseSettings.Sslmode,
                usePooling: true,
                minPoolSize: 2,
                maxPoolSize: 2);

            //** ? Save Config */

            this.Config = config;
        }

        public override void Load(bool hotReload)
        {
            _ModuleDirectory = ModuleDirectory;

            //** ? Core */

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

            Database.Instance.AdjustDatabasePooling();
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

            SaveAllPlayersCache();

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
                    `lastseen` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `kills` INT NOT NULL DEFAULT 0,
                    `firstblood` INT NOT NULL DEFAULT 0,
                    `deaths` INT NOT NULL DEFAULT 0,
                    `assists` INT NOT NULL DEFAULT 0,
                    `shoots` INT NOT NULL DEFAULT 0,
                    `hits_taken` INT NOT NULL DEFAULT 0,
                    `hits_given` INT NOT NULL DEFAULT 0,
                    `headshots` INT NOT NULL DEFAULT 0,
                    `grenades` INT NOT NULL DEFAULT 0,
                    `mvp` INT NOT NULL DEFAULT 0,
                    `round_win` INT NOT NULL DEFAULT 0,
                    `round_lose` INT NOT NULL DEFAULT 0,
                    `game_win` INT NOT NULL DEFAULT 0,
                    `game_lose` INT NOT NULL DEFAULT 0,
                    UNIQUE (`steam_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            string ranksModuleTable = $@"CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4ranks` (
                    `steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' UNIQUE NOT NULL,
                    `name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
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

            await Database.Instance.ExecuteWithTransactionAsync(async (connection, transaction) =>
            {
                MySqlCommand? command1 = new MySqlCommand(timesModuleTable, connection, transaction);
                await command1.ExecuteNonQueryAsync();

                MySqlCommand? command2 = new MySqlCommand(statsModuleTable, connection, transaction);
                await command2.ExecuteNonQueryAsync();

                MySqlCommand? command3 = new MySqlCommand(ranksModuleTable, connection, transaction);
                await command3.ExecuteNonQueryAsync();

                if (Config.GeneralSettings.LevelRanksCompatibility)
                {
                    MySqlCommand? command4 = new MySqlCommand(lvlranksModuleTable, connection, transaction);
                    await command4.ExecuteNonQueryAsync();
                }
            });

            return true;
        }
    }
}