namespace K4System
{
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;

    using Microsoft.Extensions.Logging;
    using MySqlConnector;
    using Nexd.MySQL;

    [MinimumApiVersion(142)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public required MySqlDb Database { get; set; }
        public required string _ModuleDirectory { get; set; }

        //** ? MODULES */
        private readonly IModuleRank ModuleRank;
        private readonly IModuleStat ModuleStat;
        private readonly IModuleTime ModuleTime;

        public Plugin(ModuleRank moduleRank, ModuleStat moduleStat, ModuleTime moduleTime)
        {
            this.ModuleRank = moduleRank;
            this.ModuleStat = moduleStat;
            this.ModuleTime = moduleTime;
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

            DatabaseSettings dbSettings = config.DatabaseSettings;

            string connectionString = @$"Server={dbSettings.Host};Database={dbSettings.Database};Port={dbSettings.Port};Uid={dbSettings.Username};Password={dbSettings.Password};SslMode=none;";

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                }
            }
            catch (MySqlException ex)
            {
                base.Logger.LogError($"Connection to the MySQL database failed: {ex.Message}");
            }

            //** ? Database Connection */

            Database = new MySqlDb(dbSettings.Host, dbSettings.Username, dbSettings.Password, dbSettings.Database, dbSettings.Port);

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

            //** ? Initialize LevelRanks Compatibility Table */

            if (Config.GeneralSettings.LevelRanksCompatibility)
            {
                this.Database.ExecuteNonQueryAsync(@$"CREATE TABLE IF NOT EXISTS `lvl_base` (
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
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
            }
        }

        public override void Unload(bool hotReload)
        {
            //** ? Release Modules */

            if (Config.GeneralSettings.ModuleRanks)
                this.ModuleRank.Release(hotReload);

            if (Config.GeneralSettings.ModuleStats)
                this.ModuleStat.Release(hotReload);

            if (Config.GeneralSettings.ModuleTimes)
                this.ModuleTime.Release(hotReload);
        }
    }
}