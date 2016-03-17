using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("ChatAPI", "Prefix", "0.2.0")]
    public class ChatAPI : RustLegacyPlugin
    {
        [PluginReference]
        Plugin simplemute;
		
		public string ChatTag = "[ChatAPI]";
		static string FormatTag = "{0} ({1}) {2}";
		static string FormatMessage = "{0}{1}";
		
		class ChatPerson
		{
			public string displayName;
			public string prefix;
			public int prefix_pr = 0; // Prefix priority
			public string suffix;
			public int suffix_pr = 0; // Suffix priority
			public string chatcolor = "[color #ffffff]";
			public int chatcolor_pr = 0; // Suffix priority

			public ChatPerson(string _displayName, string _prefix = "", string _suffix = "", string _chatcolor = "[color #ffffff]")
			{
				displayName = _displayName;
				prefix = _prefix;
				suffix = _suffix;
				chatcolor = _chatcolor;
			}
			
		}
		
		Dictionary<NetUser, ChatPerson> ChatPersonData = new Dictionary<NetUser, ChatPerson>();
		
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
            CheckCfg<string>("Chat: Player tag", ref FormatTag);
			CheckCfg<string>("Chat: Player message", ref FormatMessage);
			SaveConfig();
		}
		
        void Loaded()
        {
            if (simplemute == null)
            {
                Puts("Use simple mute to mute players! http://oxidemod.org/plugins/simple-mute.999/");
                return;
            } else {
				Puts("ChatAPI uses simplemute for mutes");
			}
        }
		
		bool OnPlayerChat(NetUser netuser, string message)
		{
			object obj = Interface.CallHook("ChatAPIPlayerChat", netuser, message);
			bool strip = true;
			if (obj is bool) {
				if((bool)obj == false) {
					return false;
				}
			}
			if(obj is string) {
				message = (string)obj;
				strip = false;
			}
			if(simplemute == null) {
				if(SendMessage(netuser, message, strip)) {
					return false;
				}
			} else {
				Object muted = simplemute.Call("IsMuted", netuser);
				if(muted is bool) {
					bool isMuted = (bool)muted;
					
					if (isMuted) {
						rust.SendChatMessage(netuser, ChatTag, "[color red]You are muted!");
						return false;
					}
				}
				if(SendMessage(netuser, message, strip)) {
					return false;
				}
			}
			return true;
		}
		
		bool SendMessage(NetUser netuser, string message, bool strip = true) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				string tag = string.Format(FormatTag, cp.prefix, cp.displayName, cp.suffix).Trim();
				if(strip) {
					message = StripBBCode(message);
				}
				string msg = string.Format(FormatMessage, cp.chatcolor, message).Trim();
				rust.BroadcastChat(tag, msg);
				Puts(tag + " " + StripBBCode(message));
				return true;
			}
			return false;
		}
		
		string StripBBCode(string bbCode)
		{
			string r = Regex.Replace(bbCode,
			@"\[(.*?)\]",
			String.Empty, RegexOptions.IgnoreCase);

			return r;
		}
		
		void OnPlayerConnected(NetUser netuser)
		{
			if (!(ChatPersonData.ContainsKey(netuser))) {
				ChatPerson cp = new ChatPerson(netuser.displayName);
				ChatPersonData.Add(netuser, cp);
			}
		}
		
		void OnPlayerDisconected(uLink.NetworkPlayer networkPlayer)
		{
			NetUser netuser = (NetUser)networkPlayer.GetLocalData();
			if(ChatPersonData.ContainsKey(netuser)) {
				ChatPersonData.Remove(netuser);
			}
		}
		
		object getCP(NetUser netuser) {
			if (ChatPersonData.ContainsKey(netuser)) {
				return ChatPersonData[netuser];
			}
			ChatPerson cp = new ChatPerson(netuser.displayName);
			ChatPersonData.Add(netuser, cp);
			return ChatPersonData[netuser];
		}
		
		object getPrefix(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				return cp.prefix;
			}
			return false;
		}
		
		bool setPrefix(NetUser netuser, string prefix, int priority = 0) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				if(priority >= cp.prefix_pr) {
					cp.prefix = prefix;
					cp.prefix_pr = priority;
					return true;
				}
			}
			return false;
		}
		
		bool resetPrefix(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				cp.prefix = "";
				cp.prefix_pr = 0;
				return true;
			}
			return false;
		}
		
		object getSuffix(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				return cp.suffix;
			}
			return false;
		}
		
		bool setSuffix(NetUser netuser, string suffix, int priority = 0) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				if(priority >= cp.suffix_pr) {
					cp.suffix = suffix;
					cp.suffix_pr = priority;
					return true;
				}
			}
			return false;
		}
		
		bool resetSuffix(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				cp.suffix = "";
				cp.suffix_pr = 0;
				return true;
			}
			return false;
		}
		
		object getDisplayName(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				return cp.displayName;
			}
			return false;
		}
		
		bool setDisplayName(NetUser netuser, string DisplayName) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				cp.displayName = DisplayName;
				return true;
			}
			return false;
		}
		
		bool resetDisplayName(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				cp.displayName = netuser.displayName;
				return true;
			}
			return false;
		}
		
		object getChatColor(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				return cp.chatcolor;
			}
			return false;
		}
		
		bool setChatColor(NetUser netuser, string chatcolor, int priority = 0) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				if(priority >= cp.suffix_pr) {
					cp.chatcolor = chatcolor;
					cp.chatcolor_pr = priority;
					return true;
				}
			}
			return false;
		}
		
		bool resetChatColor(NetUser netuser) {
			if(getCP(netuser) is ChatPerson) {
				ChatPerson cp = (ChatPerson)getCP(netuser);
				cp.chatcolor = "[color white]";
				cp.chatcolor_pr = 0;
				return true;
			}
			return false;
		}
		
	}
}