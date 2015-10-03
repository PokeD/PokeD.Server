﻿using System;

using Aragas.Core.Interfaces;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using PokeD.Core.Data;

namespace PokeD.Server.Data
{
    public class World : IUpdatable, IDisposable
    {
        [JsonProperty("UseLocation")]
        public bool UseLocation { get; set; }
        bool LocationChanged { get; set; }

        [JsonProperty("Location", NullValueHandling = NullValueHandling.Ignore)]
        public string Location { get { return _location; } set { LocationChanged = _location != value; _location = value; } }
        string _location;

        [JsonProperty("UseRealTime")]
        public bool UseRealTime { get; set; } = true;

        [JsonProperty("DoDayCycle")]
        public bool DoDayCycle { get; set; } = true;

        [JsonProperty("Season"), JsonConverter(typeof(StringEnumConverter))]
        public Season Season { get; set; } = Season.Spring;

        [JsonProperty("Weather"), JsonConverter(typeof(StringEnumConverter))]
        public Weather Weather { get; set; } = Weather.Sunny;

        [JsonProperty("CurrentTime")]
        public TimeSpan CurrentTime
        {
            get { TimeSpan timeSpan; return TimeSpan.TryParseExact(CurrentTimeString, "HH\\,mm\\,ss", null, out timeSpan) ? timeSpan : TimeSpan.Zero; }
            set { CurrentTimeString = value.Hours + "," + value.Minutes + "," + value.Seconds; }
        }
        string CurrentTimeString { get; set; }

        TimeSpan TimeSpanOffset => TimeSpan.FromSeconds(TimeOffset);
        int TimeOffset { get; set; }


        /// <summary>
        /// Call it one per second.
        /// </summary>
        public void Update()
        {
            TimeOffset++;
        }


        public DataItems GenerateDataItems()
        {
            if (DoDayCycle)
            {
                var now = DateTime.Now;
                if (TimeOffset != 0)
                    if (UseRealTime)
                        CurrentTimeString = now.AddSeconds(TimeOffset).Hour + "," + now.AddSeconds(TimeOffset).Minute + "," + now.AddSeconds(TimeOffset).Second;
                    else
                        CurrentTime += TimeSpanOffset;
                else
                    if (UseRealTime)
                        CurrentTimeString = DateTime.Now.Hour + "," + DateTime.Now.Minute + "," + DateTime.Now.Second;
            }
            else
                CurrentTimeString = "12,0,0";
            
            return new DataItems(((int) Season).ToString(), ((int) Weather).ToString(), CurrentTimeString);
        }


        public void Dispose() { }
    }
}