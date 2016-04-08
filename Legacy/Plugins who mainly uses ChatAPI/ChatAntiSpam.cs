using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("ChatAntiSpam", "Prefix", "0.1.0")]
	public class ChatAntiSpam: RustLegacyPlugin
	{
		[PluginReference]
		Plugin ChatAPI;
		
		Dictionary<NetUser, int> LastMessage = new Dictionary<NetUser, int>();

		void Init() {
			if(ChatAPI == null) {
				Puts("Chat API not running, this plugin won't work without it http://oxidemod.org/plugins/chatapi.1768/");
			}
		}
		
		void OnPlayerConnected(NetUser netuser)
		{
			if (!(LastMessage.ContainsKey(netuser))) {
				LastMessage.Add(netuser, 0);
			}
		}
		
		void OnPlayerDisconected(uLink.NetworkPlayer networkPlayer)
		{
			NetUser netuser = (NetUser)networkPlayer.GetLocalData();
			if(LastMessage.ContainsKey(netuser)) {
				LastMessage.Remove(netuser);
			}
		}
		
		int UnixTimestamp() {
			Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
			return unixTimestamp;
		}

		object ChatAPIPlayerChat(NetUser netuser, string message) {
			int now = UnixTimestamp();
			int newtime = now+5;
			if (!(LastMessage.ContainsKey(netuser))) {
				LastMessage.Add(netuser, now+5);
				return true;
			} else if (LastMessage[netuser] < now) {
				int left = now-LastMessage[netuser];
				if(left > 0) {
					rust.SendChatMessage(netuser, string.Format("You must wait for {0} seconds to type something in chat.", left));
					return false;
				} else {
					LastMessage[netuser] = newtime;
					return true;
				}
			} else {
				LastMessage[netuser] = newtime;
			}
			return true;
		}
	
	}
}

