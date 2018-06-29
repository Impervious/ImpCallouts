using System;
using System.Drawing;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.Callouts;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Engine.Scripting.Entities;
using ComputerPlus;

namespace ImpCallouts.Callouts {

    [CalloutInfo("Disturbance", CalloutProbability.High)]
    public class Disturbance : Callout {

        public Vector3 SpawnPoint;
        public Vector3 VehSpawnPoint;
        public Vector3 SusSpawnPoint;
        public Vector3 MechSpawnPoint;
        public Blip coBlip;
        public Ped suspect;
        public Ped mechanic;
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

            //Sets the spawn points for various things.            
            SpawnPoint = new Vector3();
            SusSpawnPoint = new Vector3(1179, 2652, 37);
            MechSpawnPoint = new Vector3(1180, 2650, 37);
            VehSpawnPoint = new Vector3(1177, 2650, 37);

            //If peds are valid, display the area the callout is in.
            this.ShowCalloutAreaBlipBeforeAccepting(SpawnPoint, 15f);
            this.AddMinimumDistanceCheck(5f, SpawnPoint);

            //Set the callout message, and the position.
            this.CalloutMessage = "Public Disturbance";
            this.CalloutPosition = SpawnPoint;

            //Play the scanner audio using SpawnPoint.

            /*
             * Need to update PlayScannerAudioUsingPosition 
             */
            Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT_03 A_02 POSSIBLE_BNE CRIME_BREAK_AND_ENTER IN_OR_ON_POSITION", this.SpawnPoint);

            computerPlusRunning = Main.IsLSPDFRPluginRunning("ComputerPlus", new Version("1.3.0.0"));

            if (computerPlusRunning) {
                callID = ComputerPlusWrapperClass.CreateCallout("Public Disturbance in Harmony", "Public Disturbance", SpawnPoint, (int)EResponseType.Code_3);
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

            if(calloutState.Equals(ECalloutState.EnRoute)) {
                NativeFunction.Natives.ClearAreaOfPeds(SpawnPoint, 500f, 0);
            }

            suspect = new Ped("s_m_m_autoshop_02", SusSpawnPoint, 202f) {
                
                IsPersistent = true,
                BlockPermanentEvents = true
            };

            mechanic = new Ped("a_m_y_stwhi_02", MechSpawnPoint, 20f) {

                IsPersistent = true,
                BlockPermanentEvents = true
            };

            susVehicle = new Vehicle("", VehSpawnPoint);
            susVehicle.IsPersistent = true;
            
            //If one or both of the peds can't spawn for some reason end the callout
            if (!suspect.Exists()) End();
            if (!mechanic.Exists()) End();

            coBlip = suspect.AttachBlip();
            coBlip.Color = Color.Yellow;
            coBlip.Scale = 0.75f;
            coBlip.IsRouteEnabled = true;

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted() {

            if(computerPlusRunning) {
                ComputerPlusWrapperClass.AssignCallToAIUnit(callID);
            }

            if (suspect.Exists()) suspect.Delete();
            if (mechanic.Exists()) mechanic.Delete();
            if (coBlip.Exists()) coBlip.Delete();

            base.OnCalloutNotAccepted();
        }

        public override void Process() {

            NativeFunction.Natives.ClearAreaOfPeds(SpawnPoint, 500f, 0);
            if(calloutState == ECalloutState.EnRoute && Game.LocalPlayer.Character.Position.DistanceTo2D(SpawnPoint) <= 25f) {
                if(computerPlusRunning) {
                    ComputerPlusWrapperClass.SetCalloutStatusToAtScene(callID);
                    ComputerPlusWrapperClass.AddUpdateToCallout(callID, "Officer arrived at scene.");
                    ComputerPlusWrapperClass.AddPedToCallout(callID, suspect);
                    ComputerPlusWrapperClass.AddPedToCallout(callID, mechanic);
                    coBlip.IsRouteEnabled = false;
                }
                coBlip.IsRouteEnabled = false;
                calloutState = ECalloutState.OnScene;
            }

            if((suspect.IsDead) || (suspect.IsCuffed)) {
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

                //Suspect & Mechanic fight
                if (r == 1) {
                    if ((suspect.IsAlive) && (mechanic.IsAlive)) {
                        if(player.DistanceTo2D(suspect.Position) > 20f) {
                            suspect.IsInvincible = true;
                            mechanic.IsInvincible = true;
                            suspect.Tasks.FightAgainst(mechanic);
                        } else if((player.IsAiming) && player.DistanceTo2D(suspect.Position) <= 20f) {
                            suspect.MaxHealth = 200;
                            mechanic.MaxHealth = 200;
                        }
                    } else {
                        End();
                    }                        
                }

                //Suspect gets in his vehicle and takes off
                if (r == 2) {
                    if ((suspect.IsAlive) && (mechanic.IsAlive)) {
                        suspect.Tasks.EnterVehicle(susVehicle, 15000, -1).WaitForCompletion(10000);
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

                //Mechanic starts damaging suspectes vehicle
                if (r == 3) {
                    if ((suspect.IsAlive) && (mechanic.IsAlive)) {
                        mechanic.Inventory.GiveNewWeapon("WEAPON_UNARMED", -1, true);
                        mechanic.Tasks.FireWeaponAt(susVehicle, 5000, FiringPattern.FullAutomatic);
                    } else {
                        End();
                    }
                }
            });
        }
    }
}