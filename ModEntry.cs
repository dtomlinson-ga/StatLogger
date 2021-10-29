// Copyright (C) 2021 Vertigon
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see https://www.gnu.org/licenses/.

using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StatLogger
{
	/// <summary>The mod entry point.</summary>
	public class ModEntry : Mod
	{
		public Harmony harmony;
		public static IMonitor Logger;
		public List<string> excludedMethods = new() { "set_StepsTaken" };

		/// <summary>The mod entry point.</summary>
		/// <param name="helper" />
		public override void Entry(IModHelper helper)
		{
			harmony = new(ModManifest.UniqueID);
			Logger = Monitor;

			PerformHarmonyPatches();
			
			helper.ConsoleCommands.Add("dump_patches_other", "Dumps list of all Harmony patches, other than those added by StatLogger and SMAPI, to the console", (_,_) => CheckForOtherPatches());
			helper.ConsoleCommands.Add("dump_patches_all", "Dumps list of all Harmony patches, including those added by StatLogger and SMAPI, to the console", (_, _) => CheckForOtherPatches(includeOwn: true));
		}

		private void CheckForOtherPatches(bool includeOwn = false)
		{
			var originalMethods = Harmony.GetAllPatchedMethods();

			LogTrace($"Total patches found: {originalMethods.Count()}\n");

			foreach (MethodBase method in originalMethods)
			{
				var patches = Harmony.GetPatchInfo(method);

				//if patches is null or patches only contains patches by StatLogger and SMAPI
				if (patches is null || (!patches.Owners.ToList().Any(x => !x.Equals("Vertigon.StatLogger") && !x.Contains("SMAPI")) && !includeOwn))
					continue;

				foreach (var patch in patches.Prefixes)
				{
					LogTrace($"Prefix found for {method.Name}");
					LogTrace("\tindex: " + patch.index);
					LogTrace("\towner: " + patch.owner);
					LogTrace("\ttarget method: " + method.FullDescription().ToString());
					LogTrace("\tpatch method: " + patch.PatchMethod);
					LogTrace("\tpriority: " + patch.priority);
					LogTrace("\tbefore: " + string.Join(", ", patch.before));
					LogTrace("\tafter: " + string.Join(", ", patch.after));
					LogTrace("");
				}

				foreach (var patch in patches.Transpilers)
				{
					LogTrace($"Transpiler found for {method.Name}");
					LogTrace("\tindex: " + patch.index);
					LogTrace("\towner: " + patch.owner);
					LogTrace("\ttarget method: " + method.FullDescription().ToString());
					LogTrace("\tpatch method: " + patch.PatchMethod);
					LogTrace("\tpriority: " + patch.priority);
					LogTrace("\tbefore: " + string.Join(", ", patch.before));
					LogTrace("\tafter: " + string.Join(", ", patch.after));
					LogTrace("");
				}

				foreach (var patch in patches.Postfixes)
				{
					LogTrace($"Postfix found for {method.Name}");
					LogTrace("\tindex: " + patch.index);
					LogTrace("\towner: " + patch.owner);
					LogTrace("\ttarget method: " + method.FullDescription().ToString());
					LogTrace("\tpatch method: " + patch.PatchMethod);
					LogTrace("\tpriority: " + patch.priority);
					LogTrace("\tbefore: " + string.Join(", ", patch.before));
					LogTrace("\tafter: " + string.Join(", ", patch.after));
					LogTrace("");
				}
			}
		}

		/// <summary>Dynamically patch setter methods of Stats object.</summary>
		private void PerformHarmonyPatches()
		{
			List<MethodInfo> methods = typeof(Stats).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name.Contains("set")).ToList();

			HarmonyMethod Stats_set_Prefix = new(typeof(ModEntry).GetMethod("Stats_set_Prefix"));
			HarmonyMethod Stats_set_Postfix = new(typeof(ModEntry).GetMethod("Stats_set_Postfix"));

			foreach (MethodInfo method in methods)
			{
				if (method.Name.Contains("set"))
				{
					LogTrace($"Detected setter method {method.Name}.");

					try
					{
						if (!excludedMethods.Contains(method.Name))
						{
							harmony.Patch(
								method,
								prefix: Stats_set_Prefix,
								postfix: Stats_set_Postfix
							);

							LogTrace($"\tSuccessfully patched {method.Name}");
						}
						else
						{
							LogTrace($"\tSkipped patching {method.Name}");
						}
					}
					catch (Exception e)
					{
						LogTrace($"Encountered exception while attempting to patch {method.Name}: {e}");
					}
				}
			}

			Type dictType = typeof(Dictionary<string, uint>);
			MethodInfo setItemMethod = dictType.GetMethod("set_Item");

			HarmonyMethod Stats_dict_set_Prefix = new(typeof(ModEntry).GetMethod("Stats_dict_set_Prefix"));
			HarmonyMethod Stats_dict_set_Postfix = new(typeof(ModEntry).GetMethod("Stats_dict_set_Postfix"));

			try
			{
				harmony.Patch(
					setItemMethod,
					prefix: Stats_dict_set_Prefix,
					postfix: Stats_dict_set_Postfix
				);

				LogTrace($"Successfully patched {dictType.Name}<{string.Join(", ", dictType.GenericTypeArguments.Select(x => $"{x.Name}"))}>::{setItemMethod.Name}");
			}
			catch (Exception e)
			{
				LogTrace($"Encountered exception while attempting to patch {dictType.Name}<{string.Join(", ", dictType.GenericTypeArguments.Select(x => $"{x.Name}"))}>::{setItemMethod.Name}: {e}");
			}

		}

		public static void Stats_set_Prefix(MethodBase __originalMethod, out PropertyInfo __state)
		{
			__state = typeof(Stats).GetProperty(__originalMethod.Name.Replace("set_", ""));
			LogTrace($"Altering stat {__state.Name}.");
			LogTrace($"Previous stat value: {__state.GetValue(Game1.stats)}");
		}

		public static void Stats_set_Postfix(PropertyInfo __state)
		{
			LogTrace($"New stat value: {__state.GetValue(Game1.stats)}");
		}

		public static void Stats_dict_set_Prefix(string key)
		{
			LogTrace($"Altering stat {key}");
			LogTrace($"Previous stat value: {(Game1.stats.stat_dictionary.ContainsKey(key) ? Game1.stats.stat_dictionary[key] : "")}");
		}

		public static void Stats_dict_set_Postfix(string key)
		{
			LogTrace($"New stat value: {Game1.stats.stat_dictionary[key]}");
		}

		public static void LogTrace(string message)
		{
			Logger.Log(message, LogLevel.Trace);
		}

		public static void LogError(string message)
		{
			Logger.Log(message, LogLevel.Error);
		}
	}
}
