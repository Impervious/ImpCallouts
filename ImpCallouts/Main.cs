
using LSPD_First_Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using LSPD_First_Response.Engine.Scripting.Entities;
using ImpCallouts;
using ImpCallouts.Callouts;

namespace ImpCallouts {
    public class Main : Plugin {
        
        public override void Initialize() {
            //When our OnDuty status is changed, it calls OnOnDutyStateChangedHandler.
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;

            //Logs stuff to RagePluginHook.log" 
            Game.LogTrivial("ImpCallouts " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " has been initialised.");
            Game.LogTrivial("Go on duty to fully load ImpCallouts.");
        }

        //Cleans up the plugin.
        public override void Finally() {
            Game.LogTrivial("ImpCallouts has been cleaned up.");
        }
        
        private static void OnOnDutyStateChangedHandler(bool OnDuty) {
            if (OnDuty) {
                RegisterCallouts();

                //Shows a notification above the minimap when you go on duty.
                Game.DisplayNotification("ImpCallouts has been loaded.");
            }
        }

        private static void RegisterCallouts() {
            Functions.RegisterCallout(typeof(HouseChecks));
        }
    }
}
