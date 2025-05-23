﻿using System;

namespace MuteIndicator
{
    static class SimpleMessageHandler
    {
        private const StringComparison CurIgn = StringComparison.CurrentCultureIgnoreCase;
        internal static Form1.MuteReceivedDelegate? MuteReceived;
        internal static Form1.OnAudioCycleReceivedDelegate? CycleReceived;

        private static bool IsMuteMessage(string message)
        {
            return message.EndsWith("muted", CurIgn) || message.Equals(true.ToString(), CurIgn) || message.Equals(false.ToString(), CurIgn);
        }

        private static bool IsCycleMessage(string message)
        {
            return true;
        }

        internal static void ParseAndFire(string rawMessage)
        {
            var message = rawMessage.Replace("<EOF>", "");

            if (IsMuteMessage(message) && MuteReceived != null)
            {
                MuteReceived(message);
                //Task.Run(() => MuteReceived?.Invoke(message)); // Asynchronously invoke MuteReceived
            }
            else if (IsCycleMessage(message) && CycleReceived != null)
            {
                CycleReceived();
                //Task.Run(() => CycleReceived?.Invoke()); // Asynchronously invoke CycleReceived
            }
        }
    }
}