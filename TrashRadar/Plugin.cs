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

			internal static SortedDictionary<int, FishSwim> treasureFishList = new();
			internal static GameObject sparkPref = null;

			[HarmonyPatch(typeof(FishSwim), nameof(FishSwim.Update))]
			[HarmonyPostfix]
			public static void FishSwimUpdatePatch(FishSwim __instance) {
				bool treasureFish = FishingManager.Instance.CheckGomi(__instance.FishId);
				bool onList = treasureFishList.ContainsKey(__instance.UniqueId);
				if (treasureFish && GetFishDist(__instance) < 500f) {
					if (onList) return;
					treasureFishList.Add(__instance.UniqueId, __instance);
					log.LogInfo($"Found trash {__instance.UniqueId}");
					AddFishSparks(__instance);
				}
				else if (onList) {
					var removed = RemoveFishSparks(__instance);
					if (removed) log.LogInfo($"Removing trash {__instance.UniqueId} from trash list (too far)");
				}
			}

			[HarmonyPatch(typeof(FishSwim), nameof(FishSwim.OnDestroy))]
			[HarmonyPrefix]
			public static void FishSwimOnDestroyPatch(FishSwim __instance) {
				var removed = RemoveFishSparks(__instance);
				if (removed) log.LogInfo($"Removing trash {__instance.UniqueId} from trash list (destroyed)");
			}

			[HarmonyPatch(typeof(FishingManager), nameof(FishingManager.FishHit))]
			[HarmonyPostfix]
			public static void FishHitPatch(FishingManager __instance) {
				var fish = __instance.targetFish;
				if (fish == null) return;
				var removed = RemoveFishSparks(fish);
				if (removed) log.LogInfo($"Removing trash {fish.UniqueId} from trash list (catched)");
			}

			[HarmonyPatch(typeof(FocusObjectName), nameof(FocusObjectName.UpdateFocus))]
			[HarmonyPostfix]
			public static void UpdateFocusPatch(FocusObjectName __instance, FocusInterface focusObect) {
				if (focusObect != null) return;
				var nearestDist = GetNearestTreasureFishDist();
				if (nearestDist == null) return;
				__instance.FocusOn();
				__instance.SetText($"Trash nearby ({nearestDist})", Color.yellow);
			}

			[HarmonyPatch(typeof(FishSwim), nameof(FishSwim.FadeIn))]
			[HarmonyPostfix]
			public static void FadeInPatch(FishSwim __instance) {
				log.LogMessage($"Fade in\t{__instance.UniqueId},\tdist {Mathf.Round(GetFishDist(__instance))},\tposition {__instance.transform.position}");
			}

			[HarmonyPatch(typeof(FishSwim), nameof(FishSwim.FadeOut))]
			[HarmonyPostfix]
			public static void FadeOutPatch(FishSwim __instance) {
				log.LogMessage($"Fade out\t{__instance.UniqueId},\tdist {Mathf.Round(GetFishDist(__instance))},\tposition {__instance.transform.position}");
			}

			internal static float GetFishDist(FishSwim fish) {
				if (PlayerManager.Character == null || fish == null || fish.gameObject == null || !fish.gameObject.active) return float.MaxValue;
				float dist = Vector3.Distance(fish.transform.position, PlayerManager.Character.transform.position);
				return dist;
			}

			internal static int? GetNearestTreasureFishDist() {
				float shortest = 501f;
				foreach (var (_, fish) in treasureFishList) {
					float dist = GetFishDist(fish);
					shortest = Mathf.Min(shortest, dist);
				}
				return shortest >= 500f ? null : (int?)Mathf.Round(shortest);
			}

			internal static bool RemoveFishSparks(FishSwim fish) {
				var result = treasureFishList.Remove(fish.UniqueId);
				var name = fish.gameObject.name + "_Sparks";
				var sparkle = fish.gameObject.FindGameObject(name);
				if (sparkle != null) {
					GameObject.Destroy(sparkle);
				}
				return result;
			}

			internal static void LoadSparkPref() {
				var defOGItem = OnGroundItem.BaseAssetOnGroundItem;
				if (defOGItem == null) return;
				var sparkles = defOGItem.GetChildren(true);
				if (sparkles == null || sparkles.Count < 1) return;
				var particleOmitters = sparkles[0].GetChildren(true);
				if (particleOmitters == null || particleOmitters.Count < 1) return;
				sparkPref = particleOmitters[0];
				log.LogInfo($"Set default spark prefab");
			}

			internal static void AddFishSparks(FishSwim fish) {
				if (sparkPref == null) LoadSparkPref();
				if (sparkPref == null) return;
				var fishTf = fish.transform;
				if (fishTf == null) return;
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
				if (particle == null) return;
				particle.startSize = 1.5f;
				particle.startLifetime = 5f;
			}
		}
	}
}
