using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("AwesomeChat", "Prefix", "1.0.0")]
    class AwesomeChat : RustLegacyPlugin
    {
		string[] rainbow = { "ff00ff", "ff00cc", "ff0099", "ff0066", "ff0033", "ff0000", "ff3300", "ff6600", "ff9900", "ffcc00", "ffff00", "ccff00", "99ff00", "66ff00", "33ff00", "00ff00", "00ff33", "00ff66", "00ff99", "00ffcc", "00ffff", "00ccff", "0099ff", "0066ff", "0033ff", "0000ff", "3300ff", "6600ff", "9900ff", "cc00ff" };
		string[] ltflag = {"fdb913", "006a44", "c1272d"};
		const string fullblock = "?";
		
		Dictionary<NetUser, int> Awesome = new Dictionary<NetUser, int>();
		const string ChatTag = "AwesomeChat";
		const string MyPerm = "awesomechat.cmd";
		
		object ChatAPIPlayerChat(NetUser netuser, string message) {
			switch (Awesome[netuser]) {
				case 1:
					return RainbowText(message);
					break;
				case 2:
					return SpaceAfterLetter(message);
					break;
				case 3:
					return RainbowText(SpaceAfterLetter(message));
					break;
				case 4:
					return LithuaniaFlag(message);
					break;
				default:
					return null;
					
			}
		}
		
		void Init() {
			// Late load, refresh.
			if(PlayerClient.All.Count > 0) {
				var netusers = PlayerClient.All.Select(pc => pc.netUser).ToList();
				for (int i = 0; i < netusers.Count; i++)
				{
					Awesome.Add(netusers[i], 0);
				}
			}
			if (!permission.PermissionExists(MyPerm)) permission.RegisterPermission(MyPerm, this);
		}
		
		void OnPlayerConnected(NetUser netuser)
		{
			Awesome.Add(netuser, 0);
		}
		
		void OnPlayerDisconected(uLink.NetworkPlayer networkPlayer)
		{
			NetUser netuser = (NetUser)networkPlayer.GetLocalData();
			if(Awesome.ContainsKey(netuser)) {
				Awesome.Remove(netuser);
			}
		}
		
		
        /*[ChatCommand("flag")]
        void Command_Flag(NetUser netuser, string command, string[] args)
        {
			if(!permission.UserHasPermission(netuser.userID.ToString(), MyPerm)) {
				rust.SendChatMessage(netuser, ChatTag, "[color red]Awesome people are vips only!");
				return;
			}
			
			StringBuilder sb = new StringBuilder();
			sb.Append(string.Format("[color #{0}]", ltflag[0]));
			sb.Append(fullblock, 10);
			rust.SendChatMessage(netuser, ChatTag, sb.ToString());
			sb.setLength(0);
			sb.Append(string.Format("[color #{0}]", ltflag[1]));
			sb.Append(fullblock, 10);
			rust.SendChatMessage(netuser, ChatTag, sb.ToString());
			sb.setLength(0);
			sb.Append(string.Format("[color #{0}]", ltflag[2]));
			sb.Append(fullblock, 10);
			rust.SendChatMessage(netuser, ChatTag, sb.ToString());
			
        }*/
		
        [ChatCommand("awesome")]
        void Command_Awesome(NetUser netuser, string command, string[] args)
        {
			if(!permission.UserHasPermission(netuser.userID.ToString(), MyPerm)) {
				rust.SendChatMessage(netuser, ChatTag, "[color red]Awesome people are vips only!");
				return;
			}
			if(args.Length == 0) {
				ChatSmthBad(netuser, command);
			} else if(args.Length == 1) {
				int n;
				bool isNumeric = false;
				isNumeric = int.TryParse(args[0], out n);
				
				if(n >= 0 && n <= 4) {
					if(Awesome.ContainsKey(netuser)) {
						Awesome[netuser] = n;
					} else {
						Awesome.Add(netuser, n);
					}
					rust.SendChatMessage(netuser, ChatTag, "[color red]New mode selected!");
				} else {
					ChatSmthBad(netuser, command);
				}
			} else {
				ChatSmthBad(netuser, command);
			}
        } 
		
		void ChatSmthBad(NetUser netuser, string command) {
			rust.SendChatMessage(netuser, ChatTag, "[color red]Awesome chat meniu");
			rust.SendChatMessage(netuser, ChatTag, "----------");
			rust.SendChatMessage(netuser, ChatTag, string.Format("/{0} 0 - default chat", command));
			rust.SendChatMessage(netuser, ChatTag, string.Format("/{0} 1 - rainbow chat", command));
			rust.SendChatMessage(netuser, ChatTag, string.Format("/{0} 2 - space after every char. chat", command));
			rust.SendChatMessage(netuser, ChatTag, string.Format("/{0} 3 - space after every char. + rainbow chat", command));
			rust.SendChatMessage(netuser, ChatTag, string.Format("/{0} 4 - lithuania flag chat", command));
		}
		
		string LithuaniaFlag(string text) {
			StringBuilder sb = new StringBuilder();
			
			if(string.IsNullOrEmpty(text))
				return string.Format("");
			int length = text.Length;
			
			if(length < 3)
				return text; 
			float check = length % 3;
			int split = length/3;
			string[] tekstas = SplitToChunks(text, split);
			
			sb.Append(string.Format("[color #{0}]", ltflag[0]));
			sb.Append(tekstas[0]);
			sb.Append(string.Format("[color #{0}]", ltflag[1]));
			sb.Append(tekstas[1]);
			sb.Append(string.Format("[color #{0}]", ltflag[2]));
			if(check == 0.0f)
				sb.Append(tekstas[2]);			
			else
				sb.Append(string.Format("{0}{1}", tekstas[2], text[length-1]));	
			
			return sb.ToString();
		}
		
		//http://stackoverflow.com/questions/3008718/split-string-into-smaller-strings-by-length-variable
		public string[] SplitToChunks(string source, int maxLength)
		{
			return source
				.Where((x, i) => i % maxLength == 0)
				.Select(
					(x, i) => new string(source
						.Skip(i * maxLength)
						.Take(maxLength)
						.ToArray()))
				.ToArray();
		}
		
		string SpaceAfterLetter(string text) {
			StringBuilder sb = new StringBuilder();
			
			if(string.IsNullOrEmpty(text))
				return string.Format("");
			int length = text.Length;
			
			
			// Append to StringBuilder.
			for (int i = 0; i < length; i++)
			{
				sb.Append(text[i]).Append(" ");
			}
			
			return sb.ToString();
		}
		
		string RainbowText(string text) {
			StringBuilder sb = new StringBuilder();
			
			if(string.IsNullOrEmpty(text))
				return string.Format("");
			int length = text.Length;
			int rlength = rainbow.Length;
			int r = 0;
			for (int i = 0; i < length; i++)
			{
				if(r >= rlength)
					r = 0;
				
				sb.Append(string.Format("[color #{0}]",rainbow[r])).Append(text[i]);
				r++;
			}
			
			return sb.ToString();
		}
		
	}
}