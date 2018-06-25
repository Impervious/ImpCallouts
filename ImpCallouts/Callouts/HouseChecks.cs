using System;
using System.Drawing;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.Callouts;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Engine.Scripting.Entities;

namespace ImpCallouts.Callouts {

    [CalloutInfo("HouseCheck", CalloutProbability.High)]
    public class HouseChecks : Callout {
        public Vector3 SpawnPoint;
        public Vector3 susWalkTo;
        public Blip myBlip;
        public Ped mySuspect;

        public override bool OnBeforeCalloutDisplayed() {
            //SpawnPoint = World.GetNextPositionOnStreet(Game.LocalPlayer.Character.Position.Around(300f));
            SpawnPoint = new Vector3(469, 2617, 43);
            susWalkTo = new Vector3(474, 2590, 44);

            //If peds are valid, display the area the callout is in.
            this.ShowCalloutAreaBlipBeforeAccepting(SpawnPoint, 15f);
            this.AddMinimumDistanceCheck(5f, SpawnPoint);

            //Set the callout message(displayed in the notification), and the position(also shown in the notification)
            this.CalloutMessage = "US Route 68 House Check";
            this.CalloutPosition = SpawnPoint;

            //Play the scanner audio using SpawnPoint to identify "POSITION" stated in "IN_OR_ON_POSITION". These audio files can be found in GTA V > LSPDFR > Police Scanner.
            Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT_03 A_02 POSSIBLE_BNE CRIME_BREAK_AND_ENTER IN_OR_ON_POSITION", this.SpawnPoint);

            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted() {

            //Spawn mySuspect at SpawnPoint.
            mySuspect = new Ped(SpawnPoint) {

                //Set mySuspect as persistent, so it doesn't randomly disappear.
                IsPersistent = true,

                //Block permanent events from mySuspect so they don't react weirdly to different things from GTA V.
                BlockPermanentEvents = true
            };

            //Stops the callout from being displayed if the suspect can't spawn for some reason
            if (!mySuspect.Exists()) End();

            //Attach myBlip to mySuspect to show where they are.
            myBlip = mySuspect.AttachBlip();
            myBlip.Color = Color.Yellow;
            myBlip.Scale = 0.75F;
            myBlip.IsRouteEnabled = true;

            //Display a message to let the user know what to do.
            Game.DisplaySubtitle("Check the property~w~.", 7500);

            //Tells the officer which Code to run
            Functions.PlayScannerAudio("RESPOND_CODE_1");

            mySuspect.Tasks.FollowNavigationMeshToPosition(susWalkTo, 8F, 1);

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted() {
            base.OnCalloutNotAccepted();
            //Clean up what we spawned earlier, since the player didn't accept the callout.
            //This states that if mySuspect exists, then we need to delete it.
            if (mySuspect.Exists()) mySuspect.Delete();

            //This states that if myBlip exists, then we need to delete it.
            if (myBlip.Exists()) myBlip.Delete();
        }

        public override void Process() {
            base.Process();

            if((mySuspect.IsDead) || (mySuspect.IsCuffed)) {
                End();
            }
        }

        public override void End() {
            //This states that if mySuspect exists, then we need to dismiss it.
            if (mySuspect.Exists()) mySuspect.Dismiss();

            //Delete the blip attached to mySuspect.
            if (myBlip.Exists()) myBlip.Delete();

            base.End();
        }
    }
}