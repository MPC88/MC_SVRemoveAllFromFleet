
using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.Button;

namespace MC_SVRemoveAllFromFleet
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string pluginGuid = "mc.starvalor.removeallfromfleet";
        public const string pluginName = "SV Remove All From Fleet";
        public const string pluginVersion = "1.0.0";

        private static GameObject btnRemoveAll;
        private static DockingUI dockingUI;
        private static Inventory inventory;
        private static PlayerControl pc;

        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));
        }

        [HarmonyPatch(typeof(DockingUI), nameof(DockingUI.OpenPanel))]
        [HarmonyPostfix]
        private static void DockingUI_OpenPanelPost(DockingUI __instance)
        {
            dockingUI = __instance;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Open))]
        [HarmonyPostfix]
        private static void Inventory_OpenPost(Inventory __instance)
        {
            EnableDisable();
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.SelectItem))]
        [HarmonyPostfix]
        private static void Inventory_SelectItemPost(Inventory __instance)
        {
            inventory = __instance;
            EnableDisable();
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.LoadItems))]
        [HarmonyPostfix]
        private static void Inventory_LoadItemsPost(Inventory __instance)
        {
            inventory = __instance;
            EnableDisable();
        }

        private static void EnableDisable()
        {
            if (dockingUI == null || !dockingUI.playerDocked || inventory == null)
            {
                if (btnRemoveAll != null)
                    btnRemoveAll.SetActive(false);

                return;
            }

            int cargoMode = (int)AccessTools.Field(typeof(Inventory), "cargoMode").GetValue(inventory);

            if (cargoMode != 2)
            {
                if (btnRemoveAll != null)
                    btnRemoveAll.SetActive(false);

                return;
            }

            int selectedSlot = (int)AccessTools.Field(typeof(Inventory), "selectedSlot").GetValue(inventory);

            if (selectedSlot < 0)
            {
                if (btnRemoveAll != null)
                    btnRemoveAll.SetActive(false);

                return;
            }

            if (btnRemoveAll == null)
                Initialise();

            btnRemoveAll.SetActive(true);
        }

        internal static void Initialise()
        {
            if (btnRemoveAll != null)
                return;

            GameObject src = (GameObject)AccessTools.Field(typeof(Inventory), "btnAddToFleet").GetValue(inventory);
            btnRemoveAll = GameObject.Instantiate(src);
            btnRemoveAll.name = "btnRemoveAllFromFleet";
            btnRemoveAll.transform.SetParent(src.transform.parent);
            btnRemoveAll.GetComponentInChildren<Text>().text = "Remove all from fleet";
            RectTransform rt = btnRemoveAll.GetComponent<RectTransform>();
            ButtonClickedEvent removeBCE = new Button.ButtonClickedEvent();
            removeBCE.AddListener(btnRemoveAll_Click);
            btnRemoveAll.GetComponentInChildren<Button>().onClick = removeBCE;
            btnRemoveAll.transform.localScale = src.transform.localScale;
            RectTransform btnRT = btnRemoveAll.GetComponent<RectTransform>();
            btnRemoveAll.transform.localPosition = new Vector3(src.transform.localPosition.x, src.transform.localPosition.y - btnRT.rect.height - 1.5f, src.transform.localPosition.z);
        }

        internal static void btnRemoveAll_Click()
        {
            InventorySlot[] slots = inventory.gameObject.GetComponentsInChildren<InventorySlot>();

            List<PlayerFleetMember> validFleetMembers = new List<PlayerFleetMember>();

            foreach (InventorySlot slot in slots)
            {
                if (slot.itemIndex < 0)
                    continue;

                if (slot.itemIndex < 0)
                    continue;

                PlayerFleetMember pfm = PChar.Char.mercenaries[slot.itemIndex] as PlayerFleetMember;
                if (pfm == null || (pfm.dockedStationID != dockingUI.station.id && !pfm.hangarDocked))
                    continue;

                validFleetMembers.Add(pfm);
            }

            if (pc == null)
                pc = GameManager.instance.Player.GetComponent<PlayerControl>();

            foreach (PlayerFleetMember fleetMemeber in validFleetMembers)
                RemoveFleetMember(fleetMemeber);

            pc.CalculateShip(changeWeapon: false);
            inventory.RefreshIfOpen(null, resetScreen: true, alsoUpdateShipInfo: true);
            AccessTools.Method(typeof(Inventory), "ResetScreen").Invoke(inventory, null);
            inventory.LoadItems();
            FleetControl.instance.Refresh(forceOpen: false);
        }

        internal static void RemoveFleetMember(PlayerFleetMember fleetMember)
        {
            ShipModelData shipModelData = fleetMember.ModelData();
            SpaceShipData shipData = fleetMember.shipData;
            ShipInfo shipInfo = (ShipInfo)AccessTools.Field(typeof(Inventory), "shipInfo").GetValue(inventory);
            CargoSystem cs = (CargoSystem)AccessTools.Field(typeof(Inventory), "cs").GetValue(inventory);
            if (shipData.HPPercent < 0.99f)
            {
                InfoPanelControl.inst.ShowWarning(Lang.Get(5, 296), 1, playAudio: true);
                return;
            }
            if (shipInfo.editingFleetShip == fleetMember)
            {
                shipInfo.StopEditingFleetShip();
            }
            int num = GameData.data.NewShipLoadout(null);
            GameData.data.SetShipLoadout(shipData, num);
            cs.StoreItem(4, shipData.shipModelID, shipModelData.rarity, 1, 0f, inventory.currStation.id, num);
            CrewMember crewMember = CrewDB.GetCrewMember(fleetMember.crewMemberID);
            crewMember.aiChar.behavior = fleetMember.behavior;
            cs.StoreItem(5, crewMember.id, crewMember.rarity, 1, 0f, inventory.currStation.id, -1);
            if (fleetMember.hangarDocked)
            {
                CarrierControl getCarrierControl = pc.GetCarrierControl;
                if ((bool)getCarrierControl)
                {
                    getCarrierControl.RemoveDockedShip(fleetMember);
                }
            }
            FleetControl.instance.CleanFleetSlot(fleetMember);
            PChar.Char.mercenaries.Remove(fleetMember);
            pc.RemoveMercenaryGO(fleetMember);
            SoundSys.PlaySound(11, keepPlaying: true);
        }
    }
}