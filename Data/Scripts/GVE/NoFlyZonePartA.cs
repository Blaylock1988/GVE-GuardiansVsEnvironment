using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Components;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using System.IO;
using System.Runtime.Remoting.Messaging;
using Sandbox.Game.Entities;
using Sandbox.Game;
using VRage.Utils;

namespace NoFlyZone
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false, "NoFlyNpcLargeBlockBeacon")]
    public class NoFlyZone_Beacon : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase _objectBuilder;
        private IMyBeacon beacon;
        private IMyPlayer client;
        private IMyCharacter character;
        private VRage.Game.ModAPI.Interfaces.IMyControllableEntity controller;

        private TextWriter logger = null;
        private String timeofload = "" + DateTime.Now.Year + "." + DateTime.Now.Month + "." + DateTime.Now.Day + " " + DateTime.Now.Hour + "." + DateTime.Now.Minute + "." + DateTime.Now.Second;
        private bool logicEnabled = false;

		private float DefaultBeaconRadius = 9999999;
		private string lastCustomData = "";
		private int tickCount = 0;
		private bool InhibitEnabled = false;
		private bool InBeaconRange = false;
        private bool InDisablePlayerDampingRange = false;
        private bool InDisablePlayerThrustRange = false;
		private double MaxHeight = 50; 
		private int ZoneRange = 5000;
        private int NoDampingRange = 4000;
        private int NoThrustRange = 3000;

		//private bool IsWorking = false;
		//private bool IsNpcOwned = false;
		private bool InRange = false;


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            beacon = (Entity as IMyBeacon);
            NoFlyZone_Thruster.beaconList.Add(beacon);
            if (beacon != null)
            {
                logicEnabled = true;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                //NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            }

            client = MyAPIGateway.Session.LocalHumanPlayer;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            MyAPIGateway.Parallel.Start(delegate {

                try
                {
                    if (InhibitEnabled)//Check if beacon meets criteria
					{
                    
						var character = MyAPIGateway.Session?.LocalHumanPlayer?.Character as IMyCharacter;
						var controlledEntity = MyAPIGateway.Session?.LocalHumanPlayer?.Controller?.ControlledEntity?.Entity;
						
						if (character == null || character.IsDead || character != controlledEntity || ElevationThreshold(MyAPIGateway.Session.LocalHumanPlayer.GetPosition())) return; // check if character meets criteria

						if (InDisablePlayerDampingRange && character.EnabledDamping && character.EnabledThrusts)
						{
							character.SwitchDamping(); //do the player thruster disabling
						}
						if (InDisablePlayerThrustRange && character.EnabledThrusts)
						{
							character.SwitchThrusts(); //do the player damping disabling
						}
					}
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowMessage("NoFlyZone", "An error happened in the mod" + e);
                }
            });
        }

		public override void UpdateBeforeSimulation100() //Added all this stuff from MES JD Inhibitor code
		{
			
			base.UpdateBeforeSimulation100();

            MyAPIGateway.Parallel.Start(delegate {

                try
                {
					if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.LocalHumanPlayer != null) 
					{

						if (!logicEnabled || beacon == null || !beacon.IsWorking || beacon.OwnerId == 0 || MyAPIGateway.Players.TryGetSteamId(beacon.OwnerId) > 1)  //Check if beacon meets criteria
						{
							InhibitEnabled = false;
							return;
						}
						
						if (MyAPIGateway.Session.LocalHumanPlayer.Character.IsDead) return;

						InhibitEnabled = true;
						var distance = Vector3D.Distance(beacon.GetPosition(), MyAPIGateway.Session.LocalHumanPlayer.GetPosition());
						var inBeaconRange = distance <= ZoneRange;
						var inDisablePlayerDampingRange = (distance <= NoDampingRange && distance > NoThrustRange);
						var inDisablePlayerThrustRange = distance <= NoThrustRange;
						
						if (inBeaconRange && !InBeaconRange) 
						{

							MyVisualScriptLogicProvider.ShowNotificationLocal("[WARNING:] Entering Thruster Inhibitor Field!", 14000, "White");
							MyVisualScriptLogicProvider.ShowNotificationLocal("Jetpacks and Large Grid thrusters above 15m altutude will be disabled.", 15000, "White");

						}

						if (inDisablePlayerDampingRange && !InDisablePlayerDampingRange) 
						{

							MyVisualScriptLogicProvider.ShowNotificationLocal("[WARNING:] Jetpack Dampers above 15m altutude have been disabled!", 16000, "White");

						}

						if (inDisablePlayerThrustRange && !InDisablePlayerThrustRange) 
						{

							MyVisualScriptLogicProvider.ShowNotificationLocal("[WARNING:]Jetpacks and Large Grid thrusters above 15m altutude have been disabled!", 17000, "White");

						}
		
						InBeaconRange = inBeaconRange;
						InDisablePlayerDampingRange = inDisablePlayerDampingRange;
						InDisablePlayerThrustRange = inDisablePlayerThrustRange;
						
					}

					tickCount += 100;

					if (tickCount >= 100) {

						tickCount = 0;
						SetRange();

					}
				}
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowMessage("NoFlyZone", "An error happened in the mod" + e);
                }
            });

		}

		private bool ElevationThreshold(Vector3D location)
		{
			var planet = MyGamePruningStructure.GetClosestPlanet(location);
			if (planet == null) return false;

			Vector3D surfaceLocation = planet.GetClosestSurfacePointGlobal(location);
			return Vector3D.Distance(surfaceLocation, location) <= MaxHeight;
		}

		private void SetRange() {

			if (string.IsNullOrWhiteSpace(beacon.CustomData)) {

				beacon.CustomData = DefaultBeaconRadius.ToString();
				lastCustomData = DefaultBeaconRadius.ToString();
				beacon.Radius = DefaultBeaconRadius;
				return;

			}

			if (beacon.CustomData == lastCustomData)
				return;

			lastCustomData = beacon.CustomData;
			float result = 0;

			if (!float.TryParse(beacon.CustomData, out result))
				return;

			beacon.Radius = result;
			DefaultBeaconRadius = result;

		}

        private void Log(string text)
        {
            if (logger == null)
            {
                try
                {
                    logger = MyAPIGateway.Utilities.WriteFileInLocalStorage(this.GetType().Name + "-" + timeofload + ".Log", this.GetType());
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowMessage("AICombatLib", "Could not open the Log file:" + this.GetType().Name + "-" + timeofload + ".Log");
                    return;
                }
            }

            String datum = DateTime.Now.Year + "." + DateTime.Now.Month + "." + DateTime.Now.Day + " " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second;
            logger.WriteLine(datum + ": " + text);
            logger.Flush();

        }

        public override void Close()
        {
            if (Entity == null)
            {
                return;
            }
                

            if (NoFlyZone_Thruster.beaconList.Contains(beacon))
            {
                NoFlyZone_Thruster.beaconList.Remove(beacon);
            }
        }

        public override void OnRemovedFromScene()
        {

            base.OnRemovedFromScene();

            var Block = Entity as IMyBeacon;

            if (Block == null)
            {
                return;
            }

            //Unregister any handlers here

        }
    }
}

