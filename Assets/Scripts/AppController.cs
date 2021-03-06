﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AppController : MonoBehaviour {
	public MapIndicatorsController mapIndicatorController;
	public BusRouteDataController busRouteDataController;

	public OverlayUIController overlayUIController;

	public LineRenderer lineRenderer;

	public float timeOverride;
	public float timeOffset = 0;
	public float currentTime;

	private bool tripDataHasBeenLoaded = false;

	private List<string> visiblePathsByShapeId = new List<string>();
	private List<string> visibleRoutesByRouteId = new List<string>();

	// Use this for initialization
	void Start () {
		// XML related
//		this.busRouteDataController.BeginDownloadingDataForType(BusDataType.Stops, this.LoadCompletedForDataType);
//		this.busRouteDataController.BeginDownloadingDataForType(BusDataType.RouteStops, this.LoadCompletedForDataType);

		// GTFS related
		this.busRouteDataController.gtfsDataController.LoadAllGTFSData(delegate() {
			//			this.StartCoroutine(this.co_LoadCompletedForGTFSDataShapes("3C_OB_CNT", false));
			//			this.StartCoroutine(this.co_LoadCompletedForGTFSDataShapes("3C_IB", false));

			if (this.visiblePathsByShapeId.Count > 0) {
				foreach (string routeStringId in this.visiblePathsByShapeId) {
					this.StartCoroutine(this.co_LoadCompletedForGTFSDataShapes(routeStringId, false));
				}
			}
			else {
				foreach (string routeStringId in this.busRouteDataController.gtfsDataController.shapesLatLongsByRouteStringId.Keys) {
					this.StartCoroutine(this.co_LoadCompletedForGTFSDataShapes(routeStringId, false));
				}
			}

			this.tripDataHasBeenLoaded = true;
		});
	}

	void Update () {
		if (this.tripDataHasBeenLoaded) {
			this.UpdateBusPositions();
		}
	}

	private float playSpeedTimeOffset = 0;

	public void ResetAllTiming(float newOffsetInSeconds = 0) {
		this.playSpeedTimeOffset = newOffsetInSeconds;
		this.timeOffset = 0;
	}

	private void UpdateBusPositions() {
//		float fractionalSecond = 0;// (float) (System.DateTime.Now - new System.DateTime(System.DateTime.Now.Year, System.DateTime.Now.Month, System.DateTime.Now.Day, System.DateTime.Now.Hour, System.DateTime.Now.Minute, System.DateTime.Now.Second)).TotalSeconds;
//
//		float currentSecondsIntoDay = BusGTFSDataController.SecondsIntoDayForTimeString(System.DateTime.Now.ToString("HH:mm:ss")) + fractionalSecond + this.timeOffset;

		System.DateTime startOfDayDateTime = new System.DateTime(System.DateTime.Now.Year, System.DateTime.Now.Month, System.DateTime.Now.Day);
		float currentSecondsIntoDay = (float) (System.DateTime.Now - startOfDayDateTime).TotalSeconds + this.timeOffset;

//		Debug.Log("Current time: " + currentSecondsIntoDay);

		if (this.timeOverride > 0) {
			currentSecondsIntoDay = this.timeOverride + Time.time + this.timeOffset;
		}

		this.playSpeedTimeOffset += ((this.overlayUIController.timelineBarUIController.playSpeedScalar - 1) * Time.deltaTime);

		currentSecondsIntoDay += (this.overlayUIController.timelineBarUIController.timelineScrollRect.normalizedPosition.x * 2500000) + this.playSpeedTimeOffset;

		float totalSecondsFromStartOfToday = currentSecondsIntoDay;

		while (currentSecondsIntoDay > 86400) {
			currentSecondsIntoDay -= 86400;
		}
		while (currentSecondsIntoDay < 0) {
			currentSecondsIntoDay += 86400;
		}

		this.currentTime = currentSecondsIntoDay;

		System.DateTime currentDateTime = startOfDayDateTime.AddSeconds(totalSecondsFromStartOfToday);

		this.overlayUIController.timelineBarUIController.SetCurrentTime(currentDateTime);

		foreach (KeyValuePair<string, List<BusGTFSDataController.TripInfo>> routeTripInfosPair in this.busRouteDataController.gtfsDataController.tripInfosByRouteId) {

			string routeStringId = routeTripInfosPair.Key;

			if (this.visibleRoutesByRouteId.Count == 0 || this.visibleRoutesByRouteId.Contains(routeStringId)) {

				foreach (BusGTFSDataController.TripInfo tripInfo in routeTripInfosPair.Value) {

					string tripId = tripInfo.tripId;

//					if (tripId.Equals("3-1209-O-92")) // at time 44697 (debug point for sequential same arrival times
//					if (tripId.Contains("-1a")) // is Mon - Fri
					if (this.busRouteDataController.gtfsDataController.TripIdIsActiveForDayOfWeek(tripId, currentDateTime.DayOfWeek))
					{
						if (this.busRouteDataController.gtfsDataController.stopPointInfosByTripId.ContainsKey(tripId)) {								
							List<BusGTFSDataController.StopPointInfo> routeStopInfos = this.busRouteDataController.gtfsDataController.stopPointInfosByTripId[tripId];

							for (int i = 0; i < routeStopInfos.Count - 1; i++) {
								BusGTFSDataController.StopPointInfo stopInfoA = routeStopInfos[i];
								BusGTFSDataController.StopPointInfo stopInfoB = routeStopInfos[i+1];

								if (currentSecondsIntoDay >= stopInfoA.arrivalSecondsIntoTheDay && currentSecondsIntoDay < stopInfoB.arrivalSecondsIntoTheDay) {

									float percentageBetweenStops = Mathf.InverseLerp(stopInfoA.arrivalSecondsIntoTheDay, stopInfoB.arrivalSecondsIntoTheDay, currentSecondsIntoDay);

//									percentageBetweenStops = EaseInOutSine(0, 1, percentageBetweenStops);

//									Debug.Log("tripId: " + tripId + " percentage: " + percentageBetweenStops);

//									Debug.Log("Hit active stop for tripId: " + tripId + " index: " + i + " sequence: [" + stopInfoA.sequence + ", " + stopInfoB.sequence + "] percentage: " + percentageBetweenStops);

									LatitudeLongitude stopALongLat = this.busRouteDataController.gtfsDataController.stopInfos[stopInfoA.stopId].latlong;
									LatitudeLongitude stopBLongLat = this.busRouteDataController.gtfsDataController.stopInfos[stopInfoB.stopId].latlong;

									LatitudeLongitude percentageLatLong = LatitudeLongitude.Lerp(stopALongLat, stopBLongLat, (double) percentageBetweenStops);

//									#warning !
//									{
//										if (tripId.Contains("65")) // Experimenting with fix for buses going off path, example at 6:21:14pm on Sunday for Route 65
//											percentageLatLong = this.busRouteDataController.gtfsDataController.LatLongClosestToPointOnRoughShapePath(percentageLatLong, "65_IB_to_DIMOND_2017");
//									}

									//										this.mapIndicatorController.AddIndicatorAtLatLong(stopALongLat, 4);
									//										this.mapIndicatorController.AddIndicatorAtLatLong(stopBLongLat, 4);

	//								this.mapIndicatorController.AddIndicatorAtLatLong(percentageLatLong, 4);

									BusIconController newBusIcon = this.overlayUIController.busMapUIController.BusIndicatorIconForId(tripId, this.busRouteDataController.gtfsDataController.routeInfosByRouteId[routeStringId]);

									newBusIcon.lastFrameUpdated = Time.frameCount;

									newBusIcon.SetLatLongPosition(percentageLatLong);
								}
							}
						}
						else {
							//								if (tripId.Trim().Equals("3-1636-I-1a"))							
							Debug.LogError("Found unknown tripId: <" + tripId + ">" + "nl: " + tripId.Contains("\n"));
						}
					}
				}

			}
		}

		this.overlayUIController.busMapUIController.DisableAllIndicatorsNotEqualToFrame(Time.frameCount);
	}

	private static float EaseInOutSine(float start, float end, float value){
		end -= start;
		return -end / 2 * (Mathf.Cos(Mathf.PI * value / 1) - 1) + start;
	}

	private IEnumerator co_LoadCompletedForGTFSDataShapes(string routeShapeId, bool overTime) {
//		string routeStringId = "3C_OB_CNT";// "102_IB";// "13_IB";//"3C_OB_CNT";//	this.busRouteDataController.gtfsDataController.shapesLatLongsByRouteStringId.Keys[0];

		Color colorForRoute = Color.clear;

		foreach (KeyValuePair<string, List<BusGTFSDataController.TripInfo>> tripInfosByRouteIdPair in this.busRouteDataController.gtfsDataController.tripInfosByRouteId) {
			foreach (BusGTFSDataController.TripInfo tripInfo in tripInfosByRouteIdPair.Value) {
				if (tripInfo.shapeId == routeShapeId) {
					colorForRoute = this.busRouteDataController.gtfsDataController.routeInfosByRouteId[tripInfo.routeId].routeColor;
				}
			}
		}

		if (colorForRoute == Color.clear) {
			Debug.LogWarning("Couldn't find color for routeShapeId: " + routeShapeId);

			colorForRoute = Color.black;
		}

		int latLongCount = this.busRouteDataController.gtfsDataController.shapesLatLongsByRouteStringId[routeShapeId].Count;

		List<Vector3> linePathPoints = new List<Vector3>(latLongCount);

		LineRenderer newLineRenderer = Instantiate<LineRenderer>(this.lineRenderer);
		newLineRenderer.transform.parent = this.lineRenderer.transform.parent;
		newLineRenderer.transform.localPosition = this.lineRenderer.transform.localPosition;

		newLineRenderer.material.color = colorForRoute;

		foreach (LatitudeLongitude latLong in this.busRouteDataController.gtfsDataController.shapesLatLongsByRouteStringId[routeShapeId]) {
			Vector3 addedPos = this.mapIndicatorController.AddIndicatorAtLatLong(latLong, 0);

			linePathPoints.Add(addedPos);

			newLineRenderer.numPositions = linePathPoints.Count;
			newLineRenderer.SetPositions(linePathPoints.ToArray());

			if (overTime)
				yield return null;
		}

		newLineRenderer.numPositions = linePathPoints.Count;
		newLineRenderer.SetPositions(linePathPoints.ToArray());

		newLineRenderer.gameObject.SetActive(true);

		yield return null;
	}

	//
	// XML related
	//

	private void LoadCompletedForDataType(BusDataType dataType) {
		this.StartCoroutine(this.co_LoadCompletedForDataType(dataType));
	}

	private IEnumerator co_LoadCompletedForDataType(BusDataType dataType) {
		if (dataType == BusDataType.Stops) {
			foreach (BusDataStop busStop in this.busRouteDataController.busStops) {
				// Show all bus stops
//				this.mapIndicatorController.AddIndicatorAtLatLong(busStop.latitudeLongitude);
			}
		}
		else if (dataType == BusDataType.RouteStops) {
			Debug.Log("-------- this.busRouteDataController.busRouteStops.Count: " + this.busRouteDataController.busRouteStops.Count);

//			foreach (BusRouteStopItemData routeStop in this.busRouteDataController.busRouteStops) {
//				if (routeStop.routeNumber == 1) {
//					BusDataStop busStop = this.busRouteDataController.BusStopForStopId(routeStop.stopId);
//
//					this.mapIndicatorController.AddIndicatorAtLatLong(busStop.latitudeLongitude);
//
//					yield return null;
//				}
//			}

			int routeId = 3;

			for (int i = 0; i < BusRouteStopItemData.NumberOfRouteStopItemsForRoute(routeId); i++) {
				BusRouteStopItemData routeStopItem = BusRouteStopItemData.RouteStopItemForRouteAtIndex(routeId, i);

				BusDataStop busStop = this.busRouteDataController.BusStopForStopId(routeStopItem.stopId);

				this.mapIndicatorController.AddIndicatorAtLatLong(busStop.latitudeLongitude, 1);

				Debug.Log("Adding routeStopItem sort id: " + routeStopItem.sortOrder + " stopIds are equal: " + (routeStopItem.stopId == busStop.id).ToString());

//				yield return new WaitForSeconds(0.25f);
			}
		}

		yield return null;
	}
}
