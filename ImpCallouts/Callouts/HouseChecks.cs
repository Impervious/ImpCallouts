using System;
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
        public Blip myBlip;
        public Ped mySuspect;
        public Vehicle myVehicle;
        public LHandle pursuit;
        Guid callID;
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

            //Set the callout message(displayed in the notification), and the position(also shown in the notification)
            this.CalloutMessage = "US Route 68 House Check";
            this.CalloutPosition = SpawnPoint;

            //Play the scanner audio using SpawnPoint to identify "POSITION" stated in "IN_OR_ON_POSITION". These audio files can be found in GTA V > LSPDFR > Police Scanner.
            Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT_03 A_02 POSSIBLE_BNE CRIME_BREAK_AND_ENTER IN_OR_ON_POSITION", this.SpawnPoint);

            computerPlusRunning = Main.IsLSPDFRPluginRunning("ComputerPlus", new Version("1.3.0.0"));

            if (computerPlusRunning) {
                callID = ComputerPlusWrapperClass.CreateCallout("Harmony House Check", "Harmony House Check", SpawnPoint, (int)EResponseType.Code_2);
            } else {
                Game.DisplayHelp("Computer+ not installed.");
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
            }

            calloutState = ECalloutState.EnRoute;

            //Spawn mySuspect at SpawnPoint.
            mySuspect = new Ped("g_m_m_chicold_01", SpawnPoint, 0F) {

                //Set mySuspect as persistent, so it doesn't randomly disappear.
                IsPersistent = true,

                //Block permanent events from mySuspect.
                BlockPermanentEvents = true
            };

            myVehicle = new Vehicle("YOUGA", VehSpawnPoint);
            myVehicle.IsPersistent = true;

            //Stops the callout from being displayed if the suspect can't spawn for some reason
            if (!mySuspect.Exists()) End();

            //Attach myBlip to mySuspect to show where they are.
            myBlip = mySuspect.AttachBlip();
            myBlip.Color = Color.Yellow;
            myBlip.Scale = 0.75F;
            myBlip.IsRouteEnabled = true;
            
            Functions.PlayScannerAudioUsingPosition("UNITS_RESPOND_CODE_02_02", this.SpawnPoint);

            //mySuspect.Tasks.FollowNavigationMeshToPosition(susWalkTo, 8F, 1);

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted() {

            if (computerPlusRunning) {
                ComputerPlusWrapperClass.AssignCallToAIUnit(callID);
            }
            if (mySuspect.Exists()) mySuspect.Delete();
            if (myBlip.Exists()) myBlip.Delete();

            base.OnCalloutNotAccepted();
        }

        public override void Process() {

            if (calloutState == ECalloutState.EnRoute && Game.LocalPlayer.Character.Position.DistanceTo2D(SpawnPoint) <= 25F) {
                if (computerPlusRunning) {
                    ComputerPlusWrapperClass.SetCalloutStatusToAtScene(callID);
                    ComputerPlusWrapperClass.AddUpdateToCallout(callID, "Officer arrived at scene.");
                    ComputerPlusWrapperClass.AddPedToCallout(callID, mySuspect);
                    myBlip.IsRouteEnabled = false;
                }
                myBlip.IsRouteEnabled = false;
                calloutState = ECalloutState.OnScene;
                StartSuspectScenarios();
            }

            if ((mySuspect.IsDead) || (mySuspect.IsCuffed)) {
                End();
            }
            base.Process();
        }

        public override void End() {

            if (computerPlusRunning) {
                ComputerPlusWrapperClass.ConcludeCallout(callID);
            }

            if (mySuspect.Exists()) mySuspect.Dismiss();
            if (myBlip.Exists()) myBlip.Delete();
            if (myVehicle.Exists()) myVehicle.Dismiss();

            base.End();
        }

        private void StartSuspectScenarios() {
            GameFiber.StartNew(delegate {
                int r = new Random().Next(1, 7);
                calloutState = ECalloutState.DecisionMade;
                Game.HideHelp();

                //Suspect flees on foot
                if (r == 1) {
                    if (mySuspect.IsAlive) {
                        if (!pursuitCreated && Game.LocalPlayer.Character.DistanceTo2D(mySuspect.Position) < 35F) {
                            pursuit = Functions.CreatePursuit();
                            Functions.AddPedToPursuit(pursuit, mySuspect);
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
                    if (mySuspect.IsAlive) {
                        susRunTo = new Vector3(462, 2593, 43);
                        mySuspect.Tasks.FollowNavigationMeshToPosition(susRunTo, 243, 2).WaitForCompletion(10000);
                        mySuspect.Tasks.EnterVehicle(myVehicle, 15000, -1).WaitForCompletion(5000);
                        if (!pursuitCreated && Game.LocalPlayer.Character.DistanceTo2D(mySuspect.Position) < 35F) {
                            pursuit = Functions.CreatePursuit();
                            Functions.AddPedToPursuit(pursuit, mySuspect);
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
                    Game.DisplayHelp("yes");
                    mySuspect.Inventory.GiveNewWeapon("WEAPON_KNIFE", -1, true);
                    if (mySuspect.IsAlive) {
                        mySuspect.Tasks.FightAgainst(player);
                    } else {
                        End();
                    }
                }
            });
        }
    }
}