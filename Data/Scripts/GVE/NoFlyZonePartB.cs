using System;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using VRage.Utils;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game;
using Sandbox.Definitions;


namespace NoFlyZone
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    public class NoFlyZone_Thruster : MyGameLogicComponent
    {
		private IMyPowerProducer power;
        private IMyThrust thruster;
        private IMyCubeBlock thrustdef;
        private IMyPlayer client;
		private double MaxHeight = 15; 
		private int ThrusterDisableDistance = 3000; //distance from beacon
        private bool isServer;
        private bool inZone;
        public static List<IMyBeacon> beaconList = new List<IMyBeacon>();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            thruster = (Entity as IMyThrust);

            if (thruster != null || power != null)
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            isServer = MyAPIGateway.Multiplayer.IsServer;
            client = MyAPIGateway.Session.LocalHumanPlayer;

        }

        public override void UpdateBeforeSimulation10()
        {

            base.UpdateBeforeSimulation10();

            try
            {
                if (isServer)
                {
					
                    if (thruster == null || !thruster.Enabled || thruster.CubeGrid.GridSizeEnum == MyCubeSize.Small) return;

 					string strSubBlockType = thruster.BlockDefinition.SubtypeId.ToString();
					bool isHoverThruster = false;
					isHoverThruster = strSubBlockType.Contains("Hover");

					if (isHoverThruster) return;

                    foreach (var beacon in beaconList)
                    {                        
                        if (beacon == null || !beacon.IsWorking) continue;

                        if (Vector3D.Distance(thruster.GetPosition(), beacon.GetPosition()) <= ThrusterDisableDistance)
                        {

							if (!ElevationThreshold(thruster.GetPosition()))
							{
								thruster.Enabled = false;
								return;
							}
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                MyLog.Default.WriteLineAndConsole($"Failed looping through beacon list: {exc}");
            }
        }

		private bool ElevationThreshold(Vector3D location)
		{
			var planet = MyGamePruningStructure.GetClosestPlanet(location);
			if (planet == null) return false;

			Vector3D surfaceLocation = planet.GetClosestSurfacePointGlobal(location);
			return Vector3D.Distance(surfaceLocation, location) <= MaxHeight;
		}

        public override void Close()
        {
            if (Entity == null)
                return;
        }

        public override void OnRemovedFromScene()
        {

            base.OnRemovedFromScene();

            if (Entity == null || Entity.MarkedForClose)
            {
                return;
            }

            var T_Block = Entity as IMyThrust;

            if (T_Block == null) return;

        }
    }
}
