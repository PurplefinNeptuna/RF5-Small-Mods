using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Fishing;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace TrashRadar {
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BasePlugin {

		internal static ManualLogSource log;

		public override void Load() {
			// Plugin startup logic
			log = Log;
			log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			Harmony.CreateAndPatchAll(typeof(RareCanFish));
		}

		[HarmonyPatch]
		public class RareCanFish {

			internal static SortedDictionary<int, (FishSwim, bool)> treasureFishList = new();
			internal static GameObject sparkPref = null;

			[HarmonyPatch(typeof(FocusObjectName), nameof(FocusObjectName.UpdateFocus))]
			[HarmonyPostfix]
			public static void UpdateFocusPatch(FocusObjectName __instance, FocusInterface focusObect) {
				if (focusObect is not null) return;
				var nearestFish = GetNearestTreasureFish();
				if (nearestFish.Item1 == float.MaxValue) return;
				__instance.FocusOn();
				__instance.SetText($"Trash nearby ({(int)nearestFish.Item1})", nearestFish.Item2 ? Color.cyan : Color.yellow);
			}

			[HarmonyPatch(typeof(FishSwim), nameof(FishSwim.FadeIn))]
			[HarmonyPostfix]
			public static void FadeInPatch(FishSwim __instance) {
				log.LogInfo($"Fade in {__instance.UniqueId}, dist {Mathf.Round(GetFishDist(__instance))}, position {__instance.transform.position}");
				var fishMan = FishingManager.Instance;
				bool treasureFishOrig = fishMan.CheckGomi(__instance.FishId);

				if (!treasureFishOrig && __instance.Size < 500) {
					var reRoll = FishingGomiDataTable.ReLottery(fishMan.EquipRod);
					bool reRoolSucc = reRoll is ItemID.Item_Akikan or ItemID.Item_Nagagutsu or ItemID.Item_Reanaakikan;
					log.LogInfo($"Re-roll fish {__instance.UniqueId} (from {(int)__instance.FishId}): {(reRoolSucc?"success":"failed")}");
					if (reRoolSucc) {
						ChangeFish(__instance, reRoll);
					}
				}

				bool treasureFish = FishingManager.Instance.CheckGomi(__instance.FishId);
				bool onList = treasureFishList.ContainsKey(__instance.UniqueId);
				if (treasureFish) {
					if (onList) return;
					treasureFishList.Add(__instance.UniqueId, (__instance, treasureFishOrig));
					log.LogInfo($"Found trash {__instance.UniqueId}");
					AddFishSparks(__instance);
				}
				else if (onList) {
					var removed = RemoveFishSparks(__instance);
					if (removed) log.LogInfo($"Removing trash {__instance.UniqueId} from trash list (changed)");
				}
			}

			[HarmonyPatch(typeof(FishSwim), nameof(FishSwim.FadeOut))]
			[HarmonyPrefix]
			public static void FadeOutPatch(FishSwim __instance) {
				log.LogInfo($"Fade out {__instance.UniqueId}, dist {Mathf.Round(GetFishDist(__instance))}, position {__instance.transform.position}");
				var removed = RemoveFishSparks(__instance);
				if (removed) log.LogInfo($"Removing trash {__instance.UniqueId} from trash list (fade out)");
			}

			[HarmonyPatch(typeof(FishingManager), nameof(FishingManager.Remove))]
			[HarmonyPrefix]
			public static void RemovePatch(FishingManager __instance, int unique_id) {
				var exist = __instance.AimingFish.ContainsKey(unique_id);
				if (exist) {
					var fish = __instance.AimingFish[unique_id];
					log.LogInfo($"Remove {fish.UniqueId}, dist {Mathf.Round(GetFishDist(fish))}, position {__instance.transform.position}");
					var removed = RemoveFishSparks(fish);
					if (removed) log.LogInfo($"Removing trash {fish.UniqueId} from trash list (catched)");
				}
			}

			[HarmonyPatch(typeof(FishingManager), nameof(FishingManager.ReLottery))]
			[HarmonyPrefix]
			public static bool ReLotteryPatch(ItemID item_id, ref ItemID __result) {
				__result = item_id;
				return false;
			}

			[HarmonyPatch(typeof(FishingManager), nameof(FishingManager.Create))]
			[HarmonyPostfix]
			public static void CreatePatch(FishingPoint point, int max) {
				HUDController.Instance.SetAreaChangeText($"Entering {point.name} ({max})");
			}

			[HarmonyPatch(typeof(FishingPoint), nameof(FishingPoint.DeleteFish))]
			[HarmonyPostfix]
			public static void DeleteFishPatch(FishingPoint __instance) {
				HUDController.Instance.SetAreaChangeText($"Leaving {__instance.name}");
			}

			internal static void ChangeFish(FishSwim fish, ItemID item) {
				if (fish is null) return;
				var fishData = FishData.GetFishData(item);
				if (fishData is null) return;
				fish.FishId = fishData.ItemId;
				fish.Size = Random.RandomRangeInt(10 * fishData.Min, 10 * fishData.Max);
			}

			internal static float GetFishDist(FishSwim fish) {
				if (PlayerManager.Character is null || fish is null || fish.gameObject is null || !fish.gameObject.active) return float.MaxValue;
				float dist = Vector3.Distance(fish.transform.position, PlayerManager.Character.transform.position);
				return dist;
			}

			internal static (float, bool) GetNearestTreasureFish() {
				var shortest = (501f, false);
				foreach (var (_, (fish, roll)) in treasureFishList) {
					float dist = GetFishDist(fish);
					if (dist < shortest.Item1) {
						shortest = (dist, roll);
					}
				}
				shortest.Item1 = shortest.Item1 >= 500f ? float.MaxValue : Mathf.Round(shortest.Item1);
				return (shortest.Item1, shortest.Item2);
			}

			internal static bool RemoveFishSparks(FishSwim fish) {
				var result = treasureFishList.Remove(fish.UniqueId);
				var name = fish.gameObject.name + "_Sparks";
				var sparkle = fish.gameObject.FindGameObject(name);
				if (sparkle is not null) {
					GameObject.Destroy(sparkle);
				}
				return result;
			}

			internal static void LoadSparkPref() {
				var defOGItem = OnGroundItem.BaseAssetOnGroundItem;
				if (defOGItem is null) return;
				var sparkles = defOGItem.GetChildren(true);
				if (sparkles is null || sparkles.Count < 1) return;
				var particleOmitters = sparkles[0].GetChildren(true);
				if (particleOmitters is null || particleOmitters.Count < 1) return;
				sparkPref = particleOmitters[0];
				log.LogInfo($"Set default spark prefab");
			}

			internal static void AddFishSparks(FishSwim fish) {
				if (sparkPref is null) LoadSparkPref();
				if (sparkPref is null) return;
				var fishTf = fish.transform;
				if (fishTf is null) return;
				var fishGO = fish.gameObject;

				var name = fishGO.name + "_Sparks";
				if (fishGO.FindGameObject(name) != null) return;

				var sparkGO = GameObject.Instantiate(sparkPref, fishTf.position, fishTf.rotation, fishTf);
				sparkGO.name = name;
				sparkGO.SetActive(true);
				sparkGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
				sparkGO.transform.localScale = Vector3.one;
				log.LogInfo($"Add sparkle to trash {fish.UniqueId}");
				var particle = sparkGO.GetComponent<ParticleSystem>();
				if (particle is null) return;
				particle.startSize = 1.5f;
				particle.startLifetime = 5f;
			}
		}
	}
}
