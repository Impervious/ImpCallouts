﻿using System;
using System.Drawing;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.Callouts;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Engine.Scripting.Entities;
using ComputerPlus;

namespace ImpCallouts.Callouts {

    [CalloutInfo("HouseCheck", CalloutProbability.High)]
    public class HouseChecks : Callout {

        public Vector3 SpawnPoint;
        public Vector3 VehSpawnPoint;
        public Vector3 susRunTo;
        public Blip coBlip;
        public Ped suspect;
        public Vehicle susVehicle;
        public LHandle pursuit;
        public Guid callID;
        bool computerPlusRunning;

        private bool pursuitCreated = false;

        private Ped player => Game.LocalPlayer.Character;

        private enum ECalloutState {
            None = 0,
            EnRoute,
            OnScene,
            DecisionMade
        };

        ECalloutState calloutState = ECalloutState.None;

        public override bool OnBeforeCalloutDisplayed() {
            
            VehSpawnPoint = new Vector3(466, 2592, 43);
            SpawnPoint = new Vector3(474, 2590, 44);

            //If peds are valid, display the area the callout is in.
            this.ShowCalloutAreaBlipBeforeAccepting(SpawnPoint, 15f);
            this.AddMinimumDistanceCheck(5f, SpawnPoint);

            //Set the callout message, and the position
            this.CalloutMessage = "US Route 68 House Check";
            this.CalloutPosition = SpawnPoint;

            //Play the scanner audio using SpawnPoint..
            Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT_03 A_02 POSSIBLE_BNE CRIME_BREAK_AND_ENTER IN_OR_ON_POSITION", this.SpawnPoint);

            computerPlusRunning = Main.IsLSPDFRPluginRunning("ComputerPlus", new Version("1.3.0.0"));

            if (computerPlusRunning) {
                callID = ComputerPlusWrapperClass.CreateCallout("Harmony House Check", "Harmony House Check", SpawnPoint, (int)EResponseType.Code_2);
            }

            return base.OnBeforeCalloutDisplayed();
        }

        public override void OnCalloutDisplayed() {

            if (computerPlusRunning) {
                ComputerPlusWrapperClass.UpdateCalloutStatus(callID, (int)ECallStatus.Dispatched);
            }

            base.OnCalloutDisplayed();
        }

        public override bool OnCalloutAccepted() {

            if (computerPlusRunning) {
                ComputerPlusWrapperClass.SetCalloutStatusToUnitResponding(callID);
                Game.DisplayHelp("Check Computer+ for further call details");
                ComputerPlusWrapperClass.UpdateCalloutDescription(callID, "Caller states a van with 1 occupant pulled up to their neighbours house while they're on vacation and started looking around in the backyard.");
            }

            calloutState = ECalloutState.EnRoute;

            if (calloutState.Equals(ECalloutState.EnRoute)) {
                NativeFunction.Natives.ClearAreaOfPeds(SpawnPoint, 500F, 0);
            }

            //Spawn suspect at SpawnPoint.
            suspect = new Ped("g_m_m_chicold_01", SpawnPoint, 0F) {

                //Set suspect as persistent, so it doesn't randomly disappear.
                IsPersistent = true,

                //Block permanent events from suspect.
                BlockPermanentEvents = true
            };
            
            susVehicle = new Vehicle("YOUGA", VehSpawnPoint);
            NativeFunction.Natives.SET_VEHICLE_CUSTOM_PRIMARY_COLOUR(susVehicle, 0, 0, 0);
            NativeFunction.Natives.SET_VEHICLE_CUSTOM_SECONDARY_COLOUR(susVehicle, 0, 0, 0);
            susVehicle.IsPersistent = true;

            //Stops the callout from being displayed if the suspect can't spawn for some reason
            if (!suspect.Exists()) End();

            //Attach coBlip to suspect to show where they are.
            coBlip = suspect.AttachBlip();
            coBlip.Color = Color.Yellow;
            coBlip.Scale = 0.75f;
            coBlip.IsRouteEnabled = true;
            
            Functions.PlayScannerAudioUsingPosition("UNITS_RESPOND_CODE_02_02", this.SpawnPoint);

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted() {

            if (computerPlusRunning) {
                ComputerPlusWrapperClass.AssignCallToAIUnit(callID);
            }
            if (suspect.Exists()) suspect.Delete();
            if (coBlip.Exists()) coBlip.Delete();

            base.OnCalloutNotAccepted();
        }

        public override void Process() {

            NativeFunction.Natives.ClearAreaOfPeds(SpawnPoint, 500F, 0);
            if (calloutState == ECalloutState.EnRoute && Game.LocalPlayer.Character.Position.DistanceTo2D(SpawnPoint) <= 25F) {
                if (computerPlusRunning) {
                    ComputerPlusWrapperClass.SetCalloutStatusToAtScene(callID);
                    ComputerPlusWrapperClass.AddUpdateToCallout(callID, "Officer arrived at scene.");
                    ComputerPlusWrapperClass.AddPedToCallout(callID, suspect);
                    coBlip.IsRouteEnabled = false;
                }                
                coBlip.IsRouteEnabled = false;
                calloutState = ECalloutState.OnScene;
                StartSuspectScenarios();
            }

            if ((suspect.IsDead) || (suspect.IsCuffed)) {
                End();
            }

            base.Process();
        }

        public override void End() {

            if (computerPlusRunning) {
                ComputerPlusWrapperClass.ConcludeCallout(callID);
            }

            if (suspect.Exists()) suspect.Dismiss();
            if (coBlip.Exists()) coBlip.Delete();
            if (susVehicle.Exists()) susVehicle.Dismiss();

            base.End();
        }

        private void StartSuspectScenarios() {

            GameFiber.StartNew(delegate {
                int r = new Random().Next(1, 4);
                calloutState = ECalloutState.DecisionMade;
                Game.HideHelp();

                //Suspect flees on foot
                if (r == 1) {
                    if (suspect.IsAlive) {
                        if (!pursuitCreated && Game.LocalPlayer.Character.DistanceTo2D(suspect.Position) < 35F) {
                            pursuit = Functions.CreatePursuit();
                            Functions.AddPedToPursuit(pursuit, suspect);
                            Functions.SetPursuitIsActiveForPlayer(pursuit, true);
                            pursuitCreated = true;
                        } else if (pursuitCreated && !Functions.IsPursuitStillRunning(pursuit)) {
                            End();
                        }
                    } else {
                        End();
                    }
                }

                //Suspect flees in YOUGA
                if (r == 2) {
                    if (suspect.IsAlive) {
                        susRunTo = new Vector3(464, 2592, 43);
                        suspect.Tasks.FollowNavigationMeshToPosition(susRunTo, 243, 2).WaitForCompletion(4500);
                        suspect.Tasks.EnterVehicle(susVehicle, 15000, -1).WaitForCompletion(2500);
                        if (!pursuitCreated && Game.LocalPlayer.Character.DistanceTo2D(suspect.Position) < 35F) {
                            pursuit = Functions.CreatePursuit();
                            Functions.AddPedToPursuit(pursuit, suspect);
                            Functions.SetPursuitIsActiveForPlayer(pursuit, true);
                            pursuitCreated = true;
                        } else if (pursuitCreated && !Functions.IsPursuitStillRunning(pursuit)) {
                            End();
                        }
                    } else {
                        End();
                    }
                }

                //Suspect runs at officer with knife
                if (r == 3) {
                    suspect.Inventory.GiveNewWeapon("WEAPON_KNIFE", -1, true);
                    if (suspect.IsAlive) {
                        suspect.Tasks.FightAgainst(player);
                    } else {
                        End();
                    }
                }
            });
        }
    }
}