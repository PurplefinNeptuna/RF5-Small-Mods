using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;

namespace NGWordAllow {
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BasePlugin {
		public override void Load() {
			// Plugin startup logic
			Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			Harmony.CreateAndPatchAll(typeof(NGAllow));
		}

		[HarmonyPatch]
		public class NGAllow {
			[HarmonyPatch(typeof(NGWord), nameof(NGWord.NgcCheck))]
			[HarmonyPostfix]
			public static void NGPatch(ref bool __result) {
				__result = false;
			}
		}
	}
}
