// Reference: Facepunch.MeshBatch
// Reference: Google.ProtocolBuffers
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("TeleportGlitchFix", "Prefix", "1.0.0")]
	class TeleportGlitchFix : RustLegacyPlugin
	{
		[PluginReference]
		Plugin Share;
		
		static float distance = 50.0f;
		
		/*[ChatCommand("checkwhere")]
		void cmdCheckWhere(NetUser netuser, string command, string[] args)
		{
			object obj = ClosestBuilding(netuser);
			if(obj is float) {
				SendReply(netuser, string.Format( "Closest building is {0}", (float)obj));
			} else {
				SendReply(netuser, "There aren't any buildings in game.");
			}
		}*/
		
		void LoadDefaultConfig() { }

		private void CheckCfg<T>(string Key, ref T var)
		{
			if (Config[Key] is T)
			var = (T)Config[Key];
			else
			Config[Key] = var;
		} 
		
		void Init()
		{
			CheckCfg<float>("Distance between player and building", ref distance);
			SaveConfig();
		}
		
		object canTeleport(NetUser netuser) {
			object obj = ClosestBuilding(netuser);
			if(obj is float) {
				if ((float)obj > distance) {
					return null;
				}
				SendReply(netuser, string.Format( "Closest building is {0} there must be distance more than {1}", (float)obj, distance));
				return true;
			} else {
				SendReply(netuser, "There aren't any buildings in game.");
				return null;
			}
		}
		
		object ClosestBuilding(NetUser netuser) {
			
			PlayerClient playerclient = netuser.playerClient;
			Vector3 lastPosition = playerclient.lastKnownPosition;
			Vector2 v1 = new Vector2(lastPosition.x, lastPosition.z);
			float dist = 0.0f;
			float tempf = 0.0f;
			bool set = false;
			bool notEmpty = StructureMaster.AllStructures.Any();
			string userid = netuser.userID.ToString();
			if(notEmpty) {
				foreach (StructureMaster master in (List<StructureMaster>)StructureMaster.AllStructures)
				{
					// If building owner is netuser > skip
					if(userid.Equals(master.ownerID.ToString()))
					continue;
					// If using share plugin and it's sharing
					if(Share != null)
					{
						if((bool)Share.Call("isSharing", userid, master.ownerID.ToString()))
						{
							continue;
						}
					}
					
					Vector2 v2 = new Vector2(master.transform.position.x, master.transform.position.z);
					if (!set) {
						dist = Vector2.Distance(v1, v2);
						set = true;
					} else {
						tempf = Vector2.Distance(v1, v2);
						if (dist > tempf) {
							dist = tempf;
						}
					}
				}
				return dist;
			}
			
			return false;
		}
	}
}