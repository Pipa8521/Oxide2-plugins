using System;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Report", "Prefix", "1.0.0")]
	class Report : RustLegacyPlugin
	{
		const string ChatTag = "Report";
		const string MyPerm = "report.cansee";
		
		void Init() {
			if (!permission.PermissionExists(MyPerm)) permission.RegisterPermission(MyPerm, this);
		}
		[ChatCommand("report")]
		void Command_Report(NetUser netuser, string command, string[] args)
		{
			if(args.Length < 2) { rust.SendChatMessage(netuser, ChatTag, "[color red]Use /report nick reason - to report someone."); return; }
			
			NetUser target = rust.FindPlayer(args[0]);
			if (target == null) { rust.SendChatMessage(netuser, ChatTag, string.Format("Player {0} was not found!", rust.QuoteSafe(args[0]))); return; }
			args[0] = null;
			string reason = StringArrayToString(args);
			if(PlayerClient.All.Count > 0) {
				var netusers = PlayerClient.All.Select(pc => pc.netUser).ToList();
				for (int i = 0; i < netusers.Count; i++)
				{
					if(permission.UserHasPermission(netusers[i].userID.ToString(), MyPerm))
					rust.SendChatMessage(netusers[i], ChatTag, "Player {0} reported {1} for {2}", rust.QuoteSafe(netuser.displayName), rust.QuoteSafe(target.displayName), reason);
				}
			}
			
		} 
		static string StringArrayToString(string[] array)
		{
			string result = string.Join(" ", array.Where(item => !string.IsNullOrEmpty(item)).ToArray());
			return result;
		}
	}
}