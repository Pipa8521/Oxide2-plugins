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
    [Info("Show Damage", "PreFiX", 0.1)]
    [Description("Shows damage as given and received by player")]
    public class ShowDamage : RustLegacyPlugin
    {
		private const string UNKNOWN = "Unknown";
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
			//takeDamage.playsHitNotification = false;
			if(damage.attacker.client != null) {
				if (takeDamage is HumanBodyTakeDamage) {
					if(damage.victim.client != null) {
						if (damage.attacker.client?.netUser is NetUser && damage.victim.client?.netUser is NetUser) {
							// let's grab netusers
							NetUser attacker = damage.attacker.client?.netUser;
							NetUser victim = damage.victim.client?.netUser;
							if (attacker != victim) {

								// let's grab data about weapon 
								WeaponImpact impact = damage.extraData as WeaponImpact;
								var weapon = impact?.dataBlock.name ?? UNKNOWN;
								// let's check how much dmg done
								double dmg = Math.Floor(damage.amount);
								if (dmg > 0) {
									
									// Stuff for notice
									
									var icon = "!";
									var duration = 4f;
									var weaponm = "";
									
									if (weapon != UNKNOWN) {
										weaponm = "with " + weapon;
									} else {
										// what about bow?
										if (damage.attacker.client != null )
										{
											PlayerInventory inv = attacker.playerClient.controllable.GetComponent<PlayerInventory>();
											if (inv != null && (inv.activeItem?.datablock?.name?.Contains("Bow") ?? false))
											{
												weaponm = "with " + inv.activeItem.datablock.name;
											}
										}
									}
									
									// If Attacker have enabled messages
									if (bUserOtherDamage.ContainsKey(attacker)) {
										if (bUserOtherDamage[attacker]) {
											var Amessage = "You attacked " + victim.displayName + " " + weaponm + " and made " + dmg + " dmg.";
											rust.Notice(attacker, Amessage, icon, duration);
											//rust.SendChatMessage(attacker, Amessage);
										}
									}
									// If Victim have enabled messages
									if (bUserSelfDamage.ContainsKey(victim)) {
										if (bUserSelfDamage[victim]) {
											var Vmessage = "You were attacked by " + attacker.displayName + " " + weaponm + " and he made " + dmg + " dmg.";
											rust.Notice(victim, Vmessage, icon, duration);
											//rust.SendChatMessage(victim, Vmessage);
										}
									}
									var Cmessage = attacker.displayName + " attacked " + victim.displayName + " " + weaponm  + " and made " + dmg + " dmg.";
									Puts(Cmessage);
								}
							}
						}
					}
				}
			}
		
		}
    }
}
