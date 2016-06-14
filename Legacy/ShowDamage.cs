// Reference: Facepunch.ID

using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Show Damage", "PreFiX", "0.1.0")]
	[Description("Shows damage as given and received by player")]
	public class ShowDamage : RustLegacyPlugin
	{
		private const string UNKNOWN = "Unknown";
		// TODO: Add language support, configuration file
		// TODO: Rewrite Dictionary to List. Only need to check for .Contains
		Dictionary<NetUser,bool> bUserSelfDamage = new Dictionary<NetUser, bool>();
		Dictionary<NetUser,bool> bUserOtherDamage = new Dictionary<NetUser, bool>();
		
		void OnPlayerConnected(NetUser netuser)
		{
			bUserSelfDamage[netuser] = true;
			bUserOtherDamage[netuser] = true;
		}
		
		void OnPlayerDisconected(uLink.NetworkPlayer networkPlayer)
		{
			NetUser netuser = (NetUser)networkPlayer.GetLocalData();
			bUserSelfDamage[netuser] = false;
			bUserOtherDamage[netuser] = false;
		}
		
		[ChatCommand("selfdmg")]
		void Command_selfdmg(NetUser netuser, string command, string[] args)
		{
			if (bUserSelfDamage.ContainsKey(netuser)) {
				if (bUserSelfDamage[netuser]) {
					bUserSelfDamage[netuser] = false;
					PrintToChat(netuser, "Self-damage is disabled!");
				} else {
					bUserSelfDamage[netuser] = true;
					PrintToChat(netuser, "Self-damage is enabled!");
				}
			} else {
				bUserSelfDamage[netuser] = true;
				PrintToChat(netuser, "Self-damage is enabled!");
			}
		}
		
		[ChatCommand("otherdmg")]
		void Command_otherdmg(NetUser netuser, string command, string[] args)
		{
			if (bUserOtherDamage.ContainsKey(netuser)) {
				if (bUserOtherDamage[netuser]) {
					bUserOtherDamage[netuser] = false;
					PrintToChat(netuser, "Other-damage is disabled!");
				} else {
					bUserOtherDamage[netuser] = true;
					PrintToChat(netuser, "Other-damage is enabled!");
				}
			} else {
				bUserOtherDamage[netuser] = true;
				PrintToChat(netuser, "Other-damage is enabled!");
			}
		}
		
		void OnHurt(TakeDamage takeDamage, DamageEvent damage)
		{
			if (!(takeDamage is HumanBodyTakeDamage)) return;
			if (damage.attacker.client == null) return;
			if (damage.victim.client == null) return;
			NetUser attacker = damage.attacker.client?.netUser;
			NetUser victim = damage.victim.client?.netUser;
			if (attacker == null || victim == null) return;
			if (damage.attacker.client == damage.victim.client) return;

			WeaponImpact impact = damage.extraData as WeaponImpact;
			string weapon = impact?.dataBlock.name ?? UNKNOWN;
			double dmg = Math.Floor(damage.amount);
			if (dmg == 0) return;
			string icon = "!";
			float duration = 5f;
			string weaponm = "";
			
			if (weapon != UNKNOWN) weaponm = string.Format("with {0}", weapon);
			PlayerInventory inv = attacker.playerClient.controllable.GetComponent<PlayerInventory>();
			if (inv != null && (inv.activeItem?.datablock?.name?.Contains("Bow") ?? false)) weaponm = string.Format("with {0}", inv.activeItem.datablock.name);
			if (bUserOtherDamage.ContainsKey(attacker) && bUserOtherDamage[attacker]) rust.Notice(attacker, string.Format("You attacked {0} {1} and made {2} dmg.", victim.displayName, weaponm, dmg), icon, duration);
			if (bUserSelfDamage.ContainsKey(victim) && bUserSelfDamage[victim])  rust.Notice(victim, string.Format("You were attacked by {0} {1} and he made {2} dmg.", attacker.displayName, weaponm, dmg), icon, duration);
			Puts(string.Format("{0} attacked {1} with {2} and made {3} dmg.", attacker.displayName, victim.displayName, weaponm, dmg));
		}
	}
}