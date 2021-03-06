﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BusGTFSDataController : MonoBehaviour {

	//
	// General Loading
	//

	public void LoadAllGTFSData(System.Action dataLoadedCallback) {
		this.LoadCalendarData(delegate() {
			this.LoadRouteData(delegate() { 
				this.LoadStopsData(delegate() {
					this.LoadTripStopPointsData(delegate() {
						//            Debug.Log("Trip stop points loaded!");

						this.LoadTripInfoData(delegate() {
							//                Debug.Log("Trip infos loaded too!");

							this.LoadShapesData(delegate() {

								dataLoadedCallback();

							});    
						});
					});        
				});
			});
		});
	}

	//
	// Shapes
	//

	public TextAsset shapesTextData;

	public Dictionary<string, List<LatitudeLongitude>> shapesLatLongsByRouteStringId = new Dictionary<string, List<LatitudeLongitude>>();

	private const int kShapesDataRouteMaxLength = 650; // largest was 615 for route 102_IB

	public void LoadShapesData(System.Action dataLoadedCallback) {
		System.Action<CSVLineDataHandler> lineProcessor = delegate(CSVLineDataHandler lineComponents) {
			string routeStringId = lineComponents["shape_id"];

			if (!this.shapesLatLongsByRouteStringId.ContainsKey(routeStringId)) {
				this.shapesLatLongsByRouteStringId.Add(routeStringId, new List<LatitudeLongitude>(kShapesDataRouteMaxLength));
			}

			this.shapesLatLongsByRouteStringId[routeStringId].Add(new LatitudeLongitude(double.Parse(lineComponents["shape_pt_lat"]), double.Parse(lineComponents["shape_pt_lon"])));
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

	public LatitudeLongitude LatLongClosestToPointOnRoughShapePath(LatitudeLongitude searchPoint, string routeShapeId) {
		if (!this.shapesLatLongsByRouteStringId.ContainsKey(routeShapeId) || this.shapesLatLongsByRouteStringId[routeShapeId].Count == 0) {
			Debug.LogWarning("Couldn't find route path data for shape id: " + routeShapeId);

			return searchPoint;
		}

		List<LatitudeLongitude> latLongList = this.shapesLatLongsByRouteStringId[routeShapeId];

		LatitudeLongitude closestLatLong = latLongList[0];

		foreach (LatitudeLongitude latLong in latLongList) {
			if (LatitudeLongitude.Distance(searchPoint, latLong) < LatitudeLongitude.Distance(searchPoint, closestLatLong))
				closestLatLong = latLong;
		}

		return closestLatLong;
	}

	//
	// Stops
	//

	public TextAsset stopsTextData;

	public struct StopInfo {
		public LatitudeLongitude latlong;
		public int stopId;
		//        public int btId; // not sure what bt_id is for... 
		public string stopName;
	}

	public List<StopInfo> stopInfos; // indexed by stopId

	public void LoadStopsData(System.Action dataLoadedCallback) {
		this.stopInfos = new List<StopInfo>(7030); // highest stopId is 7027, but there are tons of gaps within that

		StopInfo nullStop = new StopInfo();
		nullStop.stopId = -1;

		System.Action<CSVLineDataHandler> lineProcessor = delegate(CSVLineDataHandler lineComponents) {
			int stopId = int.Parse(lineComponents["stop_id"]);

			StopInfo newStopInfo = new StopInfo();
			newStopInfo.latlong = new LatitudeLongitude(double.Parse(lineComponents["stop_lat"]), double.Parse(lineComponents["stop_lon"]));
			newStopInfo.stopId = stopId;
			newStopInfo.stopName = lineComponents["stop_name"];

			while (this.stopInfos.Count <= (stopId))
				this.stopInfos.Add(nullStop);

			if (this.stopInfos[stopId].stopId >= 0) {
				Debug.LogError("Stop id collision, two stops found for stop id: " + stopId);
			}

			this.stopInfos[stopId] = newStopInfo;

//			if (this.stopInfos.Count > stopId) {
//				Debug.LogWarning("Out of order stop detected at count: " + this.stopInfos.Count + " stopId: " + stopId);
//			}
//			else {
//				while (this.stopInfos.Count < (stopId))
//					this.stopInfos.Add(nullStop);
//
//				this.stopInfos.Add(newStopInfo);
//			}
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

	public TextAsset stopTimesTextData; // stop_times.txt

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
		/*trip_id,arrival_time,departure_time,stop_id,stop_sequence,pickup_type,drop_off_type*/
		System.Action<CSVLineDataHandler> lineProcessor = delegate(CSVLineDataHandler lineComponents) {
			StopPointInfo nullStopPoint = new StopPointInfo();
			nullStopPoint.stopId = -1;

			string tripId = lineComponents["trip_id"];

			if (!this.stopPointInfosByTripId.ContainsKey(tripId)) {

				//                if (tripId.Equals("3-1636-I-1a"))
				//                    Debug.Log("Adding tripId: <" + tripId + ">");

				this.stopPointInfosByTripId.Add(tripId, new List<StopPointInfo>(100));
			}

			StopPointInfo newStopPoint = new StopPointInfo();

			newStopPoint.arrivalTimeString = lineComponents["arrival_time"];
			newStopPoint.departureTimeString = lineComponents["departure_time"];
			newStopPoint.stopId = int.Parse(lineComponents["stop_id"]);
			newStopPoint.sequence = int.Parse(lineComponents["stop_sequence"]);

			newStopPoint.arrivalSecondsIntoTheDay = SecondsIntoDayForTimeString(newStopPoint.arrivalTimeString);

			int arrayIndexForNewStop = newStopPoint.sequence - 1;

			{
				if (arrayIndexForNewStop < 0 || arrayIndexForNewStop > 10000) {
					Debug.LogError("Sequence is <= 0 or very large for trip id: " + tripId + " for sequence: " + newStopPoint.sequence);

					arrayIndexForNewStop = 0;
				}

				while (this.stopPointInfosByTripId[tripId].Count <= arrayIndexForNewStop) 
					this.stopPointInfosByTripId[tripId].Add(nullStopPoint);

				if (this.stopPointInfosByTripId[tripId][arrayIndexForNewStop].stopId != -1)
					Debug.LogWarning("Previous stop not null for tripId: " + tripId + " stopId: " + newStopPoint.stopId);
			}

			this.stopPointInfosByTripId[tripId][arrayIndexForNewStop] = newStopPoint;

			// Original
//			this.stopPointInfosByTripId[tripId].Add(newStopPoint);
		};

		this.ProcessCSVData(this.stopTimesTextData.text, lineProcessor, 7, dataLoadedCallback);

		foreach (KeyValuePair<string, List<StopPointInfo>> pair in this.stopPointInfosByTripId) {
			List<StopPointInfo> stopPoints = pair.Value;

			for (int i = 0; i < stopPoints.Count; i++) {
				bool keepLooking = true;
				int sequentialSameArrivalTimeTops = 0;
				int safteyCount = 0;

				while (keepLooking && (i+sequentialSameArrivalTimeTops+1 < stopPoints.Count) && safteyCount <= 10) {
					if (Mathf.Approximately(stopPoints[i+sequentialSameArrivalTimeTops].arrivalSecondsIntoTheDay, stopPoints[i+sequentialSameArrivalTimeTops+1].arrivalSecondsIntoTheDay)) {
						sequentialSameArrivalTimeTops++;
					}
					else {
						keepLooking = false;
					}

					safteyCount++;
				}

				if (safteyCount >= 10) 
					Debug.LogWarning("SafteyCount hit on tripId: " + tripInfosByRouteId + " index: " + i);

				if (sequentialSameArrivalTimeTops > 0) {
					for (int k = 1; k <= sequentialSameArrivalTimeTops; k++) {
						StopPointInfo stopPoint = stopPoints[i+k];
						stopPoint.arrivalSecondsIntoTheDay = stopPoint.arrivalSecondsIntoTheDay + (k * (60f / (sequentialSameArrivalTimeTops + 1)));
						stopPoints[i+k] = stopPoint;
					}
				}
			}
		}

		#if UNITY_EDITOR
		foreach (KeyValuePair<string, List<StopPointInfo>> pair in this.stopPointInfosByTripId) {
			for (int i = 0; i < pair.Value.Count; i++) {
				StopPointInfo stopPoint = pair.Value[i];

				if (stopPoint.sequence != (i + 1)) {
					Debug.LogWarning("StopPoint out of sequence in tripId: " + pair.Key + " at index: " + (i+1) + " sequence: " + stopPoint.sequence);
				}

				if (i < (pair.Value.Count - 1)) {
					if ((pair.Value[i+1].arrivalSecondsIntoTheDay - pair.Value[i].arrivalSecondsIntoTheDay) < 1) { // e.g. 3060 - 3000 = 60
						Debug.LogWarning("Found very short arrival time difference on tripId: " + pair.Key + " sequence: " + stopPoint.sequence);
					}
				}
			}
		}
		#endif
	}

	public static float SecondsIntoDayForTimeString(string timeStringAsHHcMMcSS) {
		if (timeStringAsHHcMMcSS.Length == 8) {
			return 
				(int.Parse(timeStringAsHHcMMcSS.Substring(0, 2)) * 3600) +  // Hours
				(int.Parse(timeStringAsHHcMMcSS.Substring(3, 2)) * 60) + // Minutes
				(int.Parse(timeStringAsHHcMMcSS.Substring(6, 2))); // Seconds
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
             bikes_allowed,    route_id,        wheelchair_accessible,    direction_id,    trip_headsign,                service_id,        shape_id,                trip_id
            1,                13,                1,                        0,                Senior Center/Muldoon,        92,                13_OB,                    13-1420-O-92
            2,                ERC,            1,                        1,                Muldoon Transfer Center,    1a,                ERC_IB_to_MULDOON,        ERC-800-I-1a
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
		System.Action<CSVLineDataHandler> lineProcessor = delegate(CSVLineDataHandler lineComponents) {
			#if UNITY_EDITOR
			try 
			#endif
			{
				//                int routeId = int.Parse(lineComponents[1]);
				string routeId = lineComponents["route_id"];

				if (!this.tripInfosByRouteId.ContainsKey(routeId)) {
					this.tripInfosByRouteId.Add(routeId, new List<TripInfo>(150));
				}

				TripInfo newTripInfo = new TripInfo();
				newTripInfo.bikesAllowed = int.Parse(lineComponents["bikes_allowed"]);
				newTripInfo.routeId = routeId;
				newTripInfo.wheelchairAccessible = int.Parse(lineComponents["wheelchair_accessible"]);
				newTripInfo.directionId = int.Parse(lineComponents["direction_id"]);
				newTripInfo.tripHeadSign = lineComponents["trip_headsign"];
				newTripInfo.serviceId = lineComponents["service_id"];
				newTripInfo.shapeId = lineComponents["shape_id"];
				newTripInfo.tripId = lineComponents["trip_id"];

				this.tripInfosByRouteId[routeId].Add(newTripInfo);
			}
			#if UNITY_EDITOR
			catch (System.Exception e) {
				Debug.LogError("Encoutnered error: " + e.ToString() + " on tripId: " + lineComponents["trip_id"]);
			}
			#endif
		};

		this.ProcessCSVData(this.tripsTextData.text, lineProcessor, 8, dataLoadedCallback);
	}

	//
	// Routes
	//

	public TextAsset routeTextData;

	public struct RouteInfo {
		/*
        route_id,    agency_id,                    route_short_name,    route_long_name,    route_desc,                route_type,        route_url,                                                                    route_color,    route_text_color
        1,            Anchorage People Mover,        1,                    CROSSTOWN,            "Route 1 trav[...]",    3,                http://www.muni.org/departments/transit/peoplemover/Pages/route1.aspx,        DB4040,            FFFFFF
        */

		public string routeId;
		public string agencyId;
		public string routeShortName;
		public string routeLongName;
		public string routeDescription;
		public int routeType;
		public string routeURL;
		public Color routeColor;
		public Color routeTextColor;
	}

	public Dictionary<string, RouteInfo> routeInfosByRouteId = new Dictionary<string, RouteInfo>();

	public void LoadRouteData(System.Action dataLoadedCallback) {
		System.Action<CSVLineDataHandler> lineProcessor = delegate(CSVLineDataHandler lineComponents) {
			RouteInfo newRouteInfo = new RouteInfo();

			newRouteInfo.routeId = lineComponents["route_id"];
			newRouteInfo.agencyId = lineComponents["agency_id"];
			newRouteInfo.routeShortName = lineComponents["route_short_name"];
			newRouteInfo.routeLongName = lineComponents["route_long_name"];
			newRouteInfo.routeDescription = lineComponents["route_desc"];
			newRouteInfo.routeType = int.Parse(lineComponents["route_type"]);
			newRouteInfo.routeURL = lineComponents["route_url"];
			newRouteInfo.routeColor = ColorForHexString(lineComponents["route_color"]);
			newRouteInfo.routeTextColor = ColorForHexString(lineComponents["route_text_color"]);

			this.routeInfosByRouteId.Add(newRouteInfo.routeId, newRouteInfo);
		};

		this.ProcessCSVData(this.routeTextData.text, lineProcessor, 9, dataLoadedCallback);

		#if UNITY_EDITOR
		#endif
	}

	public static Color ColorForHexString(string hexString) {
		if (hexString.Length < 6)
			return Color.white;

		Color c;

		if (!ColorUtility.TryParseHtmlString("#" + hexString, out c)) {
			Debug.LogWarning("Couldn't parse hexString: " + hexString);
		}

		return c;
	}

	//
	// Calendar
	//

	public TextAsset calendarTextData;

	public struct CalendarInfo {
		/*
service_id,monday,tuesday,wednesday,thursday,friday,saturday,sunday,start_date,end_date
3a,0,0,0,0,0,0,1,20160801,20180604
1a,1,1,1,1,1,0,0,20160801,20180604
2a,0,0,0,0,0,1,0,20160801,20180604
92,0,0,0,0,0,0,0,20160801,20180604
        */
		public string serviceId;
		public int[] days;
		public int startDate;
		public int endDate;
	}

	public Dictionary<string, CalendarInfo> calendarInfoByServiceId = new Dictionary<string, CalendarInfo>();

	public void LoadCalendarData(System.Action dataLoadedCallback) {
		System.Action<CSVLineDataHandler> lineProcessor = delegate(CSVLineDataHandler lineComponents) {
			CalendarInfo newCalendarInfo = new CalendarInfo();

			string serviceId = lineComponents["service_id"];

			int[] daysArray = new int[7];

			//            if (!lineComponents.ContainsKey("monday")) {
			//                foreach (KeyValuePair<string, string> pair in lineComponents) {
			//                    Debug.LogError(pair.Key);
			//                }
			//            }

			daysArray[0] = int.Parse(lineComponents["monday"]);
			daysArray[1] = int.Parse(lineComponents["tuesday"]);
			daysArray[2] = int.Parse(lineComponents["wednesday"]);
			daysArray[3] = int.Parse(lineComponents["thursday"]);
			daysArray[4] = int.Parse(lineComponents["friday"]);
			daysArray[5] = int.Parse(lineComponents["saturday"]);
			daysArray[6] = int.Parse(lineComponents["sunday"]);

			//            for (int i = 0; i < 7; i++) {
			//                daysArray[i] = int.Parse(lineComponents[i+1]);
			//            }

			newCalendarInfo.serviceId = serviceId;
			newCalendarInfo.days = daysArray;
			newCalendarInfo.startDate = int.Parse(lineComponents["start_date"]);
			newCalendarInfo.endDate = int.Parse(lineComponents["end_date"]);

			this.calendarInfoByServiceId.Add(serviceId, newCalendarInfo);
		};

		this.ProcessCSVData(this.calendarTextData.text, lineProcessor, 10, dataLoadedCallback);
	}        

	public bool TripIdIsActiveForDayOfWeek(string tripId, System.DayOfWeek dayOfWeek) {

		// System.DayOfWeek.Sunday = 0, System.DayOfWeek.Monday = 1, etc
		int calendarInfoDayIndex = (dayOfWeek == System.DayOfWeek.Sunday) ? 6 : ((int)(dayOfWeek)-1);

		foreach (KeyValuePair<string, CalendarInfo> pair in this.calendarInfoByServiceId) {
			string serviceId = pair.Key;

			if (tripId.Length >= serviceId.Length && tripId.Substring(tripId.Length - serviceId.Length, serviceId.Length).Equals(serviceId)) {
				if (pair.Value.days[calendarInfoDayIndex] > 0) {
					//                    if (Time.frameCount % 30 == 0)
					//                        Debug.Log("Hit on service id: " + serviceId + " tripId: " + tripId + " dayOfWeek: " + (int)dayOfWeek);

					return true;
				}
			}
		}

		return false;
	}

	//
	// General Processing
	//

	class CSVLineDataHandler : System.Object {
		private Dictionary <string, string> dictionary;

		public CSVLineDataHandler() {
			this.dictionary = new Dictionary<string, string>();
		}

		public void Clear() {
			this.dictionary.Clear();
		}

		public void Add(string key, string value) {
			this.dictionary.Add(key, value);
		}

		public string this[string key] {
			get {
				if (this.dictionary.ContainsKey(key))
					return this.dictionary[key];
				else
					return "";
			}
		}
	}

	private CSVLineDataHandler cachedCSVLineHandler;

	private void ProcessCSVData(string csvText, System.Action<CSVLineDataHandler> processLineCallback, int expectedNumberOfColumns, System.Action dataFinishedProcessingCallback) {
		string[] csvTextLines = csvText.Split(new string[]{"\n"}, System.StringSplitOptions.None);    

		this.cachedCSVLineHandler = new CSVLineDataHandler();

		List<string> csvColumnHeader = null; //new List<string>();

		for (int i = 0; i < csvTextLines.Length; i++) {
			string csvLine = csvTextLines[i];

			if (csvLine.Contains("\"")) {
				int firstDoubleQuote = csvLine.IndexOf("\"");
				int lastDoubleQuote = csvLine.LastIndexOf("\"");

				csvLine = csvLine.Replace(csvLine.Substring(firstDoubleQuote, (lastDoubleQuote - firstDoubleQuote) + 1), "[Unparsed Double Quote Section]");
			}

			string[] lineComponents = csvLine.Split(new string[]{","}, System.StringSplitOptions.None);

			// Remove the line break, last entry has lingering line break at end for some reason... 
			lineComponents[lineComponents.Length-1] = lineComponents[lineComponents.Length-1].Trim();

			if (i == 0) {
				csvColumnHeader = new List<string>(lineComponents);

//				#if UNITY_EDITOR
//				string totalColumnHeader = "";
//				foreach (string s in csvColumnHeader)
//					totalColumnHeader += s + " --- ";
//				Debug.Log("Column Header is: " + totalColumnHeader);
//				#endif
			}
			else {

				if (lineComponents.Length == 1 && lineComponents[0].Trim().Length == 0) {
					// Do nothing, most likely empty last line
				}

				//                if (lineComponents.Length == expectedNumberOfColumns) 
				else {
					this.cachedCSVLineHandler.Clear();

					for (int j = 0; j < lineComponents.Length; j++) {
						this.cachedCSVLineHandler.Add(csvColumnHeader[j].Trim(), lineComponents[j].Trim());
					}

					processLineCallback(this.cachedCSVLineHandler);
				}
				//                else {
				//                    if (i != csvTextLines.Length - 1) // Last line might be empty
				//                        Debug.LogError("Line: " + i + " doesn't have " + expectedNumberOfColumns + " components: " + csvTextLines[i] + " instead has: " + lineComponents.Length);
				//                }
			}
		}

		dataFinishedProcessingCallback();
	}
}

//
// Data Structures
//

public struct BusGTFSStopData {
	/*     stop_lat,    stop_lon,        stop_id,    bt_id,    stop_name
        61.216517,    -149.886091,    0002,        2788,    6TH AVENUE & C STREET ESE    */

	public LatitudeLongitude latLong;
	public int stopId;
	public int btId;
	public string stopName;
}