namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using Colossal.Atmosphere;

    #endregion

    /// <summary>
    /// A named span of the day, resolved against the loaded map's real sun times.
    /// </summary>
    /// <remarks>
    /// <c>None</c> is first and deliberately meaningless: photo mode's <c>EnumField</c> drops the
    /// lowest-valued option on its first push, so every enum bound to a dropdown here needs a
    /// sacrificial entry at zero. See <c>Camera.GateFitMode</c>, which is shaped the same way and is why
    /// Colossal never saw the defect.
    /// </remarks>
    public enum TimeOfDayPreset {
        None = 0,

        Custom = 1,

        Sunrise = 2,

        MorningGolden = 3,

        Daylight = 4,

        EveningGolden = 5,

        Sunset = 6,

        BlueHour = 7,

        FullDay = 8,
    }

    /// <summary>Resolves a <see cref="TimeOfDayPreset"/> into a pair of hours.</summary>
    public static class TimeOfDayPresets {
        public static bool TryResolve(TimeOfDayPreset preset, SunModel sun, out float start, out float end) {
            start = 0f;
            end   = 0f;

            if (preset == TimeOfDayPreset.None || preset == TimeOfDayPreset.Custom) {
                return false;
            }

            if (!sun.TryGetSunTimes(out SunMoonData.SunTimes t)) {
                return false;
            }

            switch (preset) {
                case TimeOfDayPreset.Sunrise:
                    start = sun.ToHour(t.nauticalDawn);
                    end   = sun.ToHour(t.goldenHourEnd);
                    break;

                case TimeOfDayPreset.MorningGolden:
                    start = sun.ToHour(t.sunrise);
                    end   = sun.ToHour(t.goldenHourEnd);
                    break;

                case TimeOfDayPreset.Daylight:
                    start = sun.ToHour(t.goldenHourEnd);
                    end   = sun.ToHour(t.goldenHour);
                    break;

                case TimeOfDayPreset.EveningGolden:
                    start = sun.ToHour(t.goldenHour);
                    end   = sun.ToHour(t.sunset);
                    break;

                case TimeOfDayPreset.Sunset:
                    start = sun.ToHour(t.goldenHour);
                    end   = sun.ToHour(t.nauticalDusk);
                    break;

                case TimeOfDayPreset.BlueHour:
                    start = sun.ToHour(t.sunset);
                    end   = sun.ToHour(t.night);
                    break;

                case TimeOfDayPreset.FullDay:
                    start = sun.ToHour(t.nightEnd);
                    end   = sun.ToHour(t.night);
                    break;

                default:
                    return false;
            }

            return true;
        }
    }
}
