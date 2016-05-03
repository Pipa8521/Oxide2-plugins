using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("RealTime", "Prefix", "2.0.0")]
	class RealTime : RustLegacyPlugin
	{
		static float synceveryxsec = 5f;
		static string ChatTag = "RealTime";
		static int minplayer = 0;
		Dictionary<string, Dictionary<string, int>> TimeTable = new Dictionary<string, Dictionary<string, int>>
		{
			{ "0:0", new Dictionary<string, int>
				{
					{ "RealHour", 0 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 0 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "0:30", new Dictionary<string, int>
				{
					{ "RealHour", 0 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 0 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "01:0", new Dictionary<string, int>
				{
					{ "RealHour", 1 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 1 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "01:30", new Dictionary<string, int>
				{
					{ "RealHour", 1 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 1 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "02:0", new Dictionary<string, int>
				{
					{ "RealHour", 2 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 2 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "02:30", new Dictionary<string, int>
				{
					{ "RealHour", 2 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 2 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "03:0", new Dictionary<string, int>
				{
					{ "RealHour", 3 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 3 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "03:30", new Dictionary<string, int>
				{
					{ "RealHour", 3 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 3 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "04:0", new Dictionary<string, int>
				{
					{ "RealHour", 4 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 4 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "04:30", new Dictionary<string, int>
				{
					{ "RealHour", 4 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 4 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "05:0", new Dictionary<string, int>
				{
					{ "RealHour", 5 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 5 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "05:30", new Dictionary<string, int>
				{
					{ "RealHour", 5 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 5 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "06:0", new Dictionary<string, int>
				{
					{ "RealHour", 6 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 6 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "06:30", new Dictionary<string, int>
				{
					{ "RealHour", 6 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 6 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "07:0", new Dictionary<string, int>
				{
					{ "RealHour", 7 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 7 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "07:30", new Dictionary<string, int>
				{
					{ "RealHour", 7 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 7 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "08:0", new Dictionary<string, int>
				{
					{ "RealHour", 8 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 8 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "08:30", new Dictionary<string, int>
				{
					{ "RealHour", 8 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 8 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "09:0", new Dictionary<string, int>
				{
					{ "RealHour", 9 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 9 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "09:30", new Dictionary<string, int>
				{
					{ "RealHour", 9 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 9 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "10:0", new Dictionary<string, int>
				{
					{ "RealHour", 10 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 10 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "10:30", new Dictionary<string, int>
				{
					{ "RealHour", 10 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 10 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "11:0", new Dictionary<string, int>
				{
					{ "RealHour", 11 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 11 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "11:30", new Dictionary<string, int>
				{
					{ "RealHour", 11 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 11 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "12:0", new Dictionary<string, int>
				{
					{ "RealHour", 12 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 12 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "12:30", new Dictionary<string, int>
				{
					{ "RealHour", 12 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 12 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "13:0", new Dictionary <string, int>
				{
					{ "RealHour", 13 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 13 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "13:30", new Dictionary <string, int>
				{
					{ "RealHour", 13 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 13 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "14:0", new Dictionary <string, int>
				{
					{ "RealHour", 14 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 14 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "14:30", new Dictionary <string, int>
				{
					{ "RealHour", 14 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 14 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "15:0", new Dictionary <string, int>
				{
					{ "RealHour", 15 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 15 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "15:30", new Dictionary <string, int>
				{
					{ "RealHour", 15 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 15 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "16:0", new Dictionary <string, int>
				{
					{ "RealHour", 16 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 16 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "16:30", new Dictionary <string, int>
				{
					{ "RealHour", 16 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 16 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "17:0", new Dictionary <string, int>
				{
					{ "RealHour", 17 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 17 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "17:30", new Dictionary <string, int>
				{
					{ "RealHour", 17 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 17 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "18:0", new Dictionary <string, int>
				{
					{ "RealHour", 18 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 17 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "18:30", new Dictionary <string, int>
				{
					{ "RealHour", 18 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 17 },
					{ "VirtualMinute", 45 }
				}
			},
			{ "19:0", new Dictionary <string, int>
				{
					{ "RealHour", 19 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 18 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "19:30", new Dictionary <string, int>
				{
					{ "RealHour", 19 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 18 },
					{ "VirtualMinute", 10 }
				}
			},
			{ "20:0", new Dictionary <string, int>
				{
					{ "RealHour", 20 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 18 },
					{ "VirtualMinute", 20 }
				}
			},
			{ "20:30", new Dictionary <string, int>
				{
					{ "RealHour", 20 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 18 },
					{ "VirtualMinute", 30 }
				}
			},
			{ "21:0", new Dictionary <string, int>
				{
					{ "RealHour", 21 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 18 },
					{ "VirtualMinute", 40 }
				}
			},
			{ "21:30", new Dictionary <string, int>
				{
					{ "RealHour", 21 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 18 },
					{ "VirtualMinute", 50 }
				}
			},
			{ "22:0", new Dictionary <string, int>
				{
					{ "RealHour", 22 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 19 },
					{ "VirtualMinute", 0 }
				}
			},
			{ "22:30", new Dictionary <string, int>
				{
					{ "RealHour", 22 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 19 },
					{ "VirtualMinute", 10 }
				}
			},
			{ "23:0", new Dictionary <string, int>
				{
					{ "RealHour", 23 },
					{ "RealMinute", 0 },
					{ "VirtualHour", 19 },
					{ "VirtualMinute", 20 }
				}
			},
			{ "23:30", new Dictionary <string, int>
				{
					{ "RealHour", 23 },
					{ "RealMinute", 30 },
					{ "VirtualHour", 19 },
					{ "VirtualMinute", 30 }
				}
			}
		};
		bool areplayersonline = false;
		Timer synctimer;

		void LoadDefaultConfig() { }
		
		class RealDateTimes : RealTime {
			int realhour = 0;
			int realminutes = 0;
			public int virtualhour = 0;
			public int virtualminutes = 0;
			public DateTime realdtime;

			public RealDateTimes(int rhour, int rminutes, int vhour, int vminutes) {

				realhour = rhour;
				realminutes = rminutes;
				virtualhour = vhour;
				virtualminutes = vminutes;

				realdtime = new DateTime(2016, 1, 1, rhour, rminutes, 0);
			}
		}

		void setThisTime(RealDateTimes rdt) {
			int needc = ((10000 / 60) * rdt.virtualminutes);
			float stime = float.Parse(string.Format("{0}.{1}", rdt.virtualhour, needc));
			EnvironmentControlCenter.Singleton.SetTime(stime);
		}

		Dictionary<string, RealDateTimes> RealDateTimesDic = new Dictionary<string, RealDateTimes>();

		private void CheckCfg<T>(string Key, ref T var)
		{
			if (Config[Key] is T)
			var = (T)Config[Key];
			else
			Config[Key] = var;
		} 
		
		void Init()
		{
			// realtime.json time 
			CheckCfg<Dictionary<string, Dictionary<string, int>>>("RealTime: Time table", ref TimeTable);
			CheckCfg<float>("RealTime: Sync time in seconds", ref synceveryxsec);
			CheckCfg<string>("RealTime: ChatTag", ref ChatTag);
			SaveConfig();
			// let's add everything to dictionary
			if (TimeTable.Any()) {
				foreach (var WhatTime in TimeTable) {
					if(RealDateTimesDic.ContainsKey(WhatTime.Key)) {
						Puts(string.Format("{0} is repeatable, skipping, make sure to fix your config"));
						continue;
					} else {
						// It's better in this way
						string name = WhatTime.Key.ToString();
						Dictionary<string, int> info = WhatTime.Value;

						// Let's do some checks, because some people are ...
						if (info["RealHour"] > 23 || info["RealHour"] < 0) {
							Puts(string.Format("{0} - RealHour is out of bounds ({1}) it must be between 0-23", name, info["RealHour"]));
							continue; 
						}
						else if (info["RealMinute"] > 59 || info["RealMinute"] < 0) {
							Puts(string.Format("{0} - RealMinute is out of bounds ({1}) it must be between 0-59", name, info["RealMinute"])); 
							continue; 
						}
						else if (info["VirtualHour"] > 23 || info["VirtualHour"] < 0) {
							Puts(string.Format("{0} - VirtualHour is out of bounds ({1}) it must be between 0-23", name, info["VirtualHour"])); 
							continue; 
						}
						else if (info["VirtualMinute"] > 59 || info["VirtualMinute"] < 0) {
							Puts(string.Format("{0} - VirtualHour is out of bounds ({1}) it must be between 0-59", name, info["VirtualMinute"])); 
							continue; 
						}

						// Finally let's make some progress
						RealDateTimes new_rdt = new RealDateTimes(info["RealHour"], info["RealMinute"], info["VirtualHour"], info["VirtualMinute"]);
						RealDateTimesDic.Add(name, new_rdt);
					}
				}

				synctimer = timer.Repeat(synceveryxsec, 0, () =>
				{
					RealDateTimes rdt = GetClosestDate();
					//Puts(string.Format("{0}:{1}", rdt.virtualhour, rdt.virtualminutes));
					if (rdt != null) setThisTime(rdt);
					else Puts("Failure, date could not be set for some weird reason?>");
				});

			} else {
				Puts("Fix your config, it doesn't contain a TimeTable :)");
			}

			
		}

		RealDateTimes GetClosestDate() {
			DateTime currDate;
			RealDateTimes closestDate = null;
			DateTime todayis = DateTime.Now;
			currDate = new DateTime(2016, 1, 1, todayis.Hour, todayis.Minute, 0);
			//Puts(string.Format("Current time: {0}:{1}", todayis.Hour, todayis.Minute));
				
			var first = RealDateTimesDic.ElementAt(0);

			long min = Math.Abs((long)(currDate - first.Value.realdtime).TotalSeconds);
			long diff = 0;
			closestDate = first.Value;

			foreach (var data in RealDateTimesDic)
			{
				DateTime date = data.Value.realdtime;
				diff = Math.Abs((long)(currDate - date).TotalSeconds);
				//Puts(string.Format("{0}:{1} - diff {2}", date.Hour, date.Minute, diff));
				if (diff < min)
				{
					min = diff;
					closestDate = data.Value;
				}
			}
			//Puts("Chosen: {0}:{1} with diff {2}", closestDate.realdtime.Hour, closestDate.realdtime.Minute, diff);
			return closestDate;
		}
	}
}