﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSUtil.Commons;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using UnityEngine;

namespace TrafficManager.Manager {
	public interface IExtSegmentEndManager {
		/// <summary>
		/// Extended segment end data
		/// </summary>
		ExtSegmentEnd[] ExtSegmentEnds { get; }

		/// <summary>
		/// Resets both segment ends of the given segment.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		void Reset(ushort segmentId);

		/// <summary>
		/// Determines the index of the segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">start node</param>
		/// <returns>index</returns>
		int GetIndex(ushort segmentId, bool startNode);

		/// <summary>
		/// Determines the index of the segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="nodeId">node id</param>
		/// <returns>index</returns>
		int GetIndex(ushort segmentId, ushort nodeId);

		/// <summary>
		/// Counts the number of registered vehicles for the given segment end.
		/// </summary>
		/// <param name="end">segment end</param>
		/// <returns>number of registered vehicles</returns>
		uint GetRegisteredVehicleCount(ref ExtSegmentEnd end);

		/// <summary>
		/// Performs recalcution of the segment ends for the given segment id
		/// </summary>
		/// <param name="segmentId">segment id</param>
		void Recalculate(ushort segmentId);

		/// <summary>
		/// Performs recalcution of the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		void Recalculate(ushort segmentId, bool startNode);

		/// <summary>
		/// Determines the relative compass direction from the given source end to the target segment.
		/// </summary>
		/// <param name="sourceEnd">source segment end</param>
		/// <param name="targetSegmentId">target segment id</param>
		/// <returns>compass direction</returns>
		ArrowDirection GetDirection(ref ExtSegmentEnd sourceEnd, ushort targetSegmentId);

		/// <summary>
		/// Determines whether the segment end is connected to highways only.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <returns><code>true</code> if the segment end is connected to highways only, <code>false</code> otherwise</returns>
		bool CalculateOnlyHighways(ushort segmentId, bool startNode);

		/// <summary>
		/// Calculates whether the given segment end is connected to outgoing left, straight and/or right segments.
		/// </summary>
		/// <param name="segEnd">segment end</param>
		/// <param name="node">node data</param>
		/// <param name="left">(output) has outgoing left segments</param>
		/// <param name="straight">(output) has outgoing straight segments</param>
		/// <param name="right">(output) has outgoing right segments</param>
		void CalculateOutgoingLeftStraightRightSegments(ref ExtSegmentEnd segEnd, ref NetNode node, out bool left, out bool straight, out bool right);
	}
}
