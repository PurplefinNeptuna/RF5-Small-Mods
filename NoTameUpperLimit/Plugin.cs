using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace NoTameUpperLimit {
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BasePlugin {

		internal static ManualLogSource log;

		public override void Load() {
			// Plugin startup logic

			log = Log;
			log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			Harmony.CreateAndPatchAll(typeof(NoTameUpperLimit));
		}

		[HarmonyPatch]
		class NoTameUpperLimit {
			[HarmonyPatch(typeof(ConstMonster), nameof(ConstMonster.Instance), MethodType.Getter)]
			[HarmonyPostfix]
			public static void GetMaxCount(ref ConstMonster __result) {
				if(__result.Tame_HalfRate_Max_SameMonsterCount != 0) {
					log.LogInfo("Patching tame chance");
					__result.Tame_HalfRate_Max_SameMonsterCount = 0;
				}
			}
		}
	}
}
