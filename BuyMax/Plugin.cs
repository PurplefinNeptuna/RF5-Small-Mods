using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace BuyMax {
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BasePlugin {

		internal static ManualLogSource log;

		public override void Load() {
			// Plugin startup logic

			log = Log;
			log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			Harmony.CreateAndPatchAll(typeof(BuyMax));
		}

		[HarmonyPatch]
		class BuyMax {
			[HarmonyPatch(typeof(UIShopController), nameof(UIShopController.Update))]
			[HarmonyPrefix]
			public static void ToMaxCount(UIShopController __instance) {
				if (!__instance.EndShop && RF5Input.Pad.Edge(RF5Input.Key.PS)) {
					var focusedItem = __instance.focusingBlock;
					if (focusedItem is null) return;
					var shopItem = focusedItem.shopItem;
					if (shopItem is null) return;
					var itemID = shopItem.ItemId;
					var itemStorage = __instance.GetItemStorage(itemID);
					if (itemStorage is null) return;
					var howMuch = itemStorage.GetSpaceToInput(itemID);
					for (var i = 0; i < howMuch; i++) {
						__instance.AddItemToBasket();
					}
				}
			}
		}
	}
}
