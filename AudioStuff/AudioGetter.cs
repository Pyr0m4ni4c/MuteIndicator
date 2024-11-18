using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace AudioStuff
{
    public static class AudioGetter
    {
        private const StringComparison CurIgn = StringComparison.CurrentCultureIgnoreCase;

        /*
        MMDeviceEnumerator devEnum = new MMDeviceEnumerator();

        // Get default audio output (playback) device
        MMDevice defaultOutputDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        Console.WriteLine("Default audio output device: " + defaultOutputDevice.FriendlyName);

        // Get default audio input (recording) device
        MMDevice defaultInputDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        Console.WriteLine("Default audio input device: " + defaultInputDevice.FriendlyName);

        // Get default communication audio output (playback) device
        MMDevice defaultCommunicationOutputDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
        Console.WriteLine("Default communication audio output device: " + defaultCommunicationOutputDevice.FriendlyName);

        // Get default communication audio input (recording) device
        MMDevice defaultCommunicationInputDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        Console.WriteLine("Default communication audio input device: " + defaultCommunicationInputDevice.FriendlyName);
    */

        public static bool IsInputDevice(string friendlyName)
        {
            using MMDeviceEnumerator devEnum = new MMDeviceEnumerator();
            var collection = devEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            return collection.Any(c => c.FriendlyName.Equals(friendlyName, StringComparison.CurrentCultureIgnoreCase));
        }

        public static bool IsOutputDevice(string friendlyName)
        {
            using MMDeviceEnumerator devEnum = new MMDeviceEnumerator();
            var collection = devEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            return collection.Any(c => c.FriendlyName.Equals(friendlyName, StringComparison.CurrentCultureIgnoreCase));
        }

        public static bool IsDefaultDevice(string friendlyName)
        {
            return DefaultOutputDeviceName.Equals(friendlyName, CurIgn)
                   || DefaultInputDeviceName.Equals(friendlyName, CurIgn);
        }

        public static IEnumerable<string> GetOutputDeviceNames
        {
            get
            {
                using MMDeviceEnumerator devEnum = new MMDeviceEnumerator();

                var collection = devEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                return collection.Select(c => c.FriendlyName).ToArray();
            }
        }

        public static IEnumerable<string> GetInputDeviceNames
        {
            get
            {
                using MMDeviceEnumerator devEnum = new MMDeviceEnumerator();

                var collection = devEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                return collection.Select(c => c.FriendlyName).ToArray();
            }
        }

        public static string GetIdByName(string friendlyName)
        {
            using MMDeviceEnumerator devEnum = new MMDeviceEnumerator();

            var collection = devEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            var firstOrDefault = collection.FirstOrDefault(c => c.FriendlyName.Equals(friendlyName, StringComparison.CurrentCultureIgnoreCase));
            if (firstOrDefault == null)
            {
                collection = devEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                firstOrDefault ??= collection.FirstOrDefault(c => c.FriendlyName.Equals(friendlyName, StringComparison.CurrentCultureIgnoreCase));
            }
            return firstOrDefault?.ID ?? "";
        }

        public static string DefaultOutputDeviceName
        {
            get
            {
                using var defaultOutputDevice = DefaultOutputDevice;
                return defaultOutputDevice.FriendlyName;
            }
        }

        public static string DefaultOutputDeviceId => DefaultOutputDevice.ID;

        public static string DefaultInputDeviceName
        {
            get
            {
                using var defaultInputDevice = DefaultInputDevice;
                return defaultInputDevice.FriendlyName;
            }
        }

        public static string DefaultInputDeviceId => DefaultInputDevice.ID;

        private static MMDevice DefaultOutputDevice
        {
            get
            {
                using MMDeviceEnumerator devEnum = new MMDeviceEnumerator();

                // Get default audio output (playback) device
                MMDevice defaultOutputDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return defaultOutputDevice;
            }
        }

        private static MMDevice DefaultInputDevice
        {
            get
            {
                using MMDeviceEnumerator devEnum = new MMDeviceEnumerator();

                // Get default audio input (recording) device
                MMDevice defaultInputDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                return defaultInputDevice;
            }
        }
    }
}