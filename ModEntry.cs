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

		public static IMonitor Logger;
		public List<string> excludedMethods = new() { "set_StepsTaken" };

		/// <summary>The mod entry point.</summary>
		/// <param name="helper" />
		public override void Entry(IModHelper helper)
		{
			Logger = Monitor;
			PerformHarmonyPatches();
		}

		/// <summary>Add methods to event hooks.</summary>
		private void PerformHarmonyPatches()
		{
			Harmony harmony = new(ModManifest.UniqueID);

			HarmonyMethod Stats_set_Prefix = new(typeof(ModEntry).GetMethod("Stats_set_Prefix"));
			HarmonyMethod Stats_set_Postfix = new(typeof(ModEntry).GetMethod("Stats_set_Postfix"));

			List<MethodInfo> methods = typeof(Stats).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();

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
					catch (System.Exception e)
					{
						LogTrace($"Encountered exception while attempting to patch {method.Name}: {e}");
					}
				}
			}

			//FieldInfo statDictField = typeof(Stats).GetField("stat_dictionary");

			//methods = statDictField.FieldType.GetMethods().ToList();

			//foreach (MethodInfo method in methods)
			//{
			//	LogTrace($"Detected method {method.Name} in {statDictField.Name}.");
			//	LogTrace($"\tParameters: {string.Join(", ", method.GetParameters().Select(x => $"{x.ParameterType} {x.Name}"))}");
			//	LogTrace($"\tReturn type: {method.ReturnType}");
			//}

			//List<PropertyInfo> properties = statDictField.FieldType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();

			//foreach (PropertyInfo property in properties)
			//{
			//	LogTrace($"Detected property {property.PropertyType} {property.Name} in {statDictField.Name}");

			//	if (property.Name.Equals("Item"))
			//	{
			//		LogTrace($"\tProperty Get Method: {property.GetGetMethod()}");
			//		LogTrace($"\tProperty Set Method: {property.GetSetMethod()}");
			//		LogTrace($"\tProperty Index Parameters: {property.GetIndexParameters()}");
			//		LogTrace($"\tAccessors: {string.Join(", ", property.GetAccessors(true).Select(x => $"{x.GetType()} {x.Name}"))}");
			//	}
			//}

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
	}
}
