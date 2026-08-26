namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System;

    using Colossal.Atmosphere;

    using Game.Simulation;

    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Answers "what will the sky actually look like at hour X, on this map" by running the game's own
    /// sun model rather than approximating it.
    /// </summary>
    /// <remarks>
    /// None of this needs reflection, which is not obvious from a first read of
    /// <see cref="PlanetarySystem"/>: a debugger field dump shows <c>m_Latitude</c>, <c>m_Longitude</c>
    /// and <c>m_SunLimit</c> and it is easy to conclude they are unreachable, but each has a public
    /// property. <c>m_SunMoonData</c> really is private, but <c>OnUpdate</c> assigns it
    /// <c>default(SunMoonData)</c> and the struct carries no state beyond <c>static readonly</c>
    /// constants, so constructing our own is equivalent. The only piece that has to be reimplemented is
    /// <c>CreateDateTime</c> — five lines of DateTime arithmetic, private static, trivially copied.
    /// <para>
    /// The map position is read live rather than special-cased. In <c>GameMode.Game</c> with the
    /// day/night visual setting off, <c>PlanetarySystem.OnUpdate</c> pins latitude, longitude, day and
    /// year to a fixed European summer afternoon — but it does so through the public setters, so by the
    /// time anything asks us, the properties already hold whatever the game is really rendering with.
    /// That branch is also gated on <c>!overrideTime</c>, and an animated ramp turns the override on, so
    /// the real position is what ends up being used anyway.
    /// </para>
    /// </remarks>
    public readonly struct SunModel {
        private const double kHalfDaySeconds = 43200.0;

        private readonly SunMoonData m_Data;
        private readonly double      m_Latitude;
        private readonly double      m_Longitude;
        private readonly double2     m_SunLimit;
        private readonly int         m_Year;
        private readonly int         m_Day;

        public bool IsValid { get; }

        private SunModel(PlanetarySystem planetary) {
            m_Data      = new SunMoonData();
            m_Latitude  = planetary.latitude;
            m_Longitude = planetary.longitude;
            m_SunLimit  = planetary.sunLimit;
            m_Year      = planetary.year;
            m_Day       = planetary.day;
            IsValid     = true;
        }

        public static SunModel From(PlanetarySystem planetary) {
            return planetary == null ? default : new SunModel(planetary);
        }

        public float Intensity(float hour) {
            if (!IsValid) {
                return 0f;
            }

            float3 forward = Forward(hour);

            return math.smoothstep(0f, 0.3f, math.abs(math.min(0f, forward.y)));
        }

        public float3 Forward(float hour) {
            if (!IsValid) {
                return new float3(0f, -1f, 0f);
            }

            float3 position = m_Data.GetLimitedSunPosition(ToDate(hour), m_Latitude, m_Longitude, m_SunLimit)
                                    .ToLocalCoordinates(out _);

            return math.rotate(float4x4.LookAt(position, float3.zero, new float3(0f, 1f, 0f)),
                               new float3(0f, 0f, 1f));
        }

        public bool TryGetSunTimes(out SunMoonData.SunTimes times) {
            if (!IsValid) {
                times = default;
                return false;
            }

            times = m_Data.GetSunTimes(ToDate(12f), m_Latitude, m_Longitude);
            return true;
        }

        public float ToHour(JulianDateTime date) {
            DateTime local = ((DateTime)date).AddSeconds(kHalfDaySeconds * m_Longitude / 180.0);

            return (float)local.TimeOfDay.TotalHours;
        }

        private JulianDateTime ToDate(float hour) {
            hour = Mathf.Repeat(hour, 24f);

            int   wholeHours   = (int)hour;
            float leftoverMins = (hour - wholeHours) * 60f;
            int   wholeMinutes = (int)leftoverMins;

            return CreateDateTime(m_Year, m_Day, wholeHours, wholeMinutes,
                                  (leftoverMins - wholeMinutes) * 60f, m_Longitude);
        }

        private static DateTime CreateDateTime(int year, int day, int hour, int minute, float second,
                                               double longitude) {
            year = Mathf.Clamp(year, 1, 9999);

            return new DateTime(0L, DateTimeKind.Utc).AddYears(year - 1)
                                                     .AddDays(day - 1)
                                                     .AddHours(hour)
                                                     .AddMinutes(minute)
                                                     .AddSeconds(second)
                                                     .AddSeconds(-kHalfDaySeconds * longitude / 180.0);
        }
    }
}
