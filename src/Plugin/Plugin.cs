namespace K4System
{
    using Microsoft.Extensions.Logging;

    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;

    [MinimumApiVersion(153)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public required string _ModuleDirectory { get; set; }

        //** ? MODULES */
        private readonly IModuleRank ModuleRank;
        private readonly IModuleStat ModuleStat;
        private readonly IModuleTime ModuleTime;
        private readonly IModuleUtils ModuleUtils;

        //** ? HELPERS */
        private bool isDatabaseValid = false;

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

            if (config.GeneralSettings.LevelRanksCompatibility && (!config.GeneralSettings.ModuleRanks || !config.GeneralSettings.ModuleStats || !config.GeneralSettings.ModuleTimes))
            {
                base.Logger.LogWarning("LevelRanks Compatibility is enabled but one or more of the required modules are disabled. Disabling LevelRanks Compatibility.");
                config.GeneralSettings.LevelRanksCompatibility = false;
            }

            //** ? Database Connection */

            DatabaseSettings dbSettings = config.DatabaseSettings;

            string connectionString = Database.BuildConnectionString(dbSettings.Host, dbSettings.Database, dbSettings.Username, dbSettings.Password, dbSettings.Port, dbSettings.Sslmode);

            Task.Run(async () =>
            {
                try
                {
                    await Database.Instance.InitializeAsync(connectionString);
                    isDatabaseValid = true;

                    Logger.LogInformation("Database connection established.");
                }
                catch (Exception ex)
                {
                    base.Logger.LogCritical("Database connection error: {0}", ex.Message);
                }
            }).Wait();

            //** ? Save Config */

            this.Config = config;
        }

        public override void Load(bool hotReload)
        {
            _ModuleDirectory = ModuleDirectory;

            if (!isDatabaseValid)
            {
                base.Logger.LogCritical("Plugin load has been terminated due to a database connection error. Please check your configuration and try again.");
                return;
            }

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

            ThreadHelper.ExecuteAsync(CreateMultipleTablesAsync, () =>
            {
                if (hotReload)
                {
                    //** ? Load Player Caches */

                    LoadAllPlayersCache();
                }
            });
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

        public async Task CreateMultipleTablesAsync()
        {
            string timesModuleTable = @$"CREATE TABLE IF NOT EXISTS `{this.Config.DatabaseSettings.TablePrefix}k4times` (
					`id` INT AUTO_INCREMENT PRIMARY KEY,
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
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
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
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
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

            try
            {
                await Database.Instance.BeginTransactionAsync();

                // Execute the CREATE TABLE statements within a transaction
                await Database.Instance.ExecuteNonQueryAsync(timesModuleTable);
                await Database.Instance.ExecuteNonQueryAsync(statsModuleTable);
                await Database.Instance.ExecuteNonQueryAsync(ranksModuleTable);

                if (Config.GeneralSettings.LevelRanksCompatibility)
                    await Database.Instance.ExecuteNonQueryAsync(lvlranksModuleTable);

                // Commit the transaction
                await Database.Instance.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                // Roll back the transaction in case of an error
                Logger.LogError("Error creating tables: {0}", ex.Message);
                await Database.Instance.RollbackTransactionAsync();
            }
        }

    }
}