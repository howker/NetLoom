using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NetLoom.Application.Monitoring;

namespace NetLoom.Engine
{
    internal static class EngineMonitoringTargetSetFile
    {
        public static IReadOnlyList<MonitoringScheduleTarget> Read(
            string path,
            Func<Guid, IPAddress, MonitoringPollRequest> requestFactory,
            TimeSpan cadence,
            TimeSpan maxStartupJitter)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "TARGET_SET_FILE_REQUIRED",
                    nameof(path));
            }

            if (requestFactory == null)
            {
                throw new ArgumentNullException(
                    nameof(requestFactory));
            }

            if (cadence <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cadence));
            }

            if (maxStartupJitter < TimeSpan.Zero ||
                maxStartupJitter.TotalSeconds >
                    int.MaxValue ||
                maxStartupJitter.TotalSeconds !=
                    Math.Floor(
                        maxStartupJitter.TotalSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxStartupJitter));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "TARGET_SET_FILE_NOT_FOUND",
                    path);
            }

            var result =
                new List<MonitoringScheduleTarget>();

            var deviceIds =
                new HashSet<Guid>();

            var lines =
                File.ReadAllLines(path);

            for (var index = 0;
                 index < lines.Length;
                 index++)
            {
                var line =
                    (lines[index] ??
                     string.Empty)
                        .Trim();

                if (line.Length == 0)
                {
                    continue;
                }

                var fields =
                    line.Split(
                        new[] { '\t' },
                        StringSplitOptions.None);

                if (fields.Length != 2)
                {
                    throw InvalidLine(
                        index,
                        "TARGET_SET_INVALID_FIELD_COUNT");
                }

                Guid deviceId;

                if (!Guid.TryParse(
                        fields[0].Trim(),
                        out deviceId) ||
                    deviceId == Guid.Empty)
                {
                    throw InvalidLine(
                        index,
                        "TARGET_SET_INVALID_DEVICE_ID");
                }

                if (!deviceIds.Add(
                        deviceId))
                {
                    throw InvalidLine(
                        index,
                        "TARGET_SET_DUPLICATE_DEVICE_ID");
                }

                IPAddress address;

                if (!IPAddress.TryParse(
                    fields[1].Trim(),
                    out address))
                {
                    throw InvalidLine(
                        index,
                        "TARGET_SET_INVALID_ADDRESS");
                }

                var request =
                    requestFactory(
                        deviceId,
                        address);

                if (request == null ||
                    !request.DeviceId.HasValue ||
                    request.DeviceId.Value !=
                        deviceId ||
                    !Equals(
                        request.Address,
                        address))
                {
                    throw new InvalidOperationException(
                        "TARGET_SET_REQUEST_FACTORY_MISMATCH");
                }

                result.Add(
                    new MonitoringScheduleTarget(
                        request,
                        cadence,
                        CalculateInitialDelay(
                            deviceId,
                            maxStartupJitter)));
            }

            if (result.Count == 0)
            {
                throw new InvalidDataException(
                    "TARGET_SET_EMPTY");
            }

            return result;
        }

        internal static TimeSpan CalculateInitialDelay(
            Guid deviceId,
            TimeSpan maxStartupJitter)
        {
            var maxSeconds =
                (long)maxStartupJitter.TotalSeconds;

            if (maxSeconds <= 0)
            {
                return TimeSpan.Zero;
            }

            uint hash = 2166136261;

            unchecked
            {
                foreach (var value in
                    deviceId.ToByteArray())
                {
                    hash ^= value;
                    hash *= 16777619;
                }
            }

            var delaySeconds =
                (long)((ulong)hash %
                    (ulong)(maxSeconds + 1));

            return TimeSpan.FromSeconds(
                delaySeconds);
        }

        private static InvalidDataException InvalidLine(
            int zeroBasedLine,
            string code)
        {
            return new InvalidDataException(
                code +
                "_LINE_" +
                (zeroBasedLine + 1));
        }
    }
}
