using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MuteIndicator;

public class MicrophoneLevelMonitor : IDisposable
{
    private WasapiCapture _waveIn; // Audio capture device
    private bool _isMonitoring;

    public event Action<float> InputLevelChanged; // Event to notify when the input level changes
    public event Action<bool> SpeakingChanged; // Event to notify when the input level changes

    /// <summary>
    ///     Starts monitoring the default microphone input.
    /// </summary>
    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        // Initialize the audio capture
        _waveIn = new WasapiCapture(); // Use the default input device
        _waveIn.DataAvailable += OnDataAvailable2;

        _isMonitoring = true;

        // Start capturing audio
        _waveIn.StartRecording();
        Console.WriteLine("Microphone monitoring started...");
    }

    /// <summary>
    ///     Stops monitoring the microphone input.
    /// </summary>
    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        // Stop capturing audio and release resources
        _waveIn.StopRecording();
        _waveIn.DataAvailable -= OnDataAvailable2;
        _waveIn.Dispose();
        _waveIn = null;

        _isMonitoring = false;
        Console.WriteLine("Microphone monitoring stopped.");
    }

    /// <summary>
    ///     Handles the audio data as it becomes available.
    /// </summary>
    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        float max = 0;

        // Process the audio buffer assuming it's in IeeeFloat format (32-bit float)
        for (int i = 0; i < e.BytesRecorded; i += 4) // 4 bytes per 32-bit float
        {
            // Convert 4 bytes to a single float (IeeeFloat format)
            float sample32 = BitConverter.ToSingle(e.Buffer, i);

            // Since IeeeFloat samples are already in the range [-1.0, 1.0],
            // just track the absolute maximum value
            max = Math.Max(max, Math.Abs(sample32));
        }

        // Notify subscribers of the updated input level
        InputLevelChanged?.Invoke(max);
    }

    private void OnDataAvailable2(object sender, WaveInEventArgs e)
    {
        float max = 0; // Peak value
        float sum = 0; // Sum of squared samples for RMS
        int sampleCount = 0;

        // Process the audio buffer assuming it's in IeeeFloat format (32-bit float)
        for (int i = 0; i < e.BytesRecorded; i += 4) // 4 bytes per 32-bit float
        {
            float sample32 = BitConverter.ToSingle(e.Buffer, i);

            // Track the peak (absolute max)
            max = Math.Max(max, Math.Abs(sample32));

            // Accumulate the squared sample for RMS calculation
            sum += sample32 * sample32;
            sampleCount++;
        }

        // Compute RMS value
        float rms = (float)Math.Sqrt(sum / sampleCount);
        //float rms = max;

        // Define dB range for normalization
        const float minDb = -40f; // Minimum dB level (silent)
        const float maxDb = 0f;   // Maximum dB level (full amplitude)

        // Convert RMS to dB
        float levelDb = 20 * (float) Math.Log10(rms > 0 ? rms : 0.0001f); // Avoid log of zero

        // Clamp dB to the desired range
        levelDb = Math.Clamp(levelDb, minDb, maxDb);

        // Map dB to a value in the range [0,1]
        float scaledValue = (levelDb - minDb) / (maxDb - minDb);

        // Notify subscribers with the scaled value
        InputLevelChanged?.Invoke(scaledValue);
        SpeakingChanged?.Invoke(scaledValue > .15);
    }

    /// <summary>
    ///     Disposes of the resources used by the monitor.
    /// </summary>
    public void Dispose()
    {
        StopMonitoring();
    }
}