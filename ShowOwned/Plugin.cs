using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShowOwned {
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BasePlugin {
		internal static ManualLogSource log;

		public override void Load() {
			// Plugin startup logic
			Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
			log = Log;

			Harmony.CreateAndPatchAll(typeof(ShowOwned));
		}

		[HarmonyPatch]
		public class ShowOwned {

			internal static Dictionary<int, (Text, Text)> ownedTextList = new();

			[HarmonyPatch(typeof(ItemTextDiscription), nameof(ItemTextDiscription.Start))]
			[HarmonyPostfix]
			public static void StartPatch(ItemTextDiscription __instance) {
				var instanceID = __instance.GetInstanceID();
				var hasOwned = ownedTextList.ContainsKey(instanceID);
				if (hasOwned) return;
				log.LogInfo($"Create owned text for {instanceID}");
				CreateOwned(__instance);
			}

			internal static void CreateOwned(ItemTextDiscription itemText) {
				var buyLabel = itemText.FindGameObject("ruck_buy");
				var sellLabel = itemText.FindGameObject("ruck_sell");
				var buyPrice = itemText.ItemBuyPriceText.gameObject;
				var sellPrice = itemText.ItemSellPriceText.gameObject;

				if (buyLabel is null || sellLabel is null || buyPrice is null || sellPrice is null) {
					return;
				}

				var ownedLabelRot = sellLabel.transform.rotation;
				var ownedLabelPos = sellLabel.transform.position;
				ownedLabelPos.x += sellLabel.transform.position.x - buyLabel.transform.position.x;
				ownedLabelPos.y += sellLabel.transform.position.y - buyLabel.transform.position.y;
				var ownedLabel = Object.Instantiate(sellLabel, ownedLabelPos, ownedLabelRot, sellLabel.transform.parent);
				ownedLabel.name = "ruck_owned";
				Text ownedLabelText = ownedLabel.GetComponent<SText>();
				if (ownedLabelText is null) ownedLabelText = ownedLabel.GetComponent<Text>();
				if (ownedLabelText is null) return;
				ownedLabelText.text = "In Storage";

				var ownedAmountRot = sellPrice.transform.rotation;
				var ownedAmountPos = sellPrice.transform.position;
				ownedAmountPos.x += sellPrice.transform.position.x - buyPrice.transform.position.x;
				ownedAmountPos.y += sellPrice.transform.position.y - buyPrice.transform.position.y;
				var ownedAmount = Object.Instantiate(sellPrice, ownedAmountPos, ownedAmountRot, sellPrice.transform.parent);
				ownedAmount.name = "Owned__ruck_item_amount";
				var ownedAmountText = ownedAmount.GetComponent<Text>();
				if (ownedAmountText is null) return;
				ownedAmountText.text = "0";
				ownedTextList.TryAdd(itemText.GetInstanceID(), (ownedLabelText, ownedAmountText));
			}

			[HarmonyPatch(typeof(ItemTextDiscription), nameof(ItemTextDiscription.SetItem), new Type[] { typeof(ItemData) })]
			[HarmonyPostfix]
			public static void SetItemPatch(ItemTextDiscription __instance, ItemData ItemData) {
				var instanceID = __instance.GetInstanceID();
				var hasOwned = ownedTextList.TryGetValue(instanceID, out var ownedText);
				if (!hasOwned) CreateOwned(__instance);
				hasOwned = ownedTextList.TryGetValue(instanceID, out ownedText);
				if (!hasOwned || ownedText.Item1 is null || ownedText.Item2 is null) {
					log.LogError($"No owned text exist for {instanceID}");
					return;
				}

				if (ItemData == null || ItemData.ItemID == ItemID.ITEM_EMPTY) {
					ownedText.Item1.text = "";
					ownedText.Item2.text = "";
					return;
				}

				var amountOwned = ItemStorageManager.GetAmountInAllStorage(ItemData.ItemID) - ItemStorageManager.GetStorage(Define.StorageType.Rucksack).GetItemAmoutId(ItemData.ItemID);
				ownedText.Item1.text = "In Storage";
				ownedText.Item2.text = amountOwned.ToString();
			}

			[HarmonyPatch(typeof(ItemTextDiscription), nameof(ItemTextDiscription.ClearItemDisp))]
			[HarmonyPostfix]
			public static void ClearItemDispPatch(ItemTextDiscription __instance) {
				var instanceID = __instance.GetInstanceID();
				var hasOwned = ownedTextList.TryGetValue(instanceID, out var ownedText);
				if (!hasOwned) CreateOwned(__instance);
				hasOwned = ownedTextList.TryGetValue(instanceID, out ownedText);
				if (!hasOwned || ownedText.Item1 is null || ownedText.Item2 is null) {
					log.LogError($"No owned text exist for {instanceID}");
					return;
				}
				ownedText.Item1.text = "";
				ownedText.Item2.text = "";
			}
		}
	}
}
