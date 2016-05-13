// Reference: Google.ProtocolBuffers
using System;
using Oxide.Core;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("DeadPlayersAPI", "Prefix", "0.1.0")]
	class DeadPlayersAPI : RustLegacyPlugin
	{
		List<NetUser> deadplayers = new List<NetUser>();

		void OnKilled(TakeDamage takedamage, DamageEvent damage)
		{
			if (!(takedamage is HumanBodyTakeDamage)) return;
			if (damage.victim.client == null) return;
			NetUser victim = damage.victim.client?.netUser;
			if (victim == null) return;
			if(!deadplayers.Contains(victim)) deadplayers.Add(victim);
		}

		void OnPlayerSpawn(PlayerClient client, bool usecamp, RustProto.Avatar avatar)
		{
			NetUser netuser = client.netUser;
			if(netuser == null) return;
			if(deadplayers.Contains(netuser)) deadplayers.Remove(netuser);
		}

		private void OnPlayerDisconnected(uLink.NetworkPlayer player)
		{
			NetUser netuser = player.GetLocalData() as NetUser;
			if (netuser == null) return;
			if (deadplayers.Contains(netuser)) deadplayers.Remove(netuser);
		}

		public bool IsDead(NetUser netuser) {
			if(deadplayers.Contains(netuser)) return true;
			return false;
		}

	}
}