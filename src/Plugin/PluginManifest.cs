namespace K4System
{
    using CounterStrikeSharp.API.Core;

    public sealed partial class Plugin : BasePlugin
    {
        public override string ModuleName => "K4-System";

        public override string ModuleDescription => "A plugin that enhances the server with features such as a playtime tracker, statistical records, and player ranks.";

        public override string ModuleAuthor => "K4ryuu";

        public override string ModuleVersion => "3.3.5 " +
#if RELEASE
            "(release)";
#else
            "(debug)";
#endif
    }
}