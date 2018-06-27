
using LSPD_First_Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using LSPD_First_Response.Engine.Scripting.Entities;
using ImpCallouts;
using ImpCallouts.Callouts;

namespace ImpCallouts {
    public class Main : Plugin {

        public static Assembly LSPDFRResolveEventHandler(object sender, ResolveEventArgs args) {
            foreach (Assembly assembly in Functions.GetAllUserPlugins()) {
                if (args.Name.ToLower().Contains(assembly.GetName().Name.ToLower())) {
                    return assembly;
                }
            }
            return null;
        }

        public static bool IsLSPDFRPluginRunning(string Plugin, Version miniVersion = null) {
            foreach(Assembly assembly in Functions.GetAllUserPlugins()) {
                AssemblyName an = assembly.GetName();
                if(an.Name.ToLower() == Plugin.ToLower()) {
                    if(miniVersion == null || an.Version.CompareTo(miniVersion) >= 0) {
                        return true;
                    }
                }
            }
            return false;
        }
        
        public override void Initialize() {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(LSPDFRResolveEventHandler);
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
