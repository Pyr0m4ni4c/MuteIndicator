using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace MuteIndicator
{
    public class GlobalKeyboardHook : IDisposable
    {
        // Keyboard hook type
        private const int WH_KEYBOARD_LL = 13;
        // Constants for WH_KEYBOARD hook
        private const int WH_KEYBOARD = 2;

        // Windows message constants
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        // Hook procedure delegate
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static HookProc _hookProc;

        // Handle for the global hook
        private static IntPtr _hookId = IntPtr.Zero;

        // Event to signal a key press
        public event Action<Keys> KeyDown;
        public event Action<Keys> KeyUp;

        // Constructor
        public GlobalKeyboardHook()
        {
            _hookProc = HookCallback;
            _hookId = SetHook(_hookProc);
        }

        // Destructor to clean up
        ~GlobalKeyboardHook()
        {
            UnhookWindowsHookEx(_hookId);
        }

        // Set up the global hook
        private static IntPtr SetHook(HookProc proc)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                //return SetWindowsHookEx(WH_KEYBOARD, proc, GetModuleHandle(curModule.ModuleName), /*(uint) AppDomain.GetCurrentThreadId()*/(uint) Environment.CurrentManagedThreadId);
            }
        }

        // Hook callback function
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN)
                    KeyDown?.Invoke(key);
                else if (wParam == (IntPtr)WM_KEYUP)
                    KeyUp?.Invoke(key);
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // Windows API functions
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Implement IDisposable for proper cleanup
        public void Dispose()
        {
            UnhookWindowsHookEx(_hookId);
        }
    }
}