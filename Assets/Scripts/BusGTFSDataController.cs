﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BusGTFSDataController : MonoBehaviour {
	
	//
	// Shapes
	//

	public TextAsset shapesTextData;

	public Dictionary<string, List<LatitudeLongitude>> shapesLatLongsByRouteStringId = new Dictionary<string, List<LatitudeLongitude>>();

	private const int kShapesDataRouteMaxLength = 650; // largest was 615 for route 102_IB

	public void LoadShapesData(System.Action dataLoadedCallback) {
		System.Action<string[]> lineProcessor = delegate(string[] lineComponents) {
			string routeStringId = lineComponents[0];

			if (!this.shapesLatLongsByRouteStringId.ContainsKey(routeStringId)) {
				this.shapesLatLongsByRouteStringId.Add(routeStringId, new List<LatitudeLongitude>(kShapesDataRouteMaxLength));
			}

			this.shapesLatLongsByRouteStringId[routeStringId].Add(new LatitudeLongitude(double.Parse(lineComponents[1]), double.Parse(lineComponents[2])));
		};

		this.ProcessCSVData(this.shapesTextData.text, lineProcessor, 4, dataLoadedCallback);

		#if UNITY_EDITOR
		foreach (KeyValuePair<string, List<LatitudeLongitude>> pair in this.shapesLatLongsByRouteStringId) {
			if (pair.Value.Count > kShapesDataRouteMaxLength) {
				Debug.LogWarning("Shape length for route " + pair.Key + " exceeds length, is count: " + pair.Value.Count);
			}
		}
		#endif
	}

	//
	// Stops
	//

	public TextAsset stopsTextData;

	public struct StopInfo {
		public LatitudeLongitude latlong;
		public int stopId;
//		public int btId; // not sure what bt_id is for... 
		public string stopName;
	}

	public List<StopInfo> stopInfos; // indexed by stopId

	public void LoadStopsData(System.Action dataLoadedCallback) {
		this.stopInfos = new List<StopInfo>(7050); // highest stopId is 7012, but there are tons of gaps within that

		StopInfo nullStop = new StopInfo();

		System.Action<string[]> lineProcessor = delegate(string[] lineComponents) {
			int stopId = int.Parse(lineComponents[2]);

			StopInfo newStopInfo = new StopInfo();
			newStopInfo.latlong = new LatitudeLongitude(double.Parse(lineComponents[0]), double.Parse(lineComponents[1]));
			newStopInfo.stopId = stopId;
			newStopInfo.stopName = lineComponents[4];

			if (this.stopInfos.Count > stopId) {
				Debug.LogWarning("Out of order stop detected at count: " + this.stopInfos.Count + " stopId: " + stopId);
			}
			else {
				while (this.stopInfos.Count < (stopId))
					this.stopInfos.Add(nullStop);

				this.stopInfos.Add(newStopInfo);
			}
		};

		this.ProcessCSVData(this.stopsTextData.text, lineProcessor, 5, dataLoadedCallback);

		#if UNITY_EDITOR
		for (int i = 0; i < this.stopInfos.Count; i++) {
			StopInfo stopInfo = this.stopInfos[i];

			if (stopInfo.stopName != null && stopInfo.stopId != i) {
				Debug.LogWarning("Stop Id doesn't match index at index: " + i + " stopId: " + stopInfo.stopId);
			}
		}
		#endif
	}

	//
	// Stop Times
	//

	public TextAsset stopTimesTextData;

	public struct StopPointInfo {
		public string arrivalTimeString;
		public string departureTimeString;

		public int stopId;
		public int sequence;

		// Processed
		public float arrivalSecondsIntoTheDay; // seconds past midnight
	}

	public Dictionary<string, List<StopPointInfo>> stopPointInfosByTripId = new Dictionary<string, List<StopPointInfo>>();

	public void LoadTripStopPointsData(System.Action dataLoadedCallback) {
		System.Action<string[]> lineProcessor = delegate(string[] lineComponents) {
			string tripId = lineComponents[0];

			if (!this.stopPointInfosByTripId.ContainsKey(tripId)) {

//				if (tripId.Equals("3-1636-I-1a"))
//					Debug.Log("Adding tripId: <" + tripId + ">");

				this.stopPointInfosByTripId.Add(tripId, new List<StopPointInfo>(100));
			}

			StopPointInfo newStopPoint = new StopPointInfo();

			newStopPoint.arrivalTimeString = lineComponents[1];
			newStopPoint.departureTimeString = lineComponents[2];
			newStopPoint.stopId = int.Parse(lineComponents[3]);
			newStopPoint.sequence = int.Parse(lineComponents[4]);

			newStopPoint.arrivalSecondsIntoTheDay = SecondsIntoDayForTimeString(newStopPoint.arrivalTimeString);

			this.stopPointInfosByTripId[tripId].Add(newStopPoint);
		};

		this.ProcessCSVData(this.stopTimesTextData.text, lineProcessor, 7, dataLoadedCallback);

		#if UNITY_EDITOR
		// Verify sequence id order?
		#endif
	}

	public static float SecondsIntoDayForTimeString(string timeStringAsHHcMMcSS) {
		if (timeStringAsHHcMMcSS.Length == 8) {
			return 
				(int.Parse(timeStringAsHHcMMcSS.Substring(0, 2)) * 60) +  // Hours
				(int.Parse(timeStringAsHHcMMcSS.Substring(3, 2))) + // Minutes
				(int.Parse(timeStringAsHHcMMcSS.Substring(6, 2)) / 100f); // Seconds
		}
		else {
			Debug.LogWarning("Couldn't parse time: " + timeStringAsHHcMMcSS + ", must be in format HH:MM:ss");
			return 0;
		}
	}

	//
	// Trips
	//

	public TextAsset tripsTextData;

	public struct TripInfo {
		/*
		 	bikes_allowed,	route_id,		wheelchair_accessible,	direction_id,	trip_headsign,				service_id,		shape_id,				trip_id
			1,				13,				1,						0,				Senior Center/Muldoon,		92,				13_OB,					13-1420-O-92
			2,				ERC,			1,						1,				Muldoon Transfer Center,	1a,				ERC_IB_to_MULDOON,		ERC-800-I-1a
		*/

		public int bikesAllowed;
		public string routeId;
		public int wheelchairAccessible;
		public int directionId;
		public string tripHeadSign;
		public string serviceId;
		public string shapeId;
		public string tripId;
	}

	public Dictionary<string, List<TripInfo>> tripInfosByRouteId = new Dictionary<string, List<TripInfo>>();

	public void LoadTripInfoData(System.Action dataLoadedCallback) {
		System.Action<string[]> lineProcessor = delegate(string[] lineComponents) {
			#if UNITY_EDITOR
			try 
			#endif
			{
//				int routeId = int.Parse(lineComponents[1]);
				string routeId = lineComponents[1];

				if (!this.tripInfosByRouteId.ContainsKey(routeId)) {
					this.tripInfosByRouteId.Add(routeId, new List<TripInfo>(150));
				}

				TripInfo newTripInfo = new TripInfo();
				newTripInfo.bikesAllowed = int.Parse(lineComponents[0]);
				newTripInfo.routeId = routeId;
				newTripInfo.wheelchairAccessible = int.Parse(lineComponents[2]);
				newTripInfo.directionId = int.Parse(lineComponents[3]);
				newTripInfo.tripHeadSign = lineComponents[4];
				newTripInfo.serviceId = lineComponents[5];
				newTripInfo.shapeId = lineComponents[6];
				newTripInfo.tripId = lineComponents[7];

				this.tripInfosByRouteId[routeId].Add(newTripInfo);
			}
			#if UNITY_EDITOR
			catch (System.Exception e) {
				Debug.LogError("Encoutnered error: " + e.ToString() + " on tripId: " + lineComponents[7]);
			}
			#endif
		};

		this.ProcessCSVData(this.tripsTextData.text, lineProcessor, 8, dataLoadedCallback);
	}

	//
	// General Processing
	//

	private void ProcessCSVData(string csvText, System.Action<string[]> processLineCallback, int expectedNumberOfColumns, System.Action dataFinishedProcessingCallback) {
		string[] csvTextLines = csvText.Split(new string[]{"\n"}, System.StringSplitOptions.None);

		for (int i = 1; i < csvTextLines.Length; i++) {
			string[] lineComponents = csvTextLines[i].Split(new string[]{","}, System.StringSplitOptions.None);

			lineComponents[lineComponents.Length-1] = lineComponents[lineComponents.Length-1].Trim(); // Last entry has lingering line break at end for some reason... 

			if (lineComponents.Length == expectedNumberOfColumns) {
				processLineCallback(lineComponents);
			}
			else {
				if (i != csvTextLines.Length - 1) // Last line might be empty
					Debug.LogError("Line: " + i + " doesn't have " + expectedNumberOfColumns + " components: " + csvTextLines[i] + " instead has: " + lineComponents.Length);
			}
		}

		dataFinishedProcessingCallback();
	}
}

//
// Data Structures
//

public struct BusGTFSStopData {
	/* 	stop_lat,	stop_lon,		stop_id,	bt_id,	stop_name
		61.216517,	-149.886091,	0002,		2788,	6TH AVENUE & C STREET ESE    */

	public LatitudeLongitude latLong;
	public int stopId;
	public int btId;
	public string stopName;
}