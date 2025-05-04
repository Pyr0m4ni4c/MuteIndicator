using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

public class KeyboardLayoutInfo
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetLocaleInfoEx(string lpLocaleName, uint LCType, StringBuilder lpLCData, int cchData);

    private const uint LOCALE_SISO639LANGNAME = 0x0059;
    private const uint LOCALE_SISO3166CTRYNAME = 0x005A;

    public static string GetCurrentKeyboardLanguage()
    {
        try
        {
            // Get the keyboard layout for the foreground window
            IntPtr foregroundWindow = GetForegroundWindow();
            uint threadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            IntPtr keyboardLayout = GetKeyboardLayout(threadId);

            // The language identifier is in the low word of keyboardLayout
            int languageId = keyboardLayout.ToInt32() & 0xFFFF;

            // Create a culture info for this language ID
            CultureInfo culture = new CultureInfo(languageId);
            
            // Return the three-letter ISO language name
            return culture.ThreeLetterISOLanguageName.ToUpper();
        }
        catch
        {
            return "???";
        }
    }
}