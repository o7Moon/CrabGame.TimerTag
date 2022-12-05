using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using BepInEx.IL2CPP.Utils.Collections;
using GameSettings = MonoBehaviourPublicObjomaOblogaTMObseprUnique;
using SteamManager = MonoBehaviourPublicObInUIgaStCSBoStcuCSUnique;
using GameModeTag = GameModePublicLi1UIUnique;
using ServerSend = MonoBehaviourPublicInInUnique;
using Packet = ObjectPublicIDisposableLi1ByInByBoUnique;
using GameServer = MonoBehaviourPublicObInCoIE85SiAwVoFoCoUnique;
using TimerUI = MonoBehaviourPublicTetifrTeStBoStfoSiTiUnique;

namespace TimerTag
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static bool modeEnabled = false;
        public static SteamManager steam;
        public static GameMode tagMode;
        public static Dictionary<ulong, float> timers;
        public static Toggle checkbox;
        public override void Load()
        {
            // Plugin startup logic
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
        [HarmonyPatch(typeof(GameSettings),"Start")]
        [HarmonyPostfix]
        public static void onGameSettingsCreated(GameSettings __instance){
            steam = SteamManager.Instance;
            Transform container = __instance.transform.Find("Container/Content");
            createCheckbox(container,"enable TimerTag ",__instance);
        }
        public static void checkToggled(bool value){
            modeEnabled = value;
            // scuffed, but works until i can figure out why working with checkbox textures at runtime is such a hassle
            checkbox.transform.Find("Label").GetComponent<Text>().text = "enable TimerTag " + (modeEnabled ? "(on)" : "(off)");
        }
        public static void createCheckbox(Transform parent, string label, MonoBehaviour behaviour){
            DefaultControls.Resources resources = new DefaultControls.Resources();
            GameObject toggle = DefaultControls.CreateToggle(resources);
            toggle.GetComponent<Toggle>().isOn = modeEnabled;
            checkbox = toggle.GetComponent<Toggle>();
            toggle.GetComponent<Toggle>().onValueChanged.AddListener((UnityAction<bool>)checkToggled);
            Text lbl = toggle.transform.Find("Label").GetComponent<Text>();
            lbl.text = label + (modeEnabled ? "(on)" : "(off)");
            lbl.color = Color.white;
            toggle.transform.SetParent(parent);
        }
        [HarmonyPatch(typeof(GameModeTag),nameof(GameModeTag.OnFreezeOver))]
        [HarmonyPostfix]
        public static void onFreezeOverHook(GameModeTag __instance){
            // only continue if hosting and timertag is enabled
            if (!(steam.prop_CSteamID_0 == steam.prop_CSteamID_1 && modeEnabled)) return;
            tagMode = __instance;
            timers = new Dictionary<ulong, float>();
        }
        [HarmonyPatch(typeof(ServerSend),nameof(ServerSend.SendGameModeTimer),new System.Type[]{typeof(System.UInt64),typeof(System.Single),typeof(System.Int32)})]
        [HarmonyPrefix]
        public static void ServerSendTimer(System.UInt64 __0, ref System.Single __1, System.Int32 __2){
            // if host
            if (steam.prop_CSteamID_0 == steam.prop_CSteamID_1){
                if (__0 == (ulong)steam.prop_CSteamID_0) return;
                if (tagMode != null) {
                    if (timers.ContainsKey(__0)) {
                        __1 = timers[__0];
                    } else {
                        __1 = 30f;
                    }
                    return;
                }
            }
            return;
        }
        [HarmonyPatch(typeof(GameMode),nameof(GameMode.Update))]
        [HarmonyPostfix]
        public static void onTagUpdate(GameMode __instance){
            // if null, then the custom mode isnt enabled
            if (tagMode == null) return;
            var tag = __instance.Cast<GameModeTag>();
            // each player in tagger list
            foreach (ulong id in tag.field_Private_List_1_UInt64_0){
                if (timers.ContainsKey(id)){
                    timers[id] -= Time.deltaTime;
                    if(timers[id] < 0){
                        GameServer.PlayerDied(id,(ulong)0,Vector3.zero);
                    }
                } else {
                    timers.Add(id,30f);
                }
            }
            // while there are still taggers, the timer shouldn't decrease any on the host's side, to prevent the game from ending
            if (!(tag.modeState == GameMode.EnumNPublicSealedvaFrPlEnGa5vUnique.GameOver || tag.modeState == GameMode.EnumNPublicSealedvaFrPlEnGa5vUnique.Ended)) {
                tag.freezeTimer.field_Private_Single_0 = 1.0f;
            }
            // though because of this the host's ui will need to be hooked to show their tag timer rather than the round timer
        }
        [HarmonyPatch(typeof(TimerUI),nameof(TimerUI.SlowUpdate))]
        [HarmonyPostfix]
        public static void PostTimerUIUpdate(TimerUI __instance){
            if (modeEnabled && tagMode != null && steam.prop_CSteamID_0 == steam.prop_CSteamID_1){
                __instance.timer.text = timers[(ulong)steam.prop_CSteamID_0].ToString();
            }
        }
    }
}
