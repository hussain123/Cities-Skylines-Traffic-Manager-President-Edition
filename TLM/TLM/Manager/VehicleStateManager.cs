﻿#define USEPATHWAITCOUNTERx

using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Manager {
	public class VehicleStateManager : AbstractCustomManager {
		public static readonly VehicleStateManager Instance = new VehicleStateManager();

		public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail;
		public const VehicleInfo.VehicleType RECKLESS_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		public const float MIN_SPEED = 8f * 0.2f; // 10 km/h
		public const float ICY_ROADS_MIN_SPEED = 8f * 0.4f; // 20 km/h
		public const float ICY_ROADS_STUDDED_MIN_SPEED = 8f * 0.8f; // 40 km/h
		public const float WET_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		public const float WET_ROADS_FACTOR = 0.75f;
		public const float BROKEN_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		public const float BROKEN_ROADS_FACTOR = 0.75f;

		static VehicleStateManager() {
			Instance = new VehicleStateManager();
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Vehicle states:");
			for (int i = 0; i < VehicleStates.Length; ++i) {
				if (!VehicleStates[i].spawned) {
					continue;
				}
				Log._Debug($"Vehicle {i}: {VehicleStates[i]}");
			}
		}

		/// <summary>
		/// Known vehicles and their current known positions. Index: vehicle id
		/// </summary>
		internal VehicleState[] VehicleStates = null;

		private VehicleStateManager() {
			VehicleStates = new VehicleState[VehicleManager.MAX_VEHICLE_COUNT];
			for (uint i = 0; i < VehicleManager.MAX_VEHICLE_COUNT; ++i) {
				VehicleStates[i] = new VehicleState((ushort)i);
			}
		}

		/// <summary>
		/// Determines if the given vehicle is driven by a reckless driver
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		/// <returns></returns>
		public bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0)
				return true;
			if (Options.evacBussesMayIgnoreRules && vehicleData.Info.GetService() == ItemClass.Service.Disaster)
				return true;
			if (Options.recklessDrivers == 3)
				return false;

			return ((vehicleData.Info.m_vehicleType & RECKLESS_VEHICLE_TYPES) != VehicleInfo.VehicleType.None) && (uint)vehicleId % (Options.getRecklessDriverModulo()) == 0;
		}

		internal void LogTraffic(ushort vehicleId) {
			LogTraffic(vehicleId, ref VehicleStates[vehicleId]);
		}

		protected void LogTraffic(ushort vehicleId, ref VehicleState state) {
			if (state.currentSegmentId == 0) {
				return;
			}
			ushort length = (ushort)state.totalLength;
			if (length == 0) {
				return;
			}

			TrafficMeasurementManager.Instance.AddTraffic(state.currentSegmentId, state.currentLaneIndex, length, (ushort)state.sqrVelocity);
		}

		internal void OnCreateVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != (Vehicle.Flags.Created) ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnCreateVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleStateManager.OnCreateVehicle({vehicleId}): calling OnCreate for vehicle {vehicleId}");
#endif
			VehicleStates[vehicleId].OnCreate(ref vehicleData);
		}

		internal void OnStartPathFind(ushort vehicleId, ref Vehicle vehicleData, ExtVehicleType? vehicleType) {
			if (vehicleType == null) {
				return;
			}

			if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnStartPathFind({vehicleId}, {vehicleType}): unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

			ExtVehicleType type = (ExtVehicleType)vehicleType;

			ushort connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnStartPathFind({vehicleId}, {vehicleType}): overriding vehicle type for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (leading)");
#endif
				VehicleStates[connectedVehicleId].vehicleType = type;

				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_leadingVehicle;
			}

			connectedVehicleId = vehicleId;
			while (true) {
				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;

				if (connectedVehicleId == 0) {
					break;
				}

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnStartPathFind({vehicleId}, {vehicleType}): overriding vehicle type for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
#endif
				VehicleStates[connectedVehicleId].vehicleType = type;
			}
		}

		internal void OnSpawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) != (Vehicle.Flags.Created | Vehicle.Flags.Spawned) ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None || vehicleData.m_path <= 0) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnSpawnVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}, path: {vehicleData.m_path}");
#endif
				return;
			}
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleStateManager.OnSpawnVehicle({vehicleId}): calling OnSpawn for vehicle {vehicleId}");
#endif
			VehicleStates[vehicleId].OnSpawn(ref vehicleData);
		}

		internal void UpdateVehiclePosition(ushort vehicleId, ref Vehicle vehicleData, float? sqrVelocity=null) {
			if (!Options.prioritySignsEnabled && !Options.timedLightsEnabled) {
				return;
			}

			if (vehicleData.m_path == 0 || (vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
				return;
			}

			UpdateVehiclePosition(vehicleId, ref vehicleData, ref VehicleStates[vehicleId], sqrVelocity);
		}

		protected void UpdateVehiclePosition(ushort vehicleId, ref Vehicle vehicleData, ref VehicleState state, float? sqrVelocity) {
			state.sqrVelocity = sqrVelocity != null ? (float)sqrVelocity : vehicleData.GetLastFrameVelocity().sqrMagnitude;

			if (state.lastPathId == vehicleData.m_path && state.lastPathPositionIndex == vehicleData.m_pathPositionIndex) {
				return;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleStateManager.UpdateVehiclePosition({vehicleId}) called");
#endif

			// update vehicle position for timed traffic lights and priority signs
			int pathPosIndex = vehicleData.m_pathPositionIndex >> 1;
			PathUnit.Position curPathPos = Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition(pathPosIndex);
			PathUnit.Position nextPathPos = default(PathUnit.Position);
			Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].GetNextPosition(pathPosIndex, out nextPathPos);
			VehicleStates[vehicleId].UpdatePosition(ref vehicleData, ref curPathPos, ref nextPathPos);
		}

		internal void OnDespawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

			ushort connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): calling OnDespawn for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (leading)");
#endif
				VehicleStates[connectedVehicleId].OnDespawn();

				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_leadingVehicle;
			}

			connectedVehicleId = vehicleId;
			while (true) {
				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;

				if (connectedVehicleId == 0) {
					break;
				}

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): calling OnDespawn for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
#endif
				VehicleStates[connectedVehicleId].OnDespawn();
			}
		}

		internal void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleStateManager.OnReleaseVehicle({vehicleId}) called.");
#endif
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != (Vehicle.Flags.Created) ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnReleaseVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleStateManager.OnReleaseVehicle({vehicleId}): calling OnRelease for vehicle {vehicleId}");
#endif
			OnDespawnVehicle(vehicleId, ref vehicleData);
			VehicleStates[vehicleId].OnRelease(ref vehicleData);
		}

		internal void InitAllVehicles() {
			Log._Debug("VehicleStateManager: InitAllVehicles()");
			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

				for (ushort vehicleId = 0; vehicleId < VehicleManager.MAX_VEHICLE_COUNT; ++vehicleId) {
					Services.VehicleService.ProcessVehicle(vehicleId, delegate (ushort vId, ref Vehicle vehicle) {
						if ((vehicle.m_flags & Vehicle.Flags.Created) == 0) {
							return true;
						}

						OnCreateVehicle(vehicleId, ref vehicle);

						if ((vehicle.m_flags & Vehicle.Flags.Emergency2) != 0) {
							OnStartPathFind(vehicleId, ref vehicle, ExtVehicleType.Emergency);
						}

						if ((vehicle.m_flags & Vehicle.Flags.Spawned) == 0) {
							return true;
						}

						OnSpawnVehicle(vehicleId, ref vehicle);
						
						return true;
					});
				}
			}
		}

		/*public ushort GetFrontVehicleId(ushort vehicleId, ref Vehicle vehicleData) {
			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			ushort frontVehicleId = vehicleId;
			if (reversed) {
				frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
			} else {
				frontVehicleId = vehicleData.GetFirstVehicle(vehicleId);
			}

			return frontVehicleId;
		}*/

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < VehicleStates.Length; ++i) {
				VehicleStates[i].OnDespawn();
			}
		}

		public override void OnAfterLoadData() {
			base.OnAfterLoadData();
			InitAllVehicles();
		}
	}
}
