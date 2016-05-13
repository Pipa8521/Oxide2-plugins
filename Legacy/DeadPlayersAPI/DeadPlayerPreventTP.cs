using System;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("DeadPlayerPreventTP", "Prefix", "0.1.0")]
    class DeadPlayerPreventTP : RustLegacyPlugin
    {
        [PluginReference]
        Plugin DeadPlayersAPI;

        object canTeleport(NetUser netuser)
        {
            if (DeadPlayersAPI == null) return null;
            var isdead = DeadPlayersAPI?.Call("IsDead", netuser);
            if (isdead != null && (bool)isdead) return false;
            return null;
        }
    }
}
 