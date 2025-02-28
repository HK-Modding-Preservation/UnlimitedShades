using System.Collections.Generic;
using System.Linq;
using Modding;
using UnityEngine;
using HutongGames.PlayMaker;
using Satchel;

namespace UnlimitedShades {
    class UnlimitedShades : Mod, ILocalSettings<LocalShadeSettings> {
        new public string GetName() => "Unlimited Shades";
        public override string GetVersion() => "1.2.0.0";

        public static LocalShadeSettings shadeData { get; set; } = new LocalShadeSettings();
        public void OnLoadLocal(LocalShadeSettings s) => shadeData = s;
        public LocalShadeSettings OnSaveLocal() => shadeData;

        public static int jijiShadeIndex = 0;

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
            On.PlayMakerFSM.OnEnable += editFSM;
            On.SceneManager.Start += sceneStart;
        }

        private void editFSM(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
            orig(self);
            if(self.gameObject.name == "Hero Death" && self.FsmName == "Hero Death Anim") {
                if(!self.TryGetState("isCustom", out FsmState state)) {
                    self.AddAction("Remove Geo", new shadeStoreGeo());
                    self.InsertAction("Set Shade", new shadeGatherData(), 25);
                    self.AddAction("Shade Marker Check", new shadePositionNoMarker());
                    self.AddAction("Colosseum Shade", new shadePositionAction());
                    self.AddAction("Final Boss Shade", new shadePositionAction());
                    for(int i = 1; i <= 5; i++) {
                        self.AddAction("MP " + i, new shadeMpAction());
                    }
                    self.AddState("isCustom");
                }
            }
            else if((self.gameObject.name == "Hollow Shade Death(Clone)" || self.gameObject.name.StartsWith("tmgCustomDeath")) && self.FsmName == "Shade Control") {
                self.InsertAction("Death Start", new returnGeo(), 2);
                self.InsertAction("Death Start", new cleanupShadeData(), 11);
            }
            else if((self.gameObject.name == "Hollow Shade(Clone)" || self.gameObject.name.StartsWith("tmgCustomShade")) && self.FsmName == "Shade Control") {
                self.InsertAction("Init", new shadeGetMPnHP(), 6);
                self.InsertAction("Special Type", new shadeGetSpecialAndZone(), 0);
                self.InsertAction("Killed", new shadeRenameDeath(), 3);
            }
            else if(self.gameObject.name == "Jiji NPC" && self.FsmName == "Conversation Control") {
                self.GetValidState("Has Shade?").InsertAction(new jijiHasShade(), 0);
                FsmState yesState = self.GetValidState("Yes");
                yesState.RemoveAction(7);
                yesState.RemoveAction(3);
                yesState.RemoveAction(0);
                yesState.InsertAction(new jijiSetupShade(), 0);
                FsmState spawnState = self.GetValidState("Spawn");
                spawnState.RemoveAction(13);
                spawnState.AddAction(new jijiSpawnShade());
            }
        }

        private void sceneStart(On.SceneManager.orig_Start orig, SceneManager self) {
            orig(self);
            if(GameManager.instance.IsGameplayScene()) {
                GameObject.Destroy(GameObject.Find("Hollow Shade(Clone)"));
                
                string currentScene = self.gameObject.scene.name;
                Dictionary<int, string> list = shadeData.shadeSceneList;
                if(list == null) {
                    return;
                }
                Dictionary<int, string>.KeyCollection keys = list.Keys;

                foreach(int key in keys) {
                    string scene = shadeData.shadeSceneList[key];
                    if(scene == currentScene) {
                        spawnShade(key);
                    }
                }
            }
        }

        public static void spawnShade(int index) {
            float[] pos = shadeData.shadePositionList[index];
            GameObject shade = GameObject.Instantiate(GameManager.instance.sm.hollowShadeObject, new Vector3(pos[0], pos[1], 0.006f), Quaternion.identity);
            shade.transform.SetParent(GameManager.instance.sm.transform, worldPositionStays: true);
            shade.transform.SetParent(null);
            shade.name = "tmgCustomShade" + index;
        }
    }

    public class shadeStoreGeo: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            LocalShadeSettings sd = UnlimitedShades.shadeData;

            if(sd.shadeGeoList == null) {
                return;
            }
            int index = sd.shadeIndex + 1;
            int geo = pd.GetVariable<int>("geoPool");
            if(sd.shadeGeoList.ContainsKey(index)) {
                sd.shadeGeoList[index] = geo;
            }
            else {
                sd.shadeGeoList.Add(index, geo);
            }
            Finish();
        }
    }

    public class shadeGatherData: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            LocalShadeSettings sd = UnlimitedShades.shadeData;

            int index = ++sd.shadeIndex;

            string sceneName = pd.GetString("shadeScene");
            if(sd.shadeSceneList.ContainsKey(index)) {
                sd.shadeSceneList[index] = sceneName;
            }
            else {
                sd.shadeSceneList.Add(index, sceneName);
            }

            string mapZone = pd.GetString("shadeMapZone");
            if(sd.shadeMapZoneList.ContainsKey(index)) {
                sd.shadeMapZoneList[index] = mapZone;
            }
            else {
                sd.shadeMapZoneList.Add(index, mapZone);
            }

            int specialType = pd.GetInt("shadeSpecialType");
            if(sd.shadeSpecialList.ContainsKey(index)) {
                sd.shadeSpecialList[index] = specialType;
            }
            else {
                sd.shadeSpecialList.Add(index, specialType);
            }

            float shadeX = pd.GetFloat("shadePositionX");
            float shadeY = pd.GetFloat("shadePositionY");
            if(sd.shadePositionList.ContainsKey(index)) {
                sd.shadePositionList[index] = new float[] { shadeX, shadeY };
            }
            else {
                sd.shadePositionList.Add(index, new float[] { shadeX, shadeY });
            }

            int shadeHP = pd.GetInt("shadeHealth");
            if(sd.shadeHealthList.ContainsKey(index)) {
                sd.shadeHealthList[index] = shadeHP;
            }
            else {
                sd.shadeHealthList.Add(index, shadeHP);
            }

            Finish();
        }
    }

    public class shadePositionNoMarker: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            LocalShadeSettings sd = UnlimitedShades.shadeData;
            int index = sd.shadeIndex;

            float shadeX = pd.GetFloat("shadePositionX");
            float shadeY = pd.GetFloat("shadePositionY");
            if(sd.shadePositionList.ContainsKey(index)) {
                sd.shadePositionList[index] = new float[] { shadeX, shadeY };
            }
            else {
                sd.shadePositionList.Add(index, new float[] { shadeX, shadeY });
            }

            Finish();
        }
    }

    public class shadePositionAction: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            LocalShadeSettings sd = UnlimitedShades.shadeData;
            int index = sd.shadeIndex;

            string sceneName = pd.GetString("shadeScene");
            if(sd.shadeSceneList.ContainsKey(index)) {
                sd.shadeSceneList[index] = sceneName;
            }
            else {
                sd.shadeSceneList.Add(index, sceneName);
            }

            float shadeX = pd.GetFloat("shadePositionX");
            float shadeY = pd.GetFloat("shadePositionY");
            if(sd.shadePositionList.ContainsKey(index)) {
                sd.shadePositionList[index] = new float[] { shadeX, shadeY };
            }
            else {
                sd.shadePositionList.Add(index, new float[] { shadeX, shadeY });
            }
            Finish();
        }
    }

    public class shadeMpAction: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            LocalShadeSettings sd = UnlimitedShades.shadeData;
            int index = sd.shadeIndex;
            int mp = pd.GetInt("shadeMP");
            if(sd.shadeMPList.ContainsKey(index)) {
                sd.shadeMPList[index] = mp;
            }
            else {
                sd.shadeMPList.Add(index, mp);
            }
            Finish();
        }
    }

    public class shadeRenameDeath: FsmStateAction {
        public override void OnEnter() {
            GameObject shadeDeath = Fsm.FsmComponent.FsmVariables.GetFsmGameObject("Corpse").Value;
            shadeDeath.name = "tmgCustomDeath" + int.Parse(Fsm.GameObject.name.Substring(14));
            Finish();
        }
    }

    public class returnGeo: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            string name = Fsm.GameObject.name;
            int id = int.Parse(name.Substring(14));
            int geo = UnlimitedShades.shadeData.shadeGeoList[id];
            pd.SetVariable<int>("geoPool", geo);
            Finish();
        }
    }

    public class cleanupShadeData: FsmStateAction {
        public override void OnEnter() {
            LocalShadeSettings sd = UnlimitedShades.shadeData;
            string name = Fsm.GameObjectName;
            int id = int.Parse(name.Substring(14));
            sd.shadeSceneList.Remove(id);
            sd.shadeMapZoneList.Remove(id);
            sd.shadePositionList.Remove(id);
            sd.shadeHealthList.Remove(id);
            sd.shadeMPList.Remove(id);
            sd.shadeSpecialList.Remove(id);
            sd.shadeMapPosList.Remove(id);
            sd.shadeGeoList.Remove(id);
            Finish();
        }
    }

    public class shadeGetMPnHP: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            string name = Fsm.GameObject.name;
            int id = int.Parse(name.Substring(14));
            int mp = UnlimitedShades.shadeData.shadeMPList[id];
            int hp = UnlimitedShades.shadeData.shadeHealthList[id];
            pd.SetVariable<int>("shadeMP", mp);
            pd.SetVariable<int>("shadeHealth", hp);
            Finish();
        }
    }

    public class shadeGetSpecialAndZone: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            string name = Fsm.GameObject.name;
            int id = int.Parse(name.Substring(14));
            int special = UnlimitedShades.shadeData.shadeSpecialList[id];
            string zone = UnlimitedShades.shadeData.shadeMapZoneList[id];
            pd.SetInt("shadeSpecialType", special);
            pd.SetString("shadeMapZone", zone);
            Finish();
        }
    }

    public class jijiHasShade: FsmStateAction {
        public override void OnEnter() {
            PlayerData pd = PlayerData.instance;
            LocalShadeSettings sd = UnlimitedShades.shadeData;
            if(sd.shadeSceneList.Count == 0) {
                Fsm.Event(FsmEvent.GetFsmEvent("NO"));
            }
            else {
                int firstIndex = UnlimitedShades.jijiShadeIndex = sd.shadeSceneList.Keys.First();
                pd.shadeScene = sd.shadeSceneList[firstIndex];
                pd.shadeMapZone = sd.shadeMapZoneList[firstIndex];
                Fsm.Event(FsmEvent.GetFsmEvent("YES"));
            }
        }
    }

    public class jijiSetupShade: FsmStateAction {
        public override void OnEnter() {
            LocalShadeSettings sd = UnlimitedShades.shadeData;
            int jIndex = UnlimitedShades.jijiShadeIndex;
            sd.shadePositionList[jIndex][0] = 25.10169f;
            sd.shadePositionList[jIndex][1] = 10.59f;
            sd.shadeSceneList[jIndex] = "Room_Ouiji";

        }
    }

    public class jijiSpawnShade: FsmStateAction {
        public override void OnEnter() {
            if(UnlimitedShades.shadeData.shadePositionList.ContainsKey(UnlimitedShades.jijiShadeIndex)) {
                UnlimitedShades.spawnShade(UnlimitedShades.jijiShadeIndex);
            }
            Finish();
        }
    }

    public class LocalShadeSettings {
        public int shadeIndex = 0;
        public Dictionary<int, string> shadeSceneList = new();
        public Dictionary<int, string> shadeMapZoneList = new();
        public Dictionary<int, float[]> shadePositionList = new();
        public Dictionary<int, int> shadeHealthList = new();
        public Dictionary<int, int> shadeMPList = new();
        public Dictionary<int, int> shadeSpecialList = new();
        public Dictionary<int, int> shadeGeoList = new();
        public Dictionary<int, Vector3> shadeMapPosList = new();
    }
}