// Reference: Oxide.Ext.MySql
// Reference: Google.ProtocolBuffers

using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Ext.MySql;
using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using uLink;

namespace Oxide.Plugins
{
	[Info("PrefixEssentials", "Prefix", "0.1.0")]
	class PrefixEssentials : RustLegacyPlugin
	{
		
		[PluginReference]
		Plugin ChatAPI;

		[PluginReference]
		Plugin Share;

		[PluginReference]
		Plugin Location;
		
		private readonly Ext.MySql.Libraries.MySql _mySql = Interface.GetMod().GetLibrary<Ext.MySql.Libraries.MySql>();
		private Core.Database.Connection _mySqlConnection;
		private Core.Database.Sql sql;

		RustServerManagement management;

		public Vector3 zerovector = new Vector3(0, 0, 0);
		public Vector3 lastdrop = new Vector3(0, 0, 0);
		
		private const bool DEBUG = false;
		
		static int mysqlPort = 3306;
		static int newbieprotection = 0;
		static string mysqlHost = "localhost";
		static string mysqlUsername = "root";
		static string mysqlDatabase = "rust";
		static string mysqlPass = "";
		static string ChatTag = "[MyServer.com] ";
		static string DatabasePrefix = "r_";

		Timer eachminute;

		Timer shutdowntimer;
		int shutdownleft = 0;
		string shutdownreason = "";
		
		static Dictionary<string,string> PlayerTag = new Dictionary<string, string>
		{
			{"None","" },
			{"Vip","[VIP]" },
			{"ChatMod","[Chat Mod]" },
			{"Mod","[MOD]" },
			{"BestMod","[Leading MOD]" },
			{"Admin","[ADMIN]" },
		};
		
		static Dictionary<string,string> PlayerColor = new Dictionary<string, string>
		{
			{"None","#FFFFFF" },
			{"Vip","#1BA1E2" },
			{"ChatMod","#A200FF" },
			{"Mod","#8CBF26" },
			{"BestMod","#339933" },
			{"Admin","#E51400" },
		};
		
		static List<string> FPSCommands = new List<string>
		{
			{"grass.on false"}, 
			{"grass.forceredraw False"},
			{"grass.displacement True"},
			{"grass.disp_trail_seconds 0"},
			{"grass.shadowcast False"},
			{"grass.shadowreceive False"},
			{"render.level 0"},
			{"render.vsync False"},
			{"footsteps.quality 2"},
			{"gfx.ssaa False"},
			{"gfx.bloom False"},
			{"gfx.grain False"},
			{"gfx.ssao False"},
			{"gfx.tonemap False"}
		};

		int curr_notice = 0;

		static List<string> NoticeList = new List<string>
		{
			{"Message1!"}, 
			{"Your message 2"},
			{"your forum http://oxide.org"}
		};
		
		static bool usegathermulti = false;
		
		static Dictionary<string, int> GatherMulti = new Dictionary<string, int> 
		{
			{"Animal", 1},
			{"WoodPile", 1},
			{"StaticTree", 1},
			{"Rock1", 1},
			{"Rock2", 1},
			{"Rock3", 1}
		};
		
		enum Rangai
		{	None = 0,
			Vip = 1,
			ChatMod = 2,
			Mod = 3,
			BestMod = 4,
			Admin = 5
		};
		
		
		// Let's do this... Mysql queries again
		private const string QLoadPlayer = @"SELECT *,
		EXISTS( SELECT * FROM `{0}players` WHERE `nick` = '{1}' AND `steam` != '{2}') as `exist`,
		EXISTS( SELECT * FROM `{3}bans` WHERE (`nick` = '{4}' OR `steam` = '{5}' OR `ip` = '{6}') AND (`banuntil` = 0 OR `banuntil` > {7})) as `banned`,
		EXISTS( SELECT * FROM `{8}services` WHERE `steam` = '{9}' AND `expires` > {10}) as `anyservice`
		FROM `{11}players` WHERE `steam` = '{12}';";
		
		private const string QCheckNewPlayer = @"SELECT 
		EXISTS( SELECT * FROM `{0}players` WHERE `nick` = '{1}' AND `steam` != '{2}') as `exist`,
		EXISTS( SELECT * FROM `{3}bans` WHERE (`nick` = '{4}' OR `steam` = '{5}' OR `ip` = '{6}') AND (`banuntil` = 0 OR `banuntil` > {7})) as `banned`,
		EXISTS( SELECT * FROM `{8}services` WHERE `steam` = '{9}' AND `expires` > {10}) as `anyservice`";
		
		// Let's do this... Mysql queries again
		private const string QLoadServices = "SELECT * FROM `{0}services` WHERE `steam` = '{1}' AND `expires` > {2};";
		// Load service desc
		private const string QLoadAllServiceDesc = "SELECT * FROM `{0}services_desc` WHERE 1;";
		// Load service items
		private const string QLoadServiceItems = "SELECT * FROM `{0}services_items` WHERE `serviceid` = {1};";
		// Let's do this... Mysql queries again
		private const string QLoadBan = "SELECT * FROM `{0}bans` WHERE (`nick` = '{1}' OR `steam` = '{2}' OR `ip` = '{3}') AND (`banuntil` = 0 OR `banuntil` > {4});";
		// Insert ban
		private const string QInsertBan = "INSERT INTO `{0}bans`(`nick`, `steam`, `ip`, `reason`, `bantime`, `banuntil`, `anick`, `asteam`, `aip`) VALUES ('{1}', '{2}', '{3}', '{4}', {5}, {6}, '{7}', '{8}', '{9}'); SELECT LAST_INSERT_ID() as `last`;";
		// Let's call this after player disconnects, leaves clan, or gets better role.
		private const string QUpdatePlayer = "UPDATE `{0}players` SET `lastseen` = {1}, `kills` = {2}, `deaths` = {3}, `muted` = {4},  `mute_expiration` = {5}, `mute_reason` = '{6}', `newbie` = {7}, `timeplayed` = {8}, `online` = {9} WHERE `id` = {10};";
		// Let's call this query after we want to update player name after he connects to the server.
		private const string QUpdatePlayerName = "UPDATE `{0}players` SET nick`='{1}' WHERE `id` = '{2}';";
		// Let's call this when player connects first time.
		private const string QFirstTimeInsert = "INSERT INTO `{0}players`(`nick`, `steam`, `ip`, `joined`, `lastseen`, `newbie`, `firstnick`, `online`) VALUES ('{1}', '{2}', '{3}', {4}, {5}, {6}, '{7}', 1); SELECT LAST_INSERT_ID() as `last`;";
		
		class EssentialsPlayer : PrefixEssentials
		{
			public int id;
			public NetUser thisplayer { get; set; } = null;
			public string nick { get; set; } = "Unnamed";
			public string steam { get; set; } = "OnlyGodKnows";
			public Rangai admin  { get; set; } = Rangai.None;
			public string ip { get; set; } = "127.0.0.1";
			public string firstnick { get; set; } = "Unnamed";
			public int nickchangetime { get; set; } = 0;
			public int joined { get; set; } = 0;
			public int lastseen { get; set; } = 0;
			// Kiek laiko pražaide
			public int timeplayed { get; set; } = 0;
			// Topai
			public int kills { get; set; } = 0;
			public int deaths { get; set; } = 0;
			public string lastkillBy { get; set; } = "";
			public int lastkillTime  { get; set; } = 0;
			public string lastdeathBy { get; set; } = "";
			public int lastdeathTime  { get; set; } = 0;
			// Gather TOP
			//public Dictionary<string, int> gathertop = new Dictionary<string, int>();
			// Naujoko apsauga
			public int newbie { get; set; } = 0;
			// Mute
			public int muted { get; set; } = 0;
			public int mute_expiration { get; set; } = 0;
			public string mute_reason { get; set; } = "";
			public Timer thistimer;
			
			// Other stuff
			
			public int god = 0;
			
			// Services
			
			public int vip { get; set; } = 0;
			public Dictionary<string, Service> services  { get; set; } = new Dictionary<string, Service>();

			public EssentialsPlayer(int _id, NetUser tempuser)
			{
				id = _id;
				thisplayer = tempuser;
			}
			
			public void DestroyTimer() {
				thistimer.Destroy();
			}
		}
		void StartEPTimer(EssentialsPlayer ep) {
			if (ep == null) return;
			ep.thistimer = timer.Repeat(60f, 0, () =>
			{
				TimerCB(ep);
			});
		}

		void TimerCB(EssentialsPlayer ep) {
			if (ep == null) return;
			if (ep.thisplayer == null) { ep.DestroyTimer(); return; }
			if (ep.newbie == 1) {
				try { rust.SendChatMessage(ep.thisplayer, ChatTag, "Your newbie protection expired."); }
				catch (Exception ex) { Puts(ex.ToString()); }
			} 
			if (ep.newbie >= 1) ep.newbie--;
			if (ep.muted == 1 && (ep.mute_expiration > 0 && ep.mute_expiration < UnixTime())) {
				try { rust.SendChatMessage(ep.thisplayer, ChatTag, "Your mute expired, you can talk again."); }
				catch (Exception ex) { Puts(ex.ToString()); }
				ep.muted = 0;
				ep.mute_reason = "";
				ep.mute_expiration = 0;
			}
			ep.timeplayed++;
		}
		
		class Service : PrefixEssentials
		{
			public int id;
			public string steam;
			public string name;
			public int expires { get; set; } = 0;
			public int nextusage { get; set; } = 0;
			public int amount { get; set; } = 0;
			public string custom { get; set; } = "";
			public ServiceDesc desc;
			
			public Service(Dictionary<string, object> first) {
				
				id = Int32.Parse(first["id"].ToString());
				steam = first["steam"].ToString();
				name = first["name"].ToString();
				expires = Int32.Parse(first["expires"].ToString());
				nextusage = Int32.Parse(first["nextusage"].ToString());
				amount = Int32.Parse(first["amount"].ToString());
				custom = first["custom"].ToString();
				if(ServiceDescData.ContainsKey(name)) desc = ServiceDescData[name];
			}
		}
		enum ServiceType
		{	vip = 0,
			items = 1,
		};

		
		class ServiceDesc : PrefixEssentials
		{
			public int id;
			public string name { get; set; } = "BevardePaslauga";
			public decimal pricebank { get; set; } = .01m;
			public decimal pricesms { get; set; } = .01m;
			public string description { get; set; } = "";
			public ServiceType servicetype { get; set; } = ServiceType.vip;
			public int days { get; set; } = 30;
			//public Kits kit;
			
			public Dictionary<string, int> serviceitems { get; set; } = new Dictionary<string, int>();
			
			public ServiceDesc(Dictionary<string, object> first) {
				id = Int32.Parse(first["id"].ToString());
				name = first["name"].ToString();
				pricebank = decimal.Parse(first["pricebank"].ToString());
				pricesms = decimal.Parse(first["pricesms"].ToString());
				description = first["description"].ToString(); 
				servicetype = (ServiceType)Enum.Parse(typeof(ServiceType), first["servicetype"].ToString());
				days = Int32.Parse(first["days"].ToString());
			}
		}
		
		Dictionary<NetUser, EssentialsPlayer> PlayerData = new Dictionary<NetUser, EssentialsPlayer>();
		Dictionary<string, ServiceDesc> ServiceDescData = new Dictionary<string, ServiceDesc>();
		Dictionary<NetUser, Dictionary<string, int>> GatherData = new Dictionary<NetUser, Dictionary<string, int>>();
		
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
			
			CheckCfg<string>("Mysql: host", ref mysqlHost);
			CheckCfg<string>("Mysql: username", ref mysqlUsername);
			CheckCfg<string>("Mysql: database", ref mysqlDatabase);
			CheckCfg<string>("Mysql: password", ref mysqlPass);
			CheckCfg<string>("Mysql: tables prefix", ref DatabasePrefix);
			CheckCfg<int>("Mysql: port", ref mysqlPort);
			CheckCfg<string>("PrefixEssentials: Chat messages tag", ref ChatTag);
			CheckCfg<int>("PrefixEssentials: Newbie protection time", ref newbieprotection);
			CheckCfg<Dictionary<string, string>>("PrefixEssentials: Player chat prefix", ref PlayerTag);
			CheckCfg<Dictionary<string, string>>("PrefixEssentials: Player chat color", ref PlayerColor);
			CheckCfg<bool>("PrefixEssentials: Enable gather decreasion", ref usegathermulti);
			CheckCfg<Dictionary<string, int>>("PrefixEssentials: Gather decreasion rates", ref GatherMulti);
			CheckCfg<List<string>>("PrefixEssentials: FPS Commands", ref FPSCommands);
			CheckCfg<List<string>>("PrefixEssentials: Adverts", ref NoticeList);
			
			SaveConfig();

			_mySqlConnection = _mySql.OpenDb(mysqlHost, mysqlPort, mysqlDatabase, mysqlUsername, mysqlPass, this);
			
			sql = _mySql.NewSql();
			sql = Core.Database.Sql.Builder.Append(string.Format(@"CREATE TABLE IF NOT EXISTS `{0}players` (
			`id` int(10) NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`nick` varchar(64) COLLATE utf8_unicode_ci NOT NULL UNIQUE KEY,
			`steam` varchar(18) COLLATE utf8_unicode_ci NOT NULL UNIQUE KEY,
			`ip` varchar(45) NOT NULL,
			`admin` int(11) NOT NULL DEFAULT '0',
			`joined` int(10) NOT NULL DEFAULT '0',
			`lastseen` int(10) NOT NULL DEFAULT '0',
			`kills` int(11) NOT NULL DEFAULT '0',
			`deaths` int(11) NOT NULL DEFAULT '0',
			`newbie` int(11) NOT NULL DEFAULT '{1}',
			`timeplayed` int(11) NOT NULL DEFAULT '0',
			`online` int(11) NOT NULL DEFAULT '0',
			`firstnick` varchar(64) COLLATE utf8_unicode_ci NOT NULL,
			`nickchangetime` int(10) NOT NULL DEFAULT '0',
			`muted` int(11) NOT NULL DEFAULT '0',
			`mute_expiration` int(10) NOT NULL DEFAULT '0',
			`mute_reason` varchar(64) NOT NULL DEFAULT ''
			) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;", DatabasePrefix, newbieprotection));

			_mySql.Query(sql, _mySqlConnection, cb => { return; } );
			
			sql = _mySql.NewSql();
			sql = Core.Database.Sql.Builder.Append(string.Format(@"CREATE TABLE IF NOT EXISTS `{0}bans` (
			`id` int(10) NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`nick` varchar(64) COLLATE utf8_unicode_ci NOT NULL,
			`steam` varchar(18) COLLATE utf8_unicode_ci NOT NULL,
			`ip` varchar(45) COLLATE utf8_unicode_ci NOT NULL,
			`reason` varchar(128) NOT NULL DEFAULT '',
			`bantime` int(10) NOT NULL DEFAULT '0',
			`banuntil` varchar(10) NOT NULL DEFAULT '0',
			`anick` varchar(64) NOT NULL DEFAULT 'Server',
			`asteam` varchar(18) NOT NULL DEFAULT '',
			`aip` varchar(45) NOT NULL DEFAULT ''
			) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;", DatabasePrefix));
			_mySql.Query(sql, _mySqlConnection, cb => { return; } );
			
			sql = _mySql.NewSql();
			sql = Core.Database.Sql.Builder.Append(string.Format(@"CREATE TABLE IF NOT EXISTS `{0}services` (
			`id` int(10) NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`steam` varchar(18) NOT NULL,
			`name` varchar(64) NOT NULL DEFAULT '',
			`expires` int(10) NOT NULL DEFAULT '0',
			`nextusage` int(10) NOT NULL DEFAULT '0',
			`amount` int(10) NOT NULL DEFAULT '0',
			`custom` varchar(64) NOT NULL DEFAULT ''
			) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;", DatabasePrefix));
			_mySql.Query(sql, _mySqlConnection, cb => { return; } );
			
			sql = _mySql.NewSql();
			sql = Core.Database.Sql.Builder.Append(string.Format(@"CREATE TABLE IF NOT EXISTS `{0}services_desc` (
			`id` INT(10) NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`name` VARCHAR(64) NOT NULL ,
			`pricebank` DECIMAL(10,2) NOT NULL ,
			`pricesms` DECIMAL(10,2) NOT NULL ,
			`description` TEXT NOT NULL ,
			`servicetype` ENUM('vip','items') NOT NULL ,
			`days` INT(10) NOT NULL DEFAULT '30' )
			ENGINE = MyISAM CHARACTER SET utf8 COLLATE utf8_general_ci;", DatabasePrefix));
			_mySql.Query(sql, _mySqlConnection, cb => { return; } );
			
			sql = _mySql.NewSql();
			sql = Core.Database.Sql.Builder.Append(string.Format(@"CREATE TABLE IF NOT EXISTS `{0}services_items` (
			`id` INT(10) NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`serviceid` INT(10) NOT NULL,
			`item` VARCHAR(64) NOT NULL,
			`amount` INT(10) NOT NULL
			) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;", DatabasePrefix));
			_mySql.Query(sql, _mySqlConnection, cb => { return; } );

			sql = _mySql.NewSql();
			sql = Core.Database.Sql.Builder.Append(string.Format(QLoadAllServiceDesc, DatabasePrefix));
			_mySql.Query(sql, _mySqlConnection, cb => {
				if(!cb.Any()) return;
				foreach (var dd in cb)
				{
					ServiceDesc desc = new ServiceDesc(dd);
					ServiceDescData.Add(dd["name"].ToString(), desc);
					if (desc.servicetype == ServiceType.items) {
						sql = _mySql.NewSql();
						sql = Core.Database.Sql.Builder.Append(string.Format(QLoadServiceItems, DatabasePrefix, desc.id));
						_mySql.Query(sql, _mySqlConnection, cb2 => {
							if(!cb2.Any()) { Puts("{0} is {1}, but it doesn't contains any items!", desc.name, desc.servicetype.ToString()); return; }
							foreach (var list in cb2)
							{
								string skey = (string)list["item"].ToString();
								int svalue = (int)Int32.Parse((string)list["amount"].ToString());
								desc.serviceitems.Add(skey, svalue);
							}
						});
					}
				}
			} );

			if(PlayerClient.All.Count > 0) {
				List<NetUser> netusers = PlayerClient.All.Select(pc => pc.netUser).ToList();
				if(!netusers.Any()) { Puts("Found players, but couldn't make a list.... Odd..."); return; }
				foreach (NetUser netuser in netusers) MakeUserData(netuser, false);
			}
			management = RustServerManagement.Get();

			Timer_EachMinute();
			
		}

		void NoticeAll(string notice) {
			if(PlayerClient.All.Count > 0) {
				List<NetUser> netusers = PlayerClient.All.Select(pc => pc.netUser).ToList();
				if(!netusers.Any()) { Puts("Found players, but couldn't make a list.... Odd..."); return; }
				foreach (NetUser netuser in netusers) rust.Notice(netuser, notice, "!", 10f);
			}
		}

		void Timer_EachMinute() {
			if (eachminute != null) return;
			eachminute = timer.Repeat(60f, 0, () =>
			{
				// Log gather data
				if(GatherData.Any()) {
					foreach(KeyValuePair<NetUser, Dictionary<string, int>> entry in GatherData) {
						if(entry.Key == null) return;
						Puts(string.Format("In the last minute {0} collected: ", entry.Key.displayName));
						foreach(KeyValuePair<string, int> entry2 in entry.Value) {
							Puts(string.Format("{0}x {1}", entry2.Value, entry2.Key));
						}
					}
					GatherData.Clear();
				}
				if(NoticeList.Any()) {
					// Notice popus
					NoticeAll(NoticeList[curr_notice]);
					if (NoticeList.Count-1 == curr_notice) curr_notice = 0;
					else curr_notice++;
				}
			});
		}

		object ChatAPIPlayerChat(NetUser netuser, string message) {
			if (!PlayerData.ContainsKey(netuser)) return null;
			EssentialsPlayer ep = PlayerData[netuser];
			if (ep.muted == 1) {
				if (ep.mute_expiration > 0 && ep.mute_expiration < UnixTime()) {
					ep.muted = 0;
					ep.mute_reason = "";
					ep.mute_expiration = 0;
					return null;
				}
				//rust.SendChatMessage(netuser, ChatTag, "Jus negalite rašyti i chat nes esate užtildytas.");
				if(ep.mute_expiration == 0) rust.SendChatMessage(netuser, ChatTag, string.Format("You are muted forever for {0}.", ep.mute_reason));
				else 						rust.SendChatMessage(netuser, ChatTag, string.Format("You are muted until {0} for {1}.", UnixToDate(ep.mute_expiration), ep.mute_reason));
				
				return false;
			}
			return null;
		}

		void OnRunCommand(ConsoleSystem.Arg arg, bool shouldAnswer)
		{
			if (arg == null) return;
			if (arg.argUser == null) return;
			string command;
			command = arg.Class + "." + arg.Function;
			NetUser netuser = arg.argUser.connection.netUser;
			List<object> block = new List<object>() { "chat.say" };
			if(!block.Contains(command)) {
				if(!arg.Class.Equals("global")) LogToConsole(netuser, string.Format("{0} used command {1}", netuser.displayName, command));
				else 							LogToConsole(netuser, string.Format("{0} used command {1}", netuser.displayName, arg.Function));
			}
		}

		bool IsGoodName(string text) {
			Regex r = new Regex("^[a-zA-Z0-9]*$");

			if (r.IsMatch(text)) 	return true;
			else 					return false;
		}
		
		//connect
		void OnPlayerConnected(NetUser netuser)
		{
			List<string> messages = new List<string>();
			string nick = MysqlEscape(netuser.displayName);
			bool goodnick = IsGoodName(nick);

			if (nick.Length < 3) messages.Add("Your nickname is too short. It should be atleast 3 symbols.");
			if (nick.Length > 9) messages.Add("Your nickname is too long. It should max. 9 symbols.");
			if (!goodnick) messages.Add("Your nickname should be only from A-Z 0-9 a-z symbols");

			if (messages.Any()) { KickMessages(netuser, "Bad username", messages); return; }
			MakeUserData(netuser, true);
			Timer_EachMinute();
		}
		
		void OnPlayerDisconnected(uLink.NetworkPlayer networkPlayer)
		{
			NetUser netuser = (NetUser)networkPlayer.GetLocalData();
			SaveUserData(netuser, true);
			if (GatherData.ContainsKey(netuser)) GatherData.Remove(netuser);
			if (PlayerClient.All.Count == 1) { eachminute.Destroy(); GatherData.Clear(); }
		}

		void SaveUserData(NetUser netuser, bool broadcast) {
			if(!PlayerData.ContainsKey(netuser)) return;
			
			sql = _mySql.NewSql();
			string query = string.Format(QUpdatePlayer, DatabasePrefix, UnixTime(), PlayerData[netuser].kills, PlayerData[netuser].deaths, PlayerData[netuser].muted, PlayerData[netuser].mute_expiration, PlayerData[netuser].mute_reason, PlayerData[netuser].newbie, PlayerData[netuser].timeplayed, 0, PlayerData[netuser].id);
			DebugPuts(query);
			sql = Core.Database.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb2 => { return; } );
			PlayerData[netuser].DestroyTimer();
			if(broadcast) BroadcastAboutPlayer(netuser, "[color white]disconnected from the server.", true);
			PlayerData.Remove(netuser);
		}

		void Unload() {
			if(PlayerClient.All.Count > 0) {
				List<NetUser> netusers = PlayerClient.All.Select(pc => pc.netUser).ToList();
				if(!netusers.Any()) { Puts("Found players, but couldn't make a list.... Odd..."); return; }
				foreach (NetUser netuser in netusers) SaveUserData(netuser, false);
			}
		}

		void OnKilled(TakeDamage takedamage, DamageEvent damage)
		{
			if (!(takedamage is HumanBodyTakeDamage)) return;
			// Victim part
			if (damage.victim.client == null) return;

			NetUser victim = damage.victim.client?.netUser;

			if (victim == null) return;
			if (!PlayerData.ContainsKey(victim)) { MakeUserData(victim, false); return; }

			if (PlayerData[victim].admin == Rangai.None) { rust.SendChatMessage(victim, ChatTag, "Only VIP players can see them death coordinates!"); }
			else {
				Vector3 cachedVector3 = victim.playerClient.lastKnownPosition;
				var lstring = Location.Call("FindLocationName", cachedVector3);
				if(lstring is string && lstring != null) rust.SendChatMessage(victim, ChatTag, string.Format("[VIP Perk] You died {0} {1} {2} - {3}", Mathf.Ceil(cachedVector3.x).ToString(), Mathf.Ceil(cachedVector3.y).ToString(), Mathf.Ceil(cachedVector3.z).ToString(), lstring));
				else rust.SendChatMessage(victim, ChatTag, string.Format("[VIP Perk] You died {0} {1} {2}", Mathf.Ceil(cachedVector3.x).ToString(), Mathf.Ceil(cachedVector3.y).ToString(), Mathf.Ceil(cachedVector3.z).ToString()));
			}
			// Kill/Death top
			if (damage.attacker.client == null) return;
			NetUser attacker = damage.attacker.client?.netUser;
			if (attacker == null) return;
			if (damage.attacker.client == damage.victim.client) return;
			if (!PlayerData.ContainsKey(attacker)) { MakeUserData(attacker, false); return; }
			EssentialsPlayer ep_attacker = PlayerData[attacker];
			EssentialsPlayer ep_victim = PlayerData[victim];
			int now = UnixTime();
			if (ep_attacker.lastkillBy.Equals(victim.displayName) && (now-ep_attacker.lastkillTime < 300 && ep_attacker.lastkillTime > 0)) return;
			if (ep_victim.lastdeathBy.Equals(attacker.displayName) && (now-ep_victim.lastdeathTime < 120 && ep_victim.lastdeathTime > 0)) return;
			var isSharing = Share.Call("isSharing", attacker.userID.ToString(), victim.userID.ToString());
			if (isSharing is bool && (bool)isSharing) return;

			ep_attacker.lastkillTime = now;
			ep_attacker.lastkillBy = victim.displayName;
			ep_attacker.kills++;

			ep_victim.lastdeathTime = now;
			ep_victim.lastdeathBy = attacker.displayName;
			ep_victim.deaths++;

			var victimkda = "";
			var attackerkda = "";
			// Is nulio dalint negalima
			if (ep_victim.deaths > 0)	victimkda =	(ep_victim.kills/ep_victim.deaths).ToString("n2");
			else 						victimkda = ep_victim.kills.ToString("n2");

			if (ep_attacker.deaths > 0) attackerkda =(ep_attacker.kills/ep_attacker.deaths).ToString("n2");
			else 						attackerkda = ep_attacker.kills.ToString("n2");

			rust.SendChatMessage(attacker, ChatTag, string.Format("You already killed {0} and died {1} times and yours KDA are {2}", ep_attacker.kills, ep_attacker.deaths, attackerkda));
			rust.SendChatMessage(victim, ChatTag, string.Format("You already killed {0} and died {1} times and yours KDA are {2}", ep_victim.kills, ep_victim.deaths, victimkda));

		}

		
		object ModifyDamage(TakeDamage takedamage, DamageEvent damage)
		{
			if (!(takedamage is HumanBodyTakeDamage)) return null;
			if (damage.victim.client == null || damage.attacker.client == null) return null;
			if (damage.attacker.client == damage.victim.client) return null;

			NetUser victimff = damage.victim.client?.netUser;
			NetUser attackerff = damage.attacker.client?.netUser;

			if (victimff == null || attackerff == null) return null;

			if (!PlayerData.ContainsKey(attackerff)) { MakeUserData(attackerff, false); return null; }
			if (!PlayerData.ContainsKey(victimff)) { MakeUserData(victimff, false); return null; }

			if (PlayerData[victimff].god == 1) return CancelDamage(damage);

			if (PlayerData[attackerff].newbie > 0 && PlayerData[victimff].newbie == 0) {
				rust.SendChatMessage(attackerff, ChatTag, "[color red]Your newbie protection expired!");
				BroadcastAboutPlayer(attackerff, "[color red]lost his newbie protection because he attacked other player!");
				PlayerData[attackerff].newbie = 0;
				return null;
			}
			if (PlayerData[victimff].newbie > 0 && PlayerData[attackerff].god == 0) return CancelDamage(damage);

			return null;
		}

		DamageEvent CancelDamage(DamageEvent damage) {
			damage.amount = 0;
			damage.status = LifeStatus.IsAlive;
			return damage;
		}

		void OnAirdrop(Vector3 position)
		{
			lastdrop = position;
		}

		//int ITEMID = 1;
		void OnResourceNodeLoaded(ResourceTarget target) {
			
			if(!usegathermulti) return;

			NextFrame(() =>
			{
				List<ResourceGivePair> letsremovethese = new List<ResourceGivePair>();
				Dictionary<string, int> items = new Dictionary<string, int>();
				
				foreach (ResourceGivePair pair in target.resourcesAvailable) {
					string resname = pair.ResourceItemName;
					if (items.ContainsKey(resname)) items[resname] = items[resname] + pair.AmountLeft();
					else 							items.Add(resname, pair.AmountLeft());
					letsremovethese.Add(pair);
				}
				
				if (letsremovethese.Any()) {
					target.resourcesAvailable.Clear();
					letsremovethese.Clear();
				}
				
				foreach (var ff in items) {
					ResourceGivePair pairr = new ResourceGivePair();
					pairr.ResourceItemName = ff.Key;
					pairr.amountMin = ff.Value;
					pairr.amountMax = ff.Value;
					pairr.CalcAmount();
					target.resourcesAvailable.Add(pairr);
				}
				foreach (ResourceGivePair pair in target.resourcesAvailable) {
					if(pair.AnyLeft()) {
						string ss = target.type.ToString();
						int amount = (int)Math.Floor((double)(pair.AmountLeft() / GatherMulti[ss]));
						if (amount == 0) amount = 1;
						pair.Subtract(pair.AmountLeft()-amount);
					}
				}
			});
		}

		void OnGather(Inventory receiver, ResourceTarget resourceNode, ResourceGivePair resourceGivePair, int amount)
		{
			var who = receiver.GetComponent<Controllable>();
			var what = resourceNode?.type ?? ResourceTarget.ResourceTargetType.StaticTree;
			var item = resourceGivePair?.ResourceItemName ?? "Wood";
			var netuser = who.playerClient.netUser;

			if (GatherData.ContainsKey(netuser)) {
				if(GatherData[netuser].ContainsKey(item)) GatherData[netuser][item] += amount;
				else GatherData[netuser].Add(item,amount);
			} else {
				Dictionary<string, int> ditem = new Dictionary<string, int>();
				ditem.Add(item, amount);
				GatherData.Add(netuser,ditem);
			} 

			//LogToConsole(netuser, string.Format("surinko {0}x {1} iš {2}!", amount, item, what));

		}
		
		void MakeUserData(NetUser netuser, bool broadcast) {
			sql = _mySql.NewSql();

			string nick = MysqlEscape(netuser.displayName);
			string steam = netuser.userID.ToString();
			string ip = netuser.networkPlayer.ipAddress;
			int now = UnixTime();
			
			string query = string.Format(QLoadPlayer, DatabasePrefix, nick, steam, DatabasePrefix, nick, steam, ip, now, DatabasePrefix, steam, now, DatabasePrefix, steam);
			DebugPuts(query);
			sql = Core.Database.Sql.Builder.Append(query);

			_mySql.Query(sql, _mySqlConnection, cb => {
				if (cb == null || !cb.Any()) {
					DebugPuts("Player was not found");
					if (netuser != null) NewPlayer(netuser, broadcast);
				} else {
					DebugPuts("Player was found");
					DebugMysqlResult(cb);
					if (netuser != null) AddExisting(netuser, cb, broadcast);
					else DebugPuts("MakeUserData: Couldn't find netuser! Can't add!");
				}
			});
		}
		
		void AddExisting(NetUser netuser, List<Dictionary<string, object>> cb, bool broadcast) {
			DebugPuts("AddExisting: Went to the function");
			if (netuser == null) return;
			if (PlayerData.ContainsKey(netuser)) return; 
			if (!cb.Any()) return; 

			DebugPuts("AddExisting: Passed first checks");

			Dictionary<string, object> first = new Dictionary<string, object>();

			try { first = cb.ElementAt(0); }
			catch (Exception ex) { Puts(ex.ToString()); }

			if (!first.Any()) return;

			string nick = MysqlEscape(netuser.displayName);

			if (first["exist"].ToString().Equals("1")) { OtherNick(netuser, first["nick"].ToString()); return; }
			if (first["banned"].ToString().Equals("1")) { BanCheck(netuser); return; }

			EssentialsPlayer pplayer = new EssentialsPlayer(Int32.Parse(first["id"].ToString()), netuser);
			pplayer.nick = first["nick"].ToString();
			pplayer.steam = first["steam"].ToString();
			pplayer.ip = first["ip"].ToString();
			pplayer.firstnick = first["firstnick"].ToString();
			pplayer.nickchangetime = Int32.Parse(first["nickchangetime"].ToString());
			pplayer.joined = Int32.Parse(first["joined"].ToString());
			pplayer.lastseen = Int32.Parse(first["lastseen"].ToString());
			pplayer.timeplayed = Int32.Parse(first["timeplayed"].ToString());
			pplayer.kills = Int32.Parse(first["kills"].ToString());
			pplayer.deaths = Int32.Parse(first["deaths"].ToString());
			pplayer.newbie = Int32.Parse(first["newbie"].ToString());
			pplayer.muted = Int32.Parse(first["muted"].ToString());
			pplayer.mute_expiration = Int32.Parse(first["mute_expiration"].ToString());
			pplayer.mute_reason = first["mute_reason"].ToString();

			if (first["anyservice"].ToString().Equals("1")) CheckServices(netuser, pplayer);
			else pplayer.admin = (Rangai)Int32.Parse(first["admin"].ToString());

			ChatApi_Setup(netuser, pplayer.admin);

			StartEPTimer(pplayer);
			DebugPuts("AddExisting: Reached part where I need to add player");
			PlayerData.Add(netuser, pplayer);
			CheckPlayerGroups(netuser);

			if(broadcast) BroadcastAboutPlayer(netuser, "connected to the server.", true);
		}
		
		void NewPlayer(NetUser netuser, bool broadcast) {
			if (netuser != null) {
				int now = UnixTime();
				string nick = MysqlEscape(netuser.displayName);
				string steam = MysqlEscape(netuser.userID.ToString());
				string ip = netuser.networkPlayer.ipAddress;
				string queryq = string.Format(QCheckNewPlayer, DatabasePrefix, nick, steam, DatabasePrefix, nick, steam, ip, now, DatabasePrefix, steam, now);
				DebugPuts(queryq);
				sql = _mySql.NewSql();
				sql = Core.Database.Sql.Builder.Append(queryq);

				_mySql.Query(sql, _mySqlConnection, cb => {
					if (cb.Any()) {
						if (netuser != null) {

							var cc = cb.ElementAt(0);
							bool anysevice = false;

							if (cc["exist"].ToString().Equals("1")) { OtherNick(netuser, true); return; }
							if (cc["banned"].ToString().Equals("1")) { BanCheck(netuser); return; }
							if (cc["anyservice"].ToString().Equals("1")) anysevice = true;

							sql = _mySql.NewSql();
							string query = string.Format(QFirstTimeInsert, DatabasePrefix, MysqlEscape(netuser.displayName.ToString()), MysqlEscape(netuser.userID.ToString()), netuser.networkPlayer.ipAddress, now, now, newbieprotection, MysqlEscape(netuser.displayName));
							DebugPuts(query);
							sql = Core.Database.Sql.Builder.Append(query);
							_mySql.Query(sql, _mySqlConnection, cb2 => {
								if (cb2.Any() && netuser != null) {
									DebugPuts("--------------");
									DebugMysqlResult(cb2);
									DebugPuts("--------------");
									var first = cb2.ElementAt(0);
									EssentialsPlayer pplayer = new EssentialsPlayer(Int32.Parse(first["last"].ToString()), netuser);
									pplayer.steam = MysqlEscape(netuser.userID.ToString());
									pplayer.firstnick = MysqlEscape(netuser.displayName);
									pplayer.ip = MysqlEscape(netuser.networkPlayer.ipAddress);
									pplayer.nick = MysqlEscape(netuser.displayName);
									ChatApi_Setup(netuser, pplayer.admin);
									if (!PlayerData.ContainsKey(netuser)) {
										StartEPTimer(pplayer);
										PlayerData.Add(netuser, pplayer);
										CheckPlayerGroups(netuser);
									}
									if(anysevice) CheckServices(netuser, pplayer);

									if(broadcast) BroadcastAboutPlayer(netuser, "connected to the server.", true);
								} else DebugPuts("Failed to insert new player");
							});
							
						}
					}
				});

			}
		}
		
		void CheckServices(NetUser netuser, EssentialsPlayer pplayer) {
			sql = _mySql.NewSql();
			string query = string.Format(QLoadServices, DatabasePrefix, MysqlEscape(netuser.userID.ToString()), UnixTime());
			DebugPuts(query);
			sql = Core.Database.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb2 => {
				if(cb2.Any() && netuser != null) {
					DebugMysqlResult(cb2);
					foreach (var dd in cb2)
					{
						string sname = dd["name"].ToString();
						if (sname.Equals("vip")) {
							pplayer.admin = Rangai.Vip;
							ChatApi_Setup(netuser, pplayer.admin);
							DebugPuts(string.Format("{0} is now a vip", netuser.displayName));
							CheckPlayerGroups(netuser);
						}
						Service nservice = new Service(dd);
						pplayer.services.Add(sname, nservice);
					}
				}
			});	
		}

		void RemoveAllGroups(NetUser netuser) {
			string steamid = netuser.userID.ToString();
			if(permission.UserHasGroup(steamid, "vip")) permission.RemoveUserGroup(steamid, "vip");
			if(permission.UserHasGroup(steamid, "chatmod")) permission.RemoveUserGroup(steamid, "chatmod");
			if(permission.UserHasGroup(steamid, "prz")) permission.RemoveUserGroup(steamid, "prz");
			if(permission.UserHasGroup(steamid, "vyrprz")) permission.RemoveUserGroup(steamid, "vyrprz");
			if(permission.UserHasGroup(steamid, "admin")) permission.RemoveUserGroup(steamid, "admin");
		}

		void CheckPlayerGroups(NetUser netuser) {
			if (!PlayerData.ContainsKey(netuser)) return;

			int statusas = (int)PlayerData[netuser].admin;
			string name = netuser.displayName;
			string steamid = netuser.userID.ToString();

			RemoveAllGroups(netuser);

			switch(statusas) {
			case 1:
				permission.AddUserGroup(steamid, "vip");
				break;
			case 2:
				permission.AddUserGroup(steamid, "chatmod");
				break;
			case 3:
				permission.AddUserGroup(steamid, "prz");
				break;
			case 4:
				permission.AddUserGroup(steamid, "vyrprz");
				break;
			case 5:
				permission.AddUserGroup(steamid, "admin");
				break;
			default:
				break;
			}
		}
		
		void OtherNick(NetUser netuser, object nick) {
			List<string> messages = new List<string>();

			if (nick is string) messages.Add(string.Format("Jus naudojate ne savo slapyvardi! Jusu slapyvardis yra {0}", rust.QuoteSafe((string)nick)));
			else				messages.Add(string.Format("Šis slapyvardis jau yra užimtas, pasirinkite kita slapyvardi!"));
			
			KickMessages(netuser, "Naudojate ne savo slapyvardi", messages);	
		}
		void BanCheck(NetUser netuser) {
			sql = _mySql.NewSql();
			string query = string.Format(QLoadBan, DatabasePrefix, MysqlEscape(netuser.displayName), MysqlEscape(netuser.userID.ToString()), netuser.networkPlayer.ipAddress, UnixTime());
			DebugPuts(query);
			sql = Core.Database.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb2 => {
				if(cb2.Any() && netuser != null) {
					foreach (var dd in cb2)
					{
						BanMessage(netuser, Int32.Parse(dd["id"].ToString()), dd["reason"].ToString(), dd["anick"].ToString(), Int32.Parse(dd["banuntil"].ToString()), Int32.Parse(dd["bantime"].ToString()));
						break;
					}
				}
			});	
		}

		void BanMessage(NetUser netuser, int id, string reason, string anick, int banuntil, int bantime) {
			List<string> messages = new List<string>();
			messages.Add(string.Format("{0}, jus esate užblokuotas!", netuser.displayName));
			messages.Add(string.Format("Jusu BAN ID: [color white]{0}", id));
			messages.Add(string.Format("Jusu užblokavimo priežastis: [color white]{0}", reason));
			messages.Add(string.Format("Jus užblokavo: [color white]{0}", anick));

			if (banuntil == 0)	messages.Add(string.Format("Jus esate visam laikui užblokuotas iš serverio!"));
			else				messages.Add(string.Format("Jus busite uzblokuotas iki: [color white]{0}", UnixToDate(banuntil)));

			messages.Add(string.Format("Esate užblokuotas jau nuo: [color white]{0}", UnixToDate(bantime)));
			KickMessages(netuser, "Jus esate uzblokuotas!", messages);
		}
		
		void ChatApi_Setup(NetUser netuser, Rangai rangas) {
			string rn = rangas.ToString();
			ChatAPI?.Call("setPrefix", netuser, PlayerTag[rn]);
			ChatAPI?.Call("setChatColor", netuser, PlayerColor[rn]);
		} 
		
		void KickMessages(NetUser netuser, string topmessage, List<string> messages) {
			if (netuser == null) return;

			foreach(string message in messages) rust.SendChatMessage( netuser, ChatTag , string.Format("[color red]{0}", message));
			rust.Notice(netuser, topmessage, "!", 10);
			LogToConsole(netuser, topmessage);
			netuser.Kick(NetError.NoError, false);
		}
		
		void BroadcastAboutPlayer(NetUser netuser, string text, bool playeronly = false) {
			int statusas = StatusasGet(netuser);
			if (playeronly && statusas > 1) return;
			
			string st = PlayerData[netuser].admin.ToString(); 
			rust.BroadcastChat( ChatTag, string.Format("[color {0}]{1} {2} {3}", PlayerColor[st], PlayerTag[st], netuser.displayName, text) );
			text = Regex.Replace(text, @"\[/?color\b.*?\]", string.Empty);
			LogToConsole(netuser, text);
		}
		
		int StatusasGet(NetUser netuser) {
			return (int)PlayerData[netuser].admin;
		}
		
		[ChatCommand("fps")]
		void Command_FPS(NetUser netuser, string command, string[] args)
		{
			foreach (string value in FPSCommands) CCommand(netuser, value);
			rust.SendChatMessage(netuser, ChatTag, "Enjoy the game with more FPS.");
			rust.SendChatMessage(netuser, ChatTag, "If you have suggestions how we can improve this suggest on forums.");
		}
		
		void CCommand(NetUser netuser, string text) {
			ConsoleNetworker.SendClientCommand(netuser.networkPlayer, text);
		}
		
		[ChatCommand("newbie")]
		void Command_Naujokas(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "You can't use this command. Try to reconnect."); return; }
			if (PlayerData[netuser].newbie == 0) { rust.SendChatMessage(netuser, ChatTag, "Your newbie protection is expired."); return; }
			
			int per = PlayerData[netuser].newbie % 60;
			if(per == 0) {
				int eq = PlayerData[netuser].newbie / 60;
				rust.SendChatMessage(netuser, ChatTag, string.Format("Your newbie protection will still be active for {0} h.", eq));
			} else {
				double lh = Math.Floor((double)PlayerData[netuser].newbie / 60);
				rust.SendChatMessage(netuser, ChatTag, string.Format("Your newbie protection will still be active for {0} h. {1} m.", lh, per));
			}
			
		}

		[ChatCommand("shutdown")]
		void Command_Shutdown(NetUser netuser, string command, string[] args)
		{
			if(!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; }
			if((int)PlayerData[netuser].admin < (int)Rangai.Admin) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			
			if (args.Length == 0) { rust.SendChatMessage(netuser, ChatTag, "Naudojimas: /shutdown priezastis"); return; }

			if (args.Length == 1 && args[0].Equals("stop")) {
				if(shutdowntimer == null) return;

				shutdowntimer.Destroy();
				shutdownleft = 0;
				shutdownreason = "";
			} else {
				if(shutdowntimer != null) { rust.SendChatMessage(netuser, ChatTag, "Jau yra paleistas shutdown laikmatis"); return; }
				shutdownreason = StringArrayToString(args);
				shutdownleft = 300;
				shutdowntimer = timer.Repeat(1f, 0, () =>
				{
					if(shutdownleft == 0) { shutdowntimer.Destroy(); return; }

					switch (shutdownleft)
					{
					case 300: 
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]5 minutes[color red] left until server shutdown."));
						MoreSDInformation();
						break;
					case 240:
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]4 minutes[color red] left until server shutdown."));
						MoreSDInformation();
						break;
					case 180:
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]3 minutes[color red] left until server shutdown."));
						MoreSDInformation();
						break;
					case 120:
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]2 minutes[color red] left until server shutdown."));
						MoreSDInformation();
						break;
					case 60:
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]1 minute[color red] left until server shutdown."));
						MoreSDInformation();
						break;
					case 30:
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]30[color red] seconds until server shutdown."));
						MoreSDInformation();
						break;
					case 20:
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]20[color red] seconds until server shutdown."));
						MoreSDInformation();
						break;
					case 10:
						rust.BroadcastChat(ChatTag, string.Format("[color #1A97A1]10[color red] seconds until server shutdown."));
						MoreSDInformation();
						break;
					case 5:
						rust.BroadcastChat(ChatTag, string.Format("[color red]5..."));
						break;
					case 4:
						rust.BroadcastChat(ChatTag, string.Format("[color red]4..."));
						break;
					case 3:
						rust.BroadcastChat(ChatTag, string.Format("[color red]3..."));
						break;
					case 2:
						rust.BroadcastChat(ChatTag, string.Format("[color red]2..."));
						break;
					case 1:
						rust.BroadcastChat(ChatTag, string.Format("[color red]1..."));
						ConsoleSystem.Run("quit", false);
						break;
					default:
						break;
					}
					shutdownleft--;
				});
			}	
		}
		void MoreSDInformation() {
			rust.BroadcastChat(ChatTag, string.Format("[color red]Restart reason: [color #1A97A1]{0}", shutdownreason));
			rust.BroadcastChat(ChatTag, "[color red]Please [color #1A97A1]stay at your home[color red] and [color #1A97A1]don't loose your stuff.");
			rust.BroadcastChat(ChatTag, "[color red]We don't take responsibility for lost items! We gave [color #1A97A1]5 mins for it.");
		}

		[ChatCommand("god")]
		void Command_God(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; };
			if ((int)PlayerData[netuser].admin < (int)Rangai.Admin) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			
			if(PlayerData[netuser].god == 0) { PlayerData[netuser].god = 1; rust.SendChatMessage(netuser, ChatTag, "GodMode enabled."); }
			else { PlayerData[netuser].god = 0; rust.SendChatMessage(netuser, ChatTag, "GodMode disabled."); }
		}

		[ChatCommand("kick")]
		void Command_Kick(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "You can't use this command. Please reconnect to use it."); return; };
			if ((int)PlayerData[netuser].admin < (int)Rangai.ChatMod) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			if (args.Length < 2) { rust.SendChatMessage(netuser, ChatTag, "Usage: /kick [nick] [reason]"); return; }

			NetUser target = rust.FindPlayer(args[0]);
			if (target == null) { rust.SendChatMessage(netuser, ChatTag, "Player not found!"); return; }
			args[0] = null;
			string reason = StringArrayToString(args);

			BroadcastAboutPlayer(netuser, string.Format("[color white]kicked from the server [color red]{0}[color white] for [color red]{1}[color white]!", target.displayName, reason));
			target.Kick(NetError.NoError, false);
		}
		[ChatCommand("mute")]
		void Command_Mute(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; };
			if ((int)PlayerData[netuser].admin < (int)Rangai.ChatMod) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			if (args.Length < 3) { rust.SendChatMessage(netuser, ChatTag, "Naudojimas: /mute [nick] [laikas minutemis, 0 jeigu visam] [mute priezastis]"); return; }

			NetUser target = rust.FindPlayer(args[0]);
			if (target == null) { rust.SendChatMessage(netuser, ChatTag, "Žaidejas nerastas!"); return; }

			int num = 0;
			bool isNum = Int32.TryParse(args[1], out num);
			if (isNum && num != 0)  { rust.SendChatMessage(netuser, ChatTag, "Blogai nurodytas laikas! Turi buti pvz.: 1w3d15h30m60s! Arba 0 jeigu visam laikui."); return; }
			else if (!stringToSeconds(args[1], ref num)) { rust.SendChatMessage(netuser, ChatTag, "Blogai nurodytas laikas! Turi buti pvz.: 1w3d15h30m60s! Arba 0 jeigu visam laikui."); return; }
			//else { rust.SendChatMessage(netuser, ChatTag, "Kazkas blogai su laiku!"); return; }
			args[0] = null;
			args[1] = null;
			string reason = StringArrayToString(args);

			PlayerData[target].muted = 1;
			PlayerData[target].mute_reason = reason;
			if (num == 0) {
				PlayerData[target].mute_expiration = 0;
				BroadcastAboutPlayer(netuser, string.Format("[color white]užtilde [color red]{0}[color white] už [color red]{1}[color white] visam laikui!", target.displayName, reason));
			}
			else {
				PlayerData[target].mute_expiration = UnixTime()+num;
				BroadcastAboutPlayer(netuser, string.Format("[color white]užtilde [color red]{0}[color white] už [color red]{1}[color white] iki [color red]{2}[color white]!", target.displayName, reason, UnixToDate(PlayerData[target].mute_expiration)));
			}
		}

		[ChatCommand("time")]
		void Command_Time(NetUser netuser, string command, string[] args)
		{
			DateTime date = DateTime.Now;
			rust.SendChatMessage(netuser, ChatTag, date.ToString());
		}
		[ChatCommand("unmute")]
		void Command_Unmute(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; };
			if ((int)PlayerData[netuser].admin < (int)Rangai.ChatMod) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			if (args.Length != 1) { rust.SendChatMessage(netuser, ChatTag, "Naudojimas: /unmute [nick]"); return; }

			NetUser target = rust.FindPlayer(args[0]);
			if (target == null) { rust.SendChatMessage(netuser, ChatTag, "Žaidejas nerastas!"); return; }
			args[0] = null;
			string reason = StringArrayToString(args);

			if (PlayerData[target].muted == 0) { rust.SendChatMessage(netuser, ChatTag, string.Format("Žaidejas {0} nera užtildytas!", target.displayName)); return; }

			if (PlayerData[target].mute_expiration == 0)	BroadcastAboutPlayer(netuser, string.Format("[color white]nueme užtildyma nuo [color red]{0}[color white] kuris buvo už [color red]{1}[color white] visam laikui!", target.displayName, PlayerData[target].mute_reason));
			else 											BroadcastAboutPlayer(netuser, string.Format("[color white]nueme užtildyma nuo [color red]{0}[color white] kuris buvo už [color red]{1}[color white] ir butu galiojes iki [color red]{2}[color white]!", target.displayName, PlayerData[target].mute_reason, UnixToDate(PlayerData[netuser].mute_expiration)));
			
			PlayerData[netuser].muted = 0;
			PlayerData[netuser].mute_reason = "";
			PlayerData[netuser].mute_expiration = 0;
		}

		[ChatCommand("ban")]
		void Command_Ban(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; };
			if ((int)PlayerData[netuser].admin < (int)Rangai.Mod) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			if (args.Length < 3) { rust.SendChatMessage(netuser, ChatTag, "Naudojimas: /ban [nick] [laikas minutemis, 0 jeigu visam] [ban priezastis]"); return; }

			NetUser target = rust.FindPlayer(args[0]);
			if (target == null) { rust.SendChatMessage(netuser, ChatTag, "Žaidejas nerastas!"); return; }

			int num = 0;
			bool isNum = Int32.TryParse(args[1], out num);
			if (isNum && num != 0)  { rust.SendChatMessage(netuser, ChatTag, "Blogai nurodytas laikas! Turi buti pvz.: 1w3d15h30m60s! Arba 0 jeigu visam laikui."); return; }
			else if(!(stringToSeconds(args[1], ref num)) && !(isNum && num == 0)) { rust.SendChatMessage(netuser, ChatTag, "Blogai nurodytas laikas! Turi buti pvz.: 1w3d15h30m60s! Arba 0 jeigu visam laikui."); return; }
			args[0] = null;
			args[1] = null;
			string reason = StringArrayToString(args);

			BanPlayerByAdmin(netuser, target, num, reason);
		}

		bool BanPlayer(NetUser netuser, string reason, int num) {
			if (netuser == null) return false;
			//"INSERT INTO `{0}bans`(`nick`, `steam`, `ip`, `reason`, `bantime`, `banuntil`, `anick`, `asteam`, `aip`)
			// VALUES ('{1}', '{2}', '{3}', '{4}', {5}, {6}, '{7}', '{8}', '{9}');";
			sql = _mySql.NewSql();
			int bantime = UnixTime();
			int banuntil = num;
			if (num > 0) banuntil = bantime+num; 
			string query = string.Format(QInsertBan, DatabasePrefix, MysqlEscape(netuser.displayName), netuser.userID.ToString(), netuser.networkPlayer.ipAddress, reason, bantime , banuntil,  "Serveris", "", "");
			DebugPuts(query);
			sql = Core.Database.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb2 => {
				if(netuser == null) return;
				var first = cb2.ElementAt(0);

				if (num == 0) 	BroadcastAboutPlayer(netuser, string.Format("[color white]buvo užblokuotas už [color red]{0}[color white] visam laikui!", reason));
				else 			BroadcastAboutPlayer(netuser, string.Format("[color white]buvo užblokuotas už [color red]{0}[color white] iki [color red]{1}[color white].", reason, UnixToDate(banuntil)));

				BanMessage(netuser, Int32.Parse(first["last"].ToString()), reason, "Serveris", banuntil, bantime);
			});
			return true;
		}

		void BanPlayerByAdmin(NetUser netuser, NetUser target, int num, string reason) {
			if (netuser == null || target == null) return;
			//"INSERT INTO `{0}bans`(`nick`, `steam`, `ip`, `reason`, `bantime`, `banuntil`, `anick`, `asteam`, `aip`)
			// VALUES ('{1}', '{2}', '{3}', '{4}', {5}, {6}, '{7}', '{8}', '{9}');";
			sql = _mySql.NewSql();
			int bantime = UnixTime();
			int banuntil = num;
			if (num > 0) banuntil = bantime+num; 
			string query = string.Format(QInsertBan, DatabasePrefix, MysqlEscape(target.displayName), target.userID.ToString(), target.networkPlayer.ipAddress, reason, bantime , banuntil,  MysqlEscape(netuser.displayName), netuser.userID.ToString(), netuser.networkPlayer.ipAddress);
			DebugPuts(query);
			sql = Core.Database.Sql.Builder.Append(query);
			_mySql.Query(sql, _mySqlConnection, cb2 => {
				if(target == null || netuser == null) return;
				var first = cb2.ElementAt(0);

				if (num == 0) 	BroadcastAboutPlayer(netuser, string.Format("[color white]užblokavo [color red]{0}[color white] už [color red]{1}[color white] visam laikui!", target.displayName, reason));
				else 			BroadcastAboutPlayer(netuser, string.Format("[color white]užblokavo [color red]{0}[color white] už [color red]{1}[color white] iki [color red]{2}[color white].", target.displayName, reason, UnixToDate(banuntil)));

				BanMessage(target, Int32.Parse(first["last"].ToString()), reason, netuser.displayName, banuntil, bantime);
			});
			
		}
		
		[ChatCommand("tp")]
		void Command_Tp(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; }
			if ((int)PlayerData[netuser].admin < (int)Rangai.Mod) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }

			if ((int)PlayerData[netuser].admin == (int)Rangai.Admin) TeleportByAdmin(netuser, args); 
			else TeleportByMod(netuser, args); 
		}

		[ChatCommand("lastdrop")]
		void Command_LastDrop(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; }
			if ((int)PlayerData[netuser].admin < (int)Rangai.Admin) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			if (lastdrop == zerovector) { rust.SendChatMessage(netuser, ChatTag, "Serveris neužfiksavo paskutinio airdrop."); return; }
		}

		void TeleportByMod(NetUser netuser, string[] args) {
			if (args.Length < 2) { rust.SendChatMessage(netuser, ChatTag, "Naudojimas: /tp [nick] [teleportavimosi priežastis]"); return; }
			NetUser target = rust.FindPlayer(args[0]);
			if (target == null) { rust.SendChatMessage(netuser, ChatTag, "Žaidejas nerastas!"); return; }
			args[0] = null;
			string reason = StringArrayToString(args);
			if(string.IsNullOrEmpty(reason))  { rust.SendChatMessage(netuser, ChatTag, "Netinkama teleportavimosi priežastis!"); return; }
			
			DoTeleportToPlayer(netuser, target);
			BroadcastAboutPlayer(netuser, string.Format("[color white]atsiteleportavo pas [color red]{0}[color white] su priežastimi [color red]{1}[color white]", target.displayName, reason));
		}

		void TeleportByAdmin(NetUser netuser, string[] args) {
			if (args.Length == 0) { rust.SendChatMessage(netuser, ChatTag, "Naudojimas: /tp nick"); return; }
			if (args.Length == 1) {

				NetUser target = rust.FindPlayer(args[0]);
				if (target == null) { rust.SendChatMessage(netuser, ChatTag, "Žaidejas nerastas!"); return; }
				DoTeleportToPlayer(netuser, target);
				BroadcastAboutPlayer(netuser, string.Format("[color white]atsiteleportavo pas [color red]{0}[color white]", target.displayName));
			} else if (args.Length == 2) {

				NetUser target = rust.FindPlayer(args[0]);
				NetUser target2 = rust.FindPlayer(args[1]);

				if (target == null) { rust.SendChatMessage(netuser, ChatTag, string.Format("Žaidejas {0} buvo nerastas", rust.QuoteSafe(args[0]))); return; }
				if (target2 == null) { rust.SendChatMessage(netuser, ChatTag, string.Format("Žaidejas {0} buvo nerastas", rust.QuoteSafe(args[1]))); return; }
				DoTeleportToPlayer(target, target2);
				BroadcastAboutPlayer(netuser, string.Format("[color white]nuteleportavo [color red]{0}[color white] pas žaideja [color red]{1}[color white]", target.displayName, target2.displayName));
			}
		}

		[ChatCommand("tpall")]
		void Command_TpAll(NetUser netuser, string command, string[] args)
		{
			if (!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; }
			if ((int)PlayerData[netuser].admin < (int)Rangai.Admin) { rust.SendChatMessage(netuser, ChatTag, "Jus neturite leidimo naudoti šios komandos."); return; }
			
			if (args.Length == 0) {
				MassTeleport(netuser);
			}
			else if(args.Length == 1) {
				NetUser target = rust.FindPlayer(args[0]);
				if (target == null) { rust.SendChatMessage(netuser, ChatTag, string.Format("Žaidejas {0} buvo nerastas", rust.QuoteSafe(args[0]))); return; }
				MassTeleport(target);
			}
		}

		void MassTeleport(NetUser teleplayer) {
			if(PlayerClient.All.Count > 0) {
				List<NetUser> netusers = PlayerClient.All.Select(pc => pc.netUser).ToList();
				//if(!netusers.Any()) { Puts("Found players, but couldn't make a list.... Odd..."); return; }
				foreach (NetUser netuser in netusers)  {
					if(teleplayer != netuser) DoTeleportToPlayer(netuser, teleplayer);
				}
			}
		}

		void DoTeleportToPlayer(NetUser source, NetUser target)
		{
			if(source == null || target == null) return;
			if(management == null ) management = RustServerManagement.Get();
			if (management != null) management.TeleportPlayerToPlayer(source.playerClient.netPlayer, target.playerClient.netPlayer);
			rust.SendChatMessage(source, "Teleportation failure.");
		}

		[ChatCommand("vip")]
		void Command_Vip(NetUser netuser, string command, string[] args)
		{
			if(!PlayerData.ContainsKey(netuser)) { rust.SendChatMessage(netuser, ChatTag, "Jus negalite naudoti šios komandos. Prisijunkite iš naujo."); return; }
			if((int)PlayerData[netuser].admin != (int)Rangai.Vip) { rust.SendChatMessage(netuser, ChatTag, "Jus nesate VIP žaidejas."); return; }
			
			DateTime daysLeft = UnixToDateTime(PlayerData[netuser].services["vip"].expires);
			DateTime startDate = DateTime.Now;
			TimeSpan t = daysLeft - startDate;
			rust.SendChatMessage(netuser, ChatTag, string.Format("Vip paslauga dar galios {0} d., {1} val ir {2} min.", t.Days, t.Hours, t.Minutes));
		}
		
		bool chatapiworking() {

			if (ChatAPI == null)	return false;
			else 					return true;

			return false;
		}
		
		void DebugPuts(string put) {
			if(DEBUG) Puts(put);
		}
		
		public void DebugMysqlResult(List<Dictionary<string, object>> cb)
		{
			if(!DEBUG) return;

			foreach (var list in cb)
			{
				DebugPuts("--- NEW ROW ---");
				foreach (var dic in list)
				{
					string skey = dic.Key.ToString();
					string svalue = (string)dic.Value.ToString();
					DebugPuts(string.Format("Key: {0} Value: {1}", skey, svalue));
				}
				DebugPuts("----------------");
			}
		}
		
		void LogToConsole(NetUser netuser, string tekstas) {
			
			Puts("[" + netuser.displayName + "][" +  netuser.userID  + "][" + netuser.networkPlayer.ipAddress  + "] > " + tekstas);
			
		}
		
		int UnixTime() {
			Int32 unixTimestamp = (Int32)(DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
			return unixTimestamp;
		}
		
		object getPlayerData(NetUser netuser) {

			if (PlayerData.ContainsKey(netuser)) return PlayerData[netuser]; 
			else MakeUserData(netuser, false);

			return null;
		}

		
		DateTime UnixToDateTime(int Timestamp)
		{
			DateTime dateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
			dateTime = dateTime.AddSeconds(Timestamp);
			return dateTime;
		}
		
		string UnixToDate(int Timestamp)
		{
			System.DateTime dateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
			dateTime = dateTime.AddSeconds(Timestamp);
			string Date = dateTime.ToString("yyyy.MM.dd HH:mm:ss");
			return Date;
		}
		
		string MysqlEscape(string usString)
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
		static string StringArrayToString(string[] array)
		{
			string result = string.Join(" ", array.Where(item => !string.IsNullOrEmpty(item)).ToArray());
			return result;
		}
		private bool stringToSeconds(string timeString, ref int stamp)
		{
			string patern = @"(\d*)[xwhms]";
			Regex regex = new Regex(patern, RegexOptions.IgnoreCase);
			Match match = regex.Match(timeString);
			if (match.Success)
			{
				while (match.Success)
				{
					if (match.ToString().ToLower().Replace(match.Groups[1].Value, string.Empty) == "x")
					{
						stamp += int.Parse(match.Groups[1].Value)*60*60*24*7*30;
					}
					else if (match.ToString().ToLower().Replace(match.Groups[1].Value, string.Empty) == "w")
					{
						stamp += int.Parse(match.Groups[1].Value)*60*60*24*7;
					}
					else if (match.ToString().ToLower().Replace(match.Groups[1].Value, string.Empty) == "d")
					{
						stamp += int.Parse(match.Groups[1].Value)*60*60*24;
					}
					else if (match.ToString().ToLower().Replace(match.Groups[1].Value, string.Empty) == "h")
					{
						stamp += int.Parse(match.Groups[1].Value)*60*60;
					}
					else if (match.ToString().ToLower().Replace(match.Groups[1].Value, string.Empty) == "m")
					{
						stamp += int.Parse(match.Groups[1].Value)*60;
					}
					else if (match.ToString().ToLower().Replace(match.Groups[1].Value, string.Empty) == "s")
					{
						stamp += int.Parse(match.Groups[1].Value);
					}
					match = match.NextMatch();
				}
				return true;
			}
			return false;
		}

		public static string ReadableTimeSpan(TimeSpan span)
		{
			if(span.Seconds == 59) // Fix rounding problem.
			span = span.Add(new TimeSpan(0, 0, 1));

			string formatted = string.Format("{0}{1}{2}{3}{4}",
			(span.Days / 7) > 0 ? string.Format("{0:0} week(s), ", span.Days / 7) : string.Empty,
			span.Days % 7 > 0 ? string.Format("{0:0} day(s), ", span.Days % 7) : string.Empty,
			span.Hours > 0 ? string.Format("{0:0} hour(s), ", span.Hours) : string.Empty,
			span.Minutes > 0 ? string.Format("{0:0} minute(s), ", span.Minutes) : string.Empty,
			span.Seconds > 0 ? string.Format("{0:0} second(s), ", span.Seconds) : string.Empty);

			if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

			return formatted;
		}
	}
}