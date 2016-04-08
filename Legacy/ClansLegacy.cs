// Reference: Oxide.Ext.MySql

using System.Text;

using Oxide.Core;
using Oxide.Ext.MySql;
using Oxide.Core.Plugins;
using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using UnityEngine;


namespace Oxide.Plugins
{
	[Info("ClansLegacy", "Prefix", "0.1.0")]
	public class ClansLegacy : RustLegacyPlugin
	{
		
		[PluginReference]
		Plugin ChatAPI;
		
		private readonly Ext.MySql.Libraries.MySql _mySql = Interface.GetMod().GetLibrary<Ext.MySql.Libraries.MySql>();
		private Connection _mySqlConnection;
		private Sql sql;
		
		static int mysqlPort = 3306;
		static string mysqlHost = "localhost";
		static string mysqlUsername = "username";
		static string mysqlDatabase = "databasename";
		static string mysqlPass = "password";
		static string ChatTag = "[Clans] ";
		static int defaultRole = 0;
		static int leaderRole = 100;
		static bool chatapienabled = false;
		static bool chatapiusesuffix = true;
		static int chatapipriority = 10;
		
		class Clan
		{
			public int id;
			public string fullName;
			public string shortName;
			public string description;
			public int creationDate;
			public int updateDate;
			List<NetUser> members = new List<NetUser>();

			public Clan(int _id, string _fullName, string _shortName, string _description, int _creationDate, int _updateDate)
			{
				id = _id;
				fullName = _fullName;
				shortName = _shortName;
				description = _description;
				creationDate = _creationDate;
				updateDate =  _updateDate;
			}
			
			public int CountOnlinePl() {
				return members.Count;
			}
			public void addPlayer(NetUser netuser) {
				if (!(members.Contains(netuser))) {
					members.Add(netuser);
				}
			}
			public void removePlayer(NetUser netuser) {
				if (members.Contains(netuser)) {
					members.Remove(netuser);
				}
			}
			public List<NetUser> getList() {
				return members;
			}
		}
		
		class ClanPlayer
		{
			public int id;
			public string playername;
			public ulong steamid;
			public Clan clan;
			public int role;

			public ClanPlayer(string _playername, ulong _steamid, Clan _clan, int _role)
			{
				playername = _playername;
				steamid = _steamid;
				clan = _clan;
				role = _role;
			}
		}
		
		// Dictionaries for clans
		Dictionary<int, Clan> ClanData = new Dictionary<int, Clan>();
		Dictionary<NetUser, ClanPlayer> ClanPlayerData = new Dictionary<NetUser, ClanPlayer>();
		Dictionary<NetUser, List<Clan>> Invites = new Dictionary<NetUser, List<Clan>>();

		// MySQL Queries
		// I put in every single query Q prefix because I don't want them to mix with functions
		
		// We will call this when player will connect.
		private const string QLoadPlayer = "SELECT `id`, `playername`, `steamid`,`clanid`,`role` FROM `clanplayers` WHERE `steamid` = '{0}';";
		// We will call this when we want all clanplayers who has specific clan id and fixed value for NetUser
		private const string QKickPlayerQuery = "SELECT `id`, `playername`, `steamid`,`clanid`,`role`, '{0}' as `netid` FROM `clanplayers` WHERE `clanid` = {1} ORDER BY `role` DESC, `joined`;";
		// Calling this query on KickPlayer
		private const string QBestCandidateForLeader = "SELECT `id`, `playername`, `steamid`,`clanid`,`role`, '{0}' as `netid` FROM `clanplayers` WHERE `clanid` = {2} AND `steamid` != '{0}' ORDER BY `role` DESC, `joined` DESC LIMIT 1;";
		// Let's call this after player disconnects, leaves clan, or gets better role.
		private const string QUpdatePlayerAll = "UPDATE `clanplayers` SET playername`='{1}',`clanid`={2},`role`={3} WHERE `steamid` = '{0}'";
		// Let's call this when we update player role
		private const string QUpdatePlayerRole = "UPDATE `clanplayers` SET `role`={1} WHERE `steamid` = '{0}'";
		// Let's call this query after we want to update player name after he connects to the server.
		private const string QUpdatePlayerName = "UPDATE `clanplayers` SET playername`='{1}' WHERE `steamid` = '{0}'";
		// Let's call this when somebody made a clan
		private const string QCreateClan = "INSERT INTO `clans`(`name`, `shortname`, `description`, `created`, `updated`) VALUES ('{0}','{1}','{2}',{3},{4})";
		// Let's call this when we want to get all clans
		private const string QLoadClans = "SELECT `id`, `name`, `shortname`, `description`, `created`, `updated` FROM `clans` ";
		// Let's call this when we want to get clan by short&full names
		private const string QLoadClanByName = "SELECT `id`, `name`, `shortname`, `description`, `created`, `updated` FROM `clans` WHERE `name` = '{0}' AND `shortname` = '{1}'";
		// Let's call this when we want to check if clan exists by shortname or full name
		private const string QExistClanByFullShortNames = "SELECT EXISTS( SELECT * FROM `clans` WHERE `name` =  '{0}' AND `shortname` = '{1}') as exist";
		// Let's call this when we want to check if clan exists by shortname or full name
		private const string QExistPlayerInDatabase = "SELECT EXISTS( SELECT * FROM `clanplayers` WHERE `steamid` =  '{0}') as exist";
		// Let's call this when we want new player to database
		private const string QInsertPlayer = "INSERT INTO `clanplayers` (`playername`, `steamid`, `clanid`, `role`) VALUES ('{0}','{1}',{2},{3})";
		// Let's call this when we want new player to database of new clan
		private const string QInsertPlayerNewClan = "INSERT INTO `clanplayers` (`playername`, `steamid`, `clanid`, `role`) VALUES ('{0}','{1}',(SELECT `id` FROM `clans` WHERE `name` = '{2}'),{3})";
		// Let's call this when we want to delete all players from clan
		private const string QDeleteAllPlayersFromClan = "DELETE FROM `clanplayers` WHERE `clanid` = {0}";       
		// Let's call this when we want to delete clan by name
		private const string QDeleteClanByName = "DELETE FROM `clans` WHERE `name` = '{0}'";
		//  Let's call this when we want to delete clan by id
		private const string QDeleteClanById = "DELETE FROM `clans` WHERE `id` = {0}";
		//  Let's call this when we want to delete player from clan
		private const string QDeletePlayerFromClan = "DELETE FROM `clanplayers` WHERE `steamid` = '{0}'";
		
		
		// STrings and stuff parts ended
		
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
			CheckCfg<int>("Mysql: port", ref mysqlPort);
			CheckCfg<string>("Mysql: host", ref mysqlHost);
			CheckCfg<string>("Mysql: username", ref mysqlUsername);
			CheckCfg<string>("Mysql: database", ref mysqlDatabase);
			CheckCfg<string>("Mysql: password", ref mysqlPass);
			CheckCfg<bool>("ChatAPI: enabled", ref chatapienabled);
			CheckCfg<bool>("ChatAPI: use suffix instead of prefix", ref chatapiusesuffix);
			CheckCfg<int>("ChatAPI: priority for prefix or suffix", ref chatapipriority);
			SaveConfig();
			
			_mySqlConnection = _mySql.OpenDb(mysqlHost, mysqlPort, mysqlDatabase, mysqlUsername, mysqlPass, this);
			
			sql = _mySql.NewSql();

			sql = Ext.MySql.Sql.Builder.Append(@"CREATE TABLE IF NOT EXISTS `clans` (
									`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
									`name` varchar(64) COLLATE utf8_unicode_ci NOT NULL UNIQUE KEY,
									`shortname` varchar(3) COLLATE utf8_unicode_ci NOT NULL UNIQUE KEY,
									`description` varchar(64) COLLATE utf8_unicode_ci NOT NULL,
									`created` int(11) NOT NULL,
									`updated` int(11) NOT NULL
									) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;");

			_mySql.Query(sql, _mySqlConnection, cb => { return; } );
			
			sql = _mySql.NewSql();
			
			sql = Ext.MySql.Sql.Builder.Append(@"CREATE TABLE IF NOT EXISTS clanplayers (
										id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
										playername VARCHAR(64) NOT NULL,
										steamid varchar(64) NOT NULL UNIQUE KEY,
										clanid int(3),
										role int(3),
										joined int(11),
										lastseen int(11)
									) ENGINE=MyISAM DEFAULT CHARSET=utf8;");
			
			_mySql.Query(sql, _mySqlConnection, cb => { return; } );
			
			// Lets load all clans!
			
			sql = _mySql.NewSql();

			sql = Ext.MySql.Sql.Builder.Append(QLoadClans);
			
			_mySql.Query(sql, _mySqlConnection, cb => {
				if (cb == null) {
					Puts("0 clans loaded!");
				} else {
					int i = 0;
					foreach (var entry in cb) {
						//`id`, `name`, `shortname`, `description`, `created`, `updated`
						int id = Int32.Parse(entry["id"].ToString());
						string name = entry["name"].ToString();
						string shortname = entry["shortname"].ToString();
						string description = entry["description"].ToString();
						int created = Int32.Parse(entry["created"].ToString());
						int updated = Int32.Parse(entry["updated"].ToString());
						Clan clan = new Clan(id, name, shortname, description, created, updated);
						ClanData.Add(id, clan);
						object obj = Interface.CallHook("OnClanLoaded", clan);
						i++;
					}
					Puts(string.Format("{0} clans loaded", i));
				}
				return;
			} 
			);
			
			// If loaded mannualy
			if(PlayerClient.All.Count > 0) {
				var netusers = PlayerClient.All.Select(pc => pc.netUser).ToList();
				for (int i = 0; i < netusers.Count; i++)
				{
					resetTag(netusers[i]);
					CheckPlayerClan(netusers[i]);
					i++;
				}
			}
			
		}	
		
		void Loaded()
		{
			if(chatapienabled) {
				if (ChatAPI == null)
				{
					Puts("You need to install http://oxidemod.org/plugins/chatapi.1768/ to see prefix or suffix in chat");
				} else {
					Puts("Clans uses ChatAPI for suffix or prefix");
				}
			}
		}
		
		bool chatapiworking() {
			if(chatapienabled) {
				if (ChatAPI == null)
				{
					return false;
				} else {
					return true;
				}
			}
			return false;
		}
		
		void sendClanMessage(object clan, string tag, string message) {
			Clan clanas = (Clan)clan;
			List<NetUser> members = clanas.getList();
			bool notEmpty = members.Any();
			if(notEmpty) {
				foreach (NetUser netuser in members)
				{
					rust.SendChatMessage(netuser, tag, message);
				}
			}
		}
		
		//connect
		void OnPlayerConnected(NetUser netuser)
		{
			if (ClanPlayerData.ContainsKey(netuser)) UpdatePlayerClan(netuser);
			else CheckPlayerClan(netuser);
		}
		
		void UpdatePlayerClan(NetUser netuser) {
			
		}
		
		//disconnect
		void OnPlayerDisconected(uLink.NetworkPlayer networkPlayer)
		{
			NetUser netuser = (NetUser)networkPlayer.GetLocalData();
			RemoveNetUser(netuser);
		}
		
		void RemoveNetUser(NetUser netuser) {
			if(ClanPlayerData.ContainsKey(netuser)) {
				if(getPlayerClan(netuser) is Clan) {
					Clan clan = (Clan)getPlayerClan(netuser);
					clan.removePlayer(netuser);
				}
				ClanPlayerData.Remove(netuser);
			}
		}
		
		/**
		** Get Player's Clan
		**/
		
		object getPlayerClan(NetUser netuser) {
			if(ClanPlayerData.ContainsKey(netuser)) {
				return ClanPlayerData[netuser].clan;
			}
			return false;
		}
		
		/**
		** Get ClanPlayer
		**/
		
		object getClanPlayer(NetUser netuser) {
			if(ClanPlayerData.ContainsKey(netuser)) {
				return ClanPlayerData[netuser];
			}
			return false;
		}
		
		object getClanByID(int id) {
			if(ClanData.ContainsKey(id)) {
				return ClanData[id];
			}
			return false;
		}
		
		void CheckPlayerClan(NetUser netuser) {
			
			sql = _mySql.NewSql();
			string query = string.Format(QLoadPlayer, netuser.userID);
			sql = Ext.MySql.Sql.Builder.Append(query);
			
			_mySql.Query(sql, _mySqlConnection, cb => {
				if (cb == null) {
					rust.SendChatMessage(netuser, "It seems that you don't have a clan yet, use /clans to join or create one.");
				} else {
					foreach (var entry in cb) {
						//`id`, `playername`, `steamid`,`clanid`,`role`
						
						int id = Int32.Parse(entry["id"].ToString());
						ulong steamid = UInt64.Parse(entry["steamid"].ToString());
						NetUser netuseris = rust.FindPlayer(steamid.ToString());
						string playername = MySqlEscape(netuseris.displayName.ToString());
						if(!(playername.Equals(entry["playername"]))) {
							sql = _mySql.NewSql();
							string querys = string.Format(QUpdatePlayerName, netuseris.userID, playername);
							sql = Ext.MySql.Sql.Builder.Append(querys);

							_mySql.Query(sql, _mySqlConnection, cb2 => { return; } );
						}
						int cid = Int32.Parse(entry["clanid"].ToString());
						int role = Int32.Parse(entry["role"].ToString());
						
						ClanPlayer cplayer = new ClanPlayer(playername, steamid, ClanData[cid], role);
						ClanPlayerData.Add(netuser, cplayer);
						object ccc = getClanByID(cid);
						if (ccc is Clan) {
							Clan clan = (Clan)ccc;
							clan.addPlayer(netuseris);
							object obj = Interface.CallHook("OnClanPlayerConnect", clan, netuseris);
							
							if (chatapiworking()) {
								string action = "setSuffix";
								if(!chatapiusesuffix) {
									action = "setPrefix";
								}
								ChatAPI.Call(action, netuser, clan.shortName, chatapipriority);
							}
							
						} else {
							Puts(string.Format("Failed assign user to clan id: {0}", cid));
						}
						
						// some stuff later adding dictionary support with custom class
						
					}
				}
				
			});
			
		}
		
		void onChatApiPlayerLoad(NetUser netuser) {
			object ob = getPlayerClan(netuser);
			if (ob is Clan) {
				Clan clan = (Clan)ob;
				if (chatapiworking()) {
					string action = "setSuffix";
					if(!chatapiusesuffix) {
						action = "setPrefix";
					}
					ChatAPI.Call(action, netuser, clan.shortName, chatapipriority);
				}
			}
		}

		[ChatCommand("clans")]
		void Command_Clans(NetUser netuser, string command, string[] args)
		{
			CommandH_Clans(netuser, command, args);
		}
		[ChatCommand("clan")]
		void Command_Clan(NetUser netuser, string command, string[] args)
		{
			CommandH_Clans(netuser, command, args);
		}
		[ChatCommand("c")]
		void Command_C(NetUser netuser, string command, string[] args)
		{
			object clan = getPlayerClan(netuser);
			if(clan is Clan) {
				if (args.Length == 0 ) {
					rust.SendChatMessage(netuser, ChatTag, "[color red]Usage: [color white]/c Message you want to pass to your clanmates.");
				} else {
					//Clan clan = (Clan)getPlayerClan(netuser);
					if (clan == null) {
						rust.SendChatMessage(netuser, ChatTag, "Can't send message at the momment.. Something bad with your clan.");
					} else {
						string message = ConvertStringArrayToString(args);
						Clan rclan = (Clan)clan;
						string shortname = rclan.shortName;
						string stag = string.Format("{0}> Clan({1})", netuser.displayName, shortname);
						sendClanMessage(clan, stag, message);
						Puts(string.Format("{0} => {1}", stag, message));
					}
				}
			} else {
				rust.SendChatMessage(netuser, ChatTag, "You don't belong to any clan");
			}
		}
		
		List<String> HelpPage(NetUser netuser, string command, string[] args) {
			List<string> message = new List<string>();
			
			message.Add("[color white]---- [color red]Clans by Prefix [color white]----");
			if (getPlayerClan(netuser) is Clan) {
				Clan clan = (Clan)getPlayerClan(netuser);
				message.Add(string.Format("You belong to clan: {0}", clan.fullName));
			} else {
				message.Add("You don't belong to any clan");
			}
			object prec = Interface.CallHook("OnClanHelpPreCommands", netuser, command, args);
			if (prec is List<String>) {
				message.AddRange((List<String>)prec);
			}
			
			message.Add("[color white]---- [color red]Commands [color white]----");
			message.Add(string.Format("/{0} create {1} {2}" , command, rust.QuoteSafe("FullClanName"), rust.QuoteSafe("ShortClanName")));
			message.Add(string.Format("/{0} list", command));
			message.Add(string.Format("/{0} online", command));
			message.Add(string.Format("/{0} info", command));
			message.Add(string.Format("/{0} leave", command));
			
			message.Add(string.Format("/{0} join {1}", command, rust.QuoteSafe("FullClanName")));
			
			object commands = Interface.CallHook("OnClanHelpCommands", netuser, command, args);
			if (commands is List<String>) {
				message.AddRange((List<String>)commands);
			}
			
			return message;
		}
		
		void CommandH_Clans(NetUser netuser, string command, string[] args) {
			
			int n;
			bool isNumeric = false;
			if(args.Length == 1) {
				isNumeric = int.TryParse(args[0], out n);
			}
			
			if (args.Length == 0 || isNumeric) {
				
				List<string> nonPaginatedList = HelpPage(netuser, command, args);
				int recordsPerPage = 7;
				int total = (nonPaginatedList.Count)/(recordsPerPage);
				int page = 1;
				if(args.Length == 1) {
					isNumeric = int.TryParse(args[0], out n);
					page = n;
				}
				List<string> paginatedList = GetPaginatedList(nonPaginatedList, page, recordsPerPage);
				int nextpage = page+1;
				foreach (string msg in paginatedList) {
					rust.SendChatMessage(netuser, ChatTag, msg);
				}
				if(nextpage < total) {
					rust.SendChatMessage(netuser, ChatTag, string.Format("[color white]/{0} {1}", command, nextpage));
				}
				rust.SendChatMessage(netuser, ChatTag, string.Format("[color white]Page {0}/{1}", page, total));

			} else {
				if(args[0].Equals("list")) {
					rust.SendChatMessage(netuser, ChatTag, "[color white]---- [color red]Clans list [color white]----");
				} else if (args[0].Equals("online")) {
					rust.SendChatMessage(netuser, ChatTag, "[color white]---- [color red]Clan online list [color white]----");
					object ob = getPlayerClan(netuser);
					if (ob is Clan) {
						Clan clan = (Clan)ob;
						List<NetUser> list = clan.getList().ToList();
						foreach(NetUser user in list) {
							rust.SendChatMessage(netuser, ChatTag, string.Format("[color white]{0}", user.displayName));
						}
					} else {
						rust.SendChatMessage(netuser, ChatTag, "You don't belong to any clan.");
					}
				} else if (args[0].Equals("info")) {
					rust.SendChatMessage(netuser, ChatTag, "[color white]---- [color red]Clan information [color white]----");
				} else if (args[0].Equals("leave")) {
					rust.SendChatMessage(netuser, ChatTag, "[color white]---- [color red]Leave your clan [color white]----");
					object ob = getPlayerClan(netuser);
					if (ob is Clan) {
						Clan clan = (Clan)ob;
						KickFromClan(netuser, clan);
					} else {
						rust.SendChatMessage(netuser, ChatTag, "[color red]You aren't in any clan!");
					}
				} else if(args[0].Equals("create")) {
					if(args.Length == 3) {
						if (getPlayerClan(netuser) is Clan) {
							rust.SendChatMessage(netuser, ChatTag, "[color red]You already have clan! Leave your current clan to make new one!");
							return;
						} else if (args[1].Length > 12) {
							rust.SendChatMessage(netuser, ChatTag, "[color red]Full Clan Name must be less than 12 symbols");
							return;
						} else if (args[2].Length > 4) {
							rust.SendChatMessage(netuser, ChatTag, "[color red]Short Clan Name must be less than 4 symbols");
							return;
						} else if (args[1].Length <= 2) {
							rust.SendChatMessage(netuser, ChatTag, "[color red]Full Clan Name must be more than 2 symbols");
							return;
						} else if (args[2].Length <= 0) {
							rust.SendChatMessage(netuser, ChatTag, "[color red]Short Clan Name must be more than 0 symbols");
							return;
						} else if (Clan_FName_Exist(MySqlEscape(args[1]))) {
							rust.SendChatMessage(netuser, ChatTag, "[color red]Full name of clan is already taken");
							return;
						} else if (Clan_SName_Exist(MySqlEscape(args[2]))) {
							rust.SendChatMessage(netuser, ChatTag, "[color red]Short name of clan is already taken");
							return;
						}
						
						sql = _mySql.NewSql();
						sql = Ext.MySql.Sql.Builder.Append(string.Format(QExistClanByFullShortNames, MySqlEscape(args[1]), MySqlEscape(args[2])));
						_mySql.Query(sql, _mySqlConnection, cb2 => {
							if(netuser == null)
							return;
							
							bool exist = false;
							
							if(cb2 == null) {
								exist = false;
							} else {
								foreach(var value in cb2) {
									if(value["exist"].ToString().Equals("1")) {
										exist = true;
										break;
									}
								}
							}
							
							if(exist) {
								rust.SendChatMessage(netuser, ChatTag, "[color red]FullName or Shortname of clan is already taken!");
								return;
							}
							
							bool exist2 = false;
							sql = _mySql.NewSql();
							sql = Ext.MySql.Sql.Builder.Append(string.Format(QExistPlayerInDatabase, netuser.userID.ToString()));
							
							_mySql.Query(sql, _mySqlConnection, cb => {
								if(netuser == null)
								return;
								if(cb2 == null) {
									exist2 = false;
								} else {
									foreach(var value in cb2) {
										if(value["exist"].ToString().Equals("1")) {
											exist2 = true;
											break;
										}
									}
								}
								
								if(exist2) {
									rust.SendChatMessage(netuser, ChatTag, "[color red]You seem to be already in database, try reconnecting and leaving your current clan!");
									return;
								}
								
								object ctry = Interface.CallHook("OnClanCreationTry", netuser, command, args);
								if (ctry is bool) {
									if((bool)ctry == false)
									return;
								}
								rust.BroadcastChat(ChatTag, string.Format("[color red]{0}[color white] made a new clan called {1} ({2})!", netuser.displayName, MySqlEscape(args[1]), MySqlEscape(args[2])));
								NewClan(netuser, MySqlEscape(args[1]), MySqlEscape(args[2]));
								
							});
							
							return; 
							
						});
						


						

					} else {
						rust.SendChatMessage(netuser, ChatTag, string.Format("[color red]Usage:[color white] /{0} {1} {2} {3}", command, args[0], rust.QuoteSafe("FullClanName"), rust.QuoteSafe("ShortClanName")));
					}
				} else if (args[0].Equals("join")) {
					rust.SendChatMessage(netuser, ChatTag, "[color white]---- [color red]Join to clan [color white]----");
					if (getPlayerClan(netuser) is Clan) {
						rust.SendChatMessage(netuser, ChatTag, "[color red]You already have clan! Leave your current clan to join one!");
						return;
					} else if(args.Length == 1) {
						rust.SendChatMessage(netuser, ChatTag, string.Format("/{0} {1} {2}", command, args[0], rust.QuoteSafe("FullName")));
						return;
					}
					object ob = getClanByFullName(MySqlEscape(args[1]));
					if (ob is bool) {
						rust.SendChatMessage(netuser, ChatTag, string.Format("[color red]Clan {0} doesn't exist.", args[1]));
						return;
					}
					
					Clan clan = (Clan)ob;
					
					if(!Invites[netuser].Contains(clan)) {
						rust.SendChatMessage(netuser, ChatTag, string.Format("[color red]{0} haven't invited to join them!", clan.fullName));
						return;
					}
					
					object ctry = Interface.CallHook("OnClanJoin", netuser, clan);
					if (ctry is bool) {
						if((bool)ctry == false)
						return;
					}
					
					// Okey all passed.
					AddToClan(netuser, clan);
					// Remove invite
					Invites[netuser].Remove(clan);
					
				} else {
					object ccmd = Interface.CallHook("OnClanCommand", netuser, command, args);
					if (ccmd == null) {
						rust.SendChatMessage(netuser, ChatTag, "[color red]Unknown sub-command!");
					}
				}
			}
		}
		
		
		void KickFromClan(NetUser netuser, Clan clan) {
			sql = _mySql.NewSql();
			//	private const string QKickPlayerQuery = "SELECT `id`, `playername`, `steamid`,`clanid`,`role`, '{0}' as `netid` FROM `clanplayers` WHERE `clanid` = {1} ORDER BY `role` DESC, `joined`;";
			string query = string.Format(QKickPlayerQuery, netuser.userID, clan.id);
			sql = Ext.MySql.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb => {
				if (cb == null) {
					Puts("Trying to kick player from Clan, while clan doesn't exists in database?");
					return;
				} else {
					var first = cb.ElementAt(0);
					NetUser netuseris = rust.FindPlayer((string)first["netid"].ToString());
					if(netuseris == null)
					return;
					
					int id = Int32.Parse(first["id"].ToString());
					object ob = getClanPlayer(netuseris);
					if (ob is bool) {
						Puts("NetUser doesn't have ClanPlayer? Failure....");
						return;
					}
					object ob2 = getPlayerClan(netuseris);
					if (ob2 is bool) {
						Puts("NetUser doesn't have Clan? Failure....");
						return;
					}
					int players = cb.Count;
					Clan cl = (Clan)ob2;
					ClanPlayer cp = (ClanPlayer)ob;
					// If kicked player is a leader
					if(cp.role == leaderRole) {
						// If there are more players in clan
						if(players > 1) {
							var entry = cb.ElementAt(1);
							if(entry == null)
							return;
							var newleader = rust.FindPlayer((string)entry["steamid"].ToString());
							// If player is connected
							if(newleader != null) {
								object ob3 = getClanPlayer(newleader);
								if (ob3 is bool) {
									Puts("New leader doesn't have ClanPlayer? Failure....");
									return;
								}
								ClanPlayer cp_newleader = (ClanPlayer)ob3;
								cp_newleader.role = leaderRole;
								rust.SendChatMessage(newleader, ChatTag, string.Format("[color red]You are now leader of {0}", cl.fullName));
							}
							
							// Update role in mysql
							
							sql = _mySql.NewSql();
							sql = Ext.MySql.Sql.Builder.Append(string.Format(QUpdatePlayerRole, netuseris.userID, leaderRole));
							_mySql.Query(sql, _mySqlConnection, cb2 => { return; } );
							
							// New leader part gone now let's make player be free
							
							SimpleKickOut(netuseris.userID);
							
							rust.BroadcastChat(string.Format("[color green]{0} left clan {1} and leader of clan now is {2}", netuseris.displayName, cl.fullName, entry["playername"]));
							
						} else {
							DestroyClan(id);
							rust.BroadcastChat(string.Format("[color green]{0} disbanded {1} clan.", netuseris.displayName, cl.fullName));
						}
					} else {
						
						if (players > 1) {
							SimpleKickOut(netuseris.userID);
							rust.SendChatMessage(netuseris, ChatTag, string.Format("You left your clan."));
						} else {
							DestroyClan(id);
							rust.BroadcastChat(string.Format("[color green]{0} disbanded {1} clan.", netuseris.displayName, cl.fullName));
						}
					}

				}
				
			});
		}
		
		bool DestroyClan(int cid) {
			object ob = getClanByID(cid);
			if(ob is bool) {
				Puts(string.Format("Tried to remove Clan ID: {0}, but it doesn't exists!", cid));
				
				//Atleast try to remove from mysql
				sql = _mySql.NewSql();
				sql = Ext.MySql.Sql.Builder.Append(string.Format(QDeleteClanById, cid));
				_mySql.Query(sql, _mySqlConnection, cb2 => { return; } );
				
				return false;
			}
			Clan clan = (Clan)ob;
			
			// If there are any ONLINE members!
			List<NetUser> listas = clan.getList();
			bool notEmpty = listas.Any();
			if(notEmpty) {
				foreach (NetUser netuser in listas.ToList())
				{
					SimpleKickOut(netuser.userID, false, false);
				}
			}
			
			sql = _mySql.NewSql();
			sql = Ext.MySql.Sql.Builder.Append(string.Format(QDeleteAllPlayersFromClan, cid));
			_mySql.Query(sql, _mySqlConnection, cb2 => { return; } );
			
			sql = _mySql.NewSql();
			sql = Ext.MySql.Sql.Builder.Append(string.Format(QDeleteClanById, cid));
			_mySql.Query(sql, _mySqlConnection, cb2 => { return; } );
			
			ClanData.Remove(cid);
			
			return true;
			
		}
		
		void SimpleKickOut(ulong id, bool msg = true, bool mysql = true) {
			var netuser = rust.FindPlayer((string)id.ToString());
			// remove from database
			if(mysql) {
				sql = _mySql.NewSql();
				sql = Ext.MySql.Sql.Builder.Append(string.Format(QDeletePlayerFromClan, id));
				_mySql.Query(sql, _mySqlConnection, cb2 => { return; } );
			}
			// removing only entries
			if (netuser != null) {
				object ob = getPlayerClan(netuser);
				if(ob is Clan) {
					Clan clan = (Clan)ob;
					resetTag(netuser);
					RemoveNetUser(netuser);
					if(msg) {
						sendClanMessage(clan, "-", string.Format("[color red]{0} has left your clan.", netuser.displayName));
					}
				}
			}
		}
		
		void AddToClan(NetUser netuser, Clan clan) {
			sql = _mySql.NewSql();
			string query = string.Format(QInsertPlayer, MySqlEscape(netuser.displayName), netuser.userID, clan.id, defaultRole);
			sql = Ext.MySql.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb => { return; });	
			InsertToClanOnlinePlayer(netuser, clan.id, defaultRole);
			
		}
		

		
		int UnixTime() {
			Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
			return unixTimestamp;
		}
		void NewClan(NetUser netuser, string fullname, string shortname) {
			CreateClan(netuser, fullname, shortname);
			InsertPlayerToNewClan(netuser, fullname);
			object ob = getClanByFullName(fullname);
			if (ob is Clan) {
				Clan clan = (Clan) ob;
				InsertToClanOnlinePlayer(netuser, clan.id, leaderRole);
			}
			
		}
		
		void CreateClan(NetUser netuser, string fullname, string sname) {
			
			sql = _mySql.NewSql();
			string query = string.Format(QCreateClan, MySqlEscape(fullname), MySqlEscape(sname), "No description provided", UnixTime(), UnixTime());
			sql = Ext.MySql.Sql.Builder.Append(query);
			
			_mySql.Query(sql, _mySqlConnection, cb => {
				if (cb == null) {
					rust.SendChatMessage(netuser, ChatTag, "[color red]Failed to create clan.");
					return;
				}
				sql = _mySql.NewSql();
				string query2 = string.Format(QLoadClanByName, MySqlEscape(fullname), MySqlEscape(sname));
				sql = Ext.MySql.Sql.Builder.Append(query2);
				_mySql.Query(sql, _mySqlConnection, cb2 => {
					if (cb2 == null) 
					return;
					var entry = cb2.ElementAt(0);
					if(entry == null)
					return;
					
					int id = Int32.Parse(entry["id"].ToString());
					string name = entry["name"].ToString();
					string shortname = entry["shortname"].ToString();
					string description = entry["description"].ToString();
					int created = Int32.Parse(entry["created"].ToString());
					int updated = Int32.Parse(entry["updated"].ToString());
					Clan clan = new Clan(id, name, shortname, description, created, updated);
					ClanData.Add(id, clan);
					object obj = Interface.CallHook("OnClanLoaded", clan);
				});

			});
			

			
		}
		
		void InsertPlayerToNewClan(NetUser netuser, string fullname) {
			sql = _mySql.NewSql();
			string query = string.Format(QInsertPlayerNewClan, MySqlEscape(netuser.displayName), netuser.userID, fullname, leaderRole);
			sql = Ext.MySql.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb => {
				if (cb == null) {
					rust.SendChatMessage(netuser, ChatTag, "[color red]Failed to insert into new clan.");
				}
			});	
		}
		
		void InsertToClanOnlinePlayer(NetUser netuser, int cid, int role){
			object ob = getClanByID(cid);
			if (ob is Clan) {
				Clan clan = (Clan)ob;
				ClanPlayer cplayer = new ClanPlayer(MySqlEscape(netuser.displayName), netuser.userID, clan, role);
				ClanPlayerData.Add(netuser, cplayer);
				clan.addPlayer(netuser);
				object obj = Interface.CallHook("OnInsertPlayerToClan", clan, netuser);
				
				if (chatapiworking()) {
					string action = "setSuffix";
					if(!chatapiusesuffix) {
						action = "setPrefix";
					}
					ChatAPI.Call(action, netuser, clan.shortName, chatapipriority);
				}
				
			}
		}
		
		bool Clan_FName_Exist(string name) {
			bool found = false;
			foreach(KeyValuePair<int, Clan> entry in ClanData)
			{
				if (entry.Value.fullName.Equals(name)) {
					found = true;
					break;
				}
			}
			return found;
		}
		
		object getClanByFullName(string name) {
			if(Clan_FName_Exist(name)) {
				Clan clan;
				foreach(KeyValuePair<int, Clan> entry in ClanData)
				{
					if (entry.Value.fullName.Equals(name)) {
						clan = entry.Value;
						break;
					}
				}
			}
			return false;
		}
		
		bool Clan_SName_Exist(string name) {
			bool found = false;
			foreach(KeyValuePair<int, Clan> entry in ClanData)
			{
				if (entry.Value.shortName.Equals(name)) {
					found = true;
					break;
				}
			}
			return found;
		}
		
		object getClanByShortName(string name) {
			if(Clan_SName_Exist(name)) {
				Clan clan;
				foreach(KeyValuePair<int, Clan> entry in ClanData)
				{
					if (entry.Value.shortName.Equals(name)) {
						clan = entry.Value;
						break;
					}
				}
			}
			return false;
		}
		
		string MySqlEscape(string usString)
		{
			if (usString == null)
			{
				return null;
			}
			// SQL Encoding for MySQL Recommended here:
			// http://au.php.net/manual/en/function.mysql-real-escape-string.php
			// it escapes \r, \n, \x00, \x1a, baskslash, single quotes, and double quotes
			return Regex.Replace(usString, @"[\r\n\x00\x1a\\'""]", @"\$0");
		}
		
		static string ConvertStringArrayToString(string[] array, int from = 0)
		{
			//
			// Concatenate all the elements into a StringBuilder.
			//
			StringBuilder builder = new StringBuilder();
			int i = 0;
			foreach (string value in array)
			{
				if(i >= from) {
					builder.Append(value);
					builder.Append(' ');
				}
				i++;
			}
			return builder.ToString();
		}
		// http://www.codelocker.net/96/c-sharp-dot-net-take-a-list-of-any-object-and-paginate-it/
		public static List<T> GetPaginatedList<T>(IList<T> source, int page, int recordsPerPage)
		{
			int startIndex = 0;
			int endIndex = 0;
			
			if (page <= 0)
			{
				page = 1;
			}
			
			if (recordsPerPage <= 0)
			{
				startIndex = 0;
				endIndex = Int32.MaxValue;
			}
			else
			{
				startIndex = (page * recordsPerPage) - recordsPerPage;
				endIndex = (page * recordsPerPage) - 1;
			}
			
			//Cap end Index
			if (endIndex > source.Count - 1)
			{
				endIndex = source.Count - 1;
			}
			
			List<T> newList = new List<T>();
			for (int x = startIndex; x <= endIndex; x++)
			{
				newList.Add((T)source[x]);
			}
			
			return newList;
		}
		void resetTag(NetUser netuser) {
			if (chatapiworking()) {
				string action = "resetSuffix";
				if(!chatapiusesuffix) {
					action = "resetPrefix";
				}
				ChatAPI.Call(action, netuser);
			}
		}
		
	}
}