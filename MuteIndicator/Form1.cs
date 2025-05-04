using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using AudioStuff;
using MuteIndicator.Properties;
using NAudio.CoreAudioApi;
using WebApplication1;

namespace MuteIndicator
{
    public partial class Form1 : Form
    {
        public delegate void MuteReceivedDelegate(string message);
        public delegate void OnAudioCycleReceivedDelegate();

        private enum CornerLocation
        {
            [Description("Top Left")] TopLeft,
            [Description("Top Right")] TopRight,
            [Description("Bottom Left")] BottomLeft,
            [Description("Bottom Right")] BottomRight
        }
    
        public enum Languages
        {
            [Description("en")] English, // quasi Platzhalter f�r unbekannte Sprachen...
            [Description("de")] Deutsch
        }

        #region Tray Icon separat erzeugen, weil der Designer das Tray Icon an die Form h�ngen w�rde
        private readonly ContextMenuStrip m_contextMenuStrip = new();
        private ToolStripMenuItem displaySettings = null;
        private ToolStripMenuItem cornerSettings = null;
        private ToolStripMenuItem sizeSettings = null;
        private ToolStripMenuItem keyboardHookSettings = null;
        private ToolStripMenuItem languageSettings = null;
        private ToolStripMenuItem hide = null;
        #endregion

        #region fields and props
        private CultureInfo _cultureInfo = null;
        private CultureInfo CultureInfo => _cultureInfo ??= new CultureInfo(GetDescription(SetLanguage));

        private ResourceManager _resourceManager = null;
        private ResourceManager ResourceManager => _resourceManager ??= new ResourceManager(typeof(Form1));

        private Languages _setLanguage;
        private Languages SetLanguage
        {
            get => _setLanguage;
            set
            {
                _setLanguage = value;
                _cultureInfo = null!;
            }
        }

        private bool _lastState;
        private readonly DateTime _lastMute;
        private const int HandlingTimeout = 200;
        private readonly Color _colorUnMuted = Color.Lime;
        private readonly Color _colorMuted = Color.Red;
        private GlobalKeyboardHook m_globalKeyboardHook;
        private MicrophoneLevelMonitor m_monitor;
        private readonly Color m_transparencyKey = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00);
        private Color MuteColor => Muted ? _colorMuted : _colorUnMuted;
        private bool Muted { get; set; }
        private MuteApi _muteApi;
        #endregion

        #region c'tor
        public Form1()
        {
            InitializeComponent();

            SetLanguage = (Languages) Settings1.Default.Language;

            #region System Tray Icon
            UpdateTrayMenu();
            #endregion

            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            Size = Settings1.Default.Size;

            // ReSharper disable once VirtualMemberCallInConstructor
            //BackColor = Color.LimeGreen;
            BackColor = m_transparencyKey;
            //TransparencyKey = Color.LimeGreen;
            TransparencyKey = m_transparencyKey;

            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Dock = DockStyle.Fill;

            UpdateCheckedMarks();
            UpdateFormLocation();

            #region Socket-Listener
            _lastMute = DateTime.Now;
            SimpleMessageHandler.MuteReceived += OnMuteReceivedAsync;
            SimpleMessageHandler.CycleReceived += OnAudioCycleReceivedAsync;

            // NUR mittels StopListening kann der Thread gestoppt werden
            var listenerThread = new Thread(AsynchronousSocketListener.StartListening);
            listenerThread.Start();
            #endregion

            #region Keyboard-Hook
            m_globalKeyboardHook = new GlobalKeyboardHook();
            if (Settings1.Default.KeyboardHookEnabled) m_globalKeyboardHook.KeyDown += OnGlobalKeyboardHookKeyDown;
            #endregion

            #region Api-Handler
            _muteApi = new MuteApi();
            MuteApi.OnSetMuteReceived += OnMuteReceivedApiAsync;
            MuteApi.OnToggleMuteReceived += OnMuteReceivedApiAsync;
            MuteApi.OnCycleDevicesReceived += OnAudioCycleReceivedAsync;
            _muteApi.Init("http://localhost:5288");
            #endregion

            m_monitor = new MicrophoneLevelMonitor();
            m_monitor.StartMonitoring();
            m_monitor.InputLevelChanged += MonitorOnInputLevelChanged;
            m_monitor.SpeakingChanged += MonitorOnSpeakingChanged;
        }

        private float CurrentLevel { get; set; }

        private void MonitorOnSpeakingChanged(bool speaking)
        {
            CurrentLevel = speaking ? 1 : 0;
        }

        private void MonitorOnInputLevelChanged(float obj)
        {
            //Debug.WriteLine($"OldLevel={CurrentLevel:P}, NewLevel={obj:P}, Muted={Muted}");
            return;
            CurrentLevel = obj;
        }

        ~Form1()
        {
            Dispose(false); // Perform non-managed resource cleanup in case Dispose was not called
        }

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose the keyboard hook if it's initialized
                if (m_globalKeyboardHook != null)
                {
                    m_globalKeyboardHook.Dispose();
                    m_globalKeyboardHook = null;
                }

                if (m_monitor != null)
                {
                    m_monitor.Dispose();
                    m_monitor = null;
                }

                MuteApi.OnSetMuteReceived -= OnMuteReceivedApiAsync;
                MuteApi.OnToggleMuteReceived -= OnMuteReceivedApiAsync;
                MuteApi.OnCycleDevicesReceived -= OnAudioCycleReceivedAsync;
                MuteApi.Stop();
            }

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion

        #region TrayMenu
        private void UpdateTrayMenu()
        {
            m_contextMenuStrip.Items.Clear();

            displaySettings = GenerateDisplaySettings();
            m_contextMenuStrip.Items.Add(displaySettings);

            cornerSettings = GenerateCornerSettings();
            m_contextMenuStrip.Items.Add(cornerSettings);

            sizeSettings = GenerateSizeSettings();
            m_contextMenuStrip.Items.Add(sizeSettings);

            languageSettings = GenerateLanguageSettings();
            m_contextMenuStrip.Items.Add(languageSettings);

            m_contextMenuStrip.Items.Add(ResourceManager.GetString("ConfigureAudioCombos", CultureInfo), Resources.configure, (sender, args) =>
            {
                var outputDeviceNames = AudioGetter.GetOutputDeviceNames;
                var inputDeviceNames = AudioGetter.GetInputDeviceNames;
                var audioCombos = AudioComboCollection.CreateFromString(Settings1.Default.AudioCombos);

                audioCombos.CollectionChanged += AudioCombosOnCollectionChanged;

                var form = new SelectAudioCombo(outputDeviceNames, inputDeviceNames, audioCombos);
                form.ShowDialog();
            });

            hide = GenerateHideSettings();
            m_contextMenuStrip.Items.Add(hide);

            keyboardHookSettings = GenerateKeyboardHookSettings();
            m_contextMenuStrip.Items.Add(keyboardHookSettings);

            m_contextMenuStrip.Items.Add(ResourceManager.GetString("Exit", CultureInfo), Resources.exit, (sender, args) =>
            {
                AsynchronousSocketListener.StopListening();
                Application.Exit();
            });

            notifyIcon1.ContextMenuStrip = m_contextMenuStrip;
        }

        private void AudioCombosOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var c = (AudioComboCollection) sender;
            Settings1.Default.AudioCombos = AudioComboCollection.ConvertToString(c);
            Settings1.Default.Save();
        }

        private ToolStripMenuItem GenerateCornerSettings()
        {
            ToolStripItem GetItem(CornerLocation location)
            {
                var item = new ToolStripMenuItem();

                item.Checked = location == CornerLocation.TopLeft;
                item.Tag = location;
                item.Text = GetEnumLocalized(location);

                item.Click += (_, _) =>
                {
                    Settings1.Default.CornerLocation = (int) location;
                    Settings1.Default.Save();
                    UpdateCheckedMarks();
                    UpdateFormLocation();
                };

                return item;
            }

            var toolStripMenuItem = new ToolStripMenuItem(ResourceManager.GetString("ConfigurationMenuCorner", CultureInfo));

            toolStripMenuItem.DropDownItems.Add(GetItem(CornerLocation.TopLeft));
            toolStripMenuItem.DropDownItems.Add(GetItem(CornerLocation.TopRight));
            toolStripMenuItem.DropDownItems.Add(GetItem(CornerLocation.BottomLeft));
            toolStripMenuItem.DropDownItems.Add(GetItem(CornerLocation.BottomRight));

            return toolStripMenuItem;
        }

        private ToolStripMenuItem GenerateHideSettings()
        {
            var toolStripMenuItem = new ToolStripMenuItem(ResourceManager.GetString("Hide", CultureInfo));
            toolStripMenuItem.Checked = Settings1.Default.Hide;

            toolStripMenuItem.Click += (sender, _) =>
            {
                Settings1.Default.Hide = !Settings1.Default.Hide;
                ((ToolStripMenuItem) sender!).Checked = Settings1.Default.Hide;
                Settings1.Default.Save();

                SetIndicator(pictureBox1);
            };

            return toolStripMenuItem;
        }

        private ToolStripMenuItem GenerateLanguageSettings()
        {
            ToolStripItem GetItem(Languages language)
            {
                var item = new ToolStripMenuItem();

                item.Checked = SetLanguage == language;
                item.Text = language.ToString();
                item.Tag = language;

                item.Click += (_, _) =>
                {
                    SetLanguage = language;
                    Settings1.Default.Language = (int) language;
                    Settings1.Default.Save();
                    UpdateCheckedMarks();
                    UpdateTrayMenu();
                };

                return item;
            }

            var toolStripMenuItem = new ToolStripMenuItem(ResourceManager.GetString("ConfigurationMenuLanguage", CultureInfo));

            foreach (var language in Enum.GetValues(typeof(Languages)).Cast<Languages>())
                toolStripMenuItem.DropDownItems.Add(GetItem(language));

            return toolStripMenuItem;
        }

        private ToolStripMenuItem GenerateSizeSettings()
        {
            ToolStripItem GetItem(Size s)
            {
                var item = new ToolStripMenuItem();

                item.Checked = Size == s;
                item.Text = s.ToString();
                item.Tag = s;

                item.Click += (_, _) =>
                {
                    Size = s;
                    Settings1.Default.Size = s;
                    Settings1.Default.Save();
                    SetIndicator(pictureBox1);
                    UpdateCheckedMarks();
                    UpdateFormLocation();
                };

                return item;
            }

            var toolStripMenuItem = new ToolStripMenuItem(ResourceManager.GetString("ConfigurationMenuSize", CultureInfo));

            toolStripMenuItem.DropDownItems.Add(GetItem(new Size(16, 16)));
            toolStripMenuItem.DropDownItems.Add(GetItem(new Size(24, 24)));
            toolStripMenuItem.DropDownItems.Add(GetItem(new Size(32, 32)));
            toolStripMenuItem.DropDownItems.Add(GetItem(new Size(48, 48)));
            toolStripMenuItem.DropDownItems.Add(GetItem(new Size(72, 72)));
            toolStripMenuItem.DropDownItems.Add(GetItem(new Size(96, 96)));
            toolStripMenuItem.DropDownItems.Add(GetItem(new Size(128, 128)));

            return toolStripMenuItem;
        }

        private ToolStripMenuItem GenerateKeyboardHookSettings()
        {
            var toolStripMenuItem = new ToolStripMenuItem(ResourceManager.GetString("ConfigurationKeyboardHook", CultureInfo));
            toolStripMenuItem.Click += (sender, _) =>
            {
                Settings1.Default.KeyboardHookEnabled = !Settings1.Default.KeyboardHookEnabled;
                Settings1.Default.Save();

                if (Settings1.Default.KeyboardHookEnabled) m_globalKeyboardHook.KeyDown += OnGlobalKeyboardHookKeyDown;
                else m_globalKeyboardHook.KeyDown -= OnGlobalKeyboardHookKeyDown;

                toolStripMenuItem.Checked = Settings1.Default.KeyboardHookEnabled;
            };

            return toolStripMenuItem;
        }

        private ToolStripMenuItem GenerateDisplaySettings()
        {
            var toolStripMenuItem = new ToolStripMenuItem(ResourceManager.GetString("ConfigurationMenuDisplay", CultureInfo));

            foreach (var screen in Screen.AllScreens)
            {
                // Zum umschalten der Resources-Culture:
                //Resources.Culture = CultureInfo.GetCultureInfo("de");
                // typspezifische ResourceManager werden davon nicht beeinflusst, also muss die CultureInfo explizit angegeben werden:
                //var x = new ResourceManager(typeof(Form1)).GetString("DisplayIdentText", new CultureInfo("de"));

                // Initialer Stand
                var stripMenuItem = new ToolStripMenuItem
                {
                    Text = string.Format(ResourceManager.GetString("DisplayIdentText", CultureInfo)!,
                        screen.DeviceName,
                        screen.Bounds.Width,
                        screen.Bounds.Height),
                    Tag = screen.DeviceName,
                    Checked = screen.Primary
                };

                stripMenuItem.Click += (sender, _) =>
                {
                    foreach (var o in toolStripMenuItem.DropDownItems)
                    {
                        if (o is not ToolStripMenuItem item)
                            continue;

                        item.Checked = false;
                    }

                    ((ToolStripMenuItem) sender!).Checked = true;
                    Settings1.Default.DisplayName = screen.DeviceName;
                    Settings1.Default.Save();
                    UpdateCheckedMarks();
                    UpdateFormLocation();
                };

                toolStripMenuItem.DropDownItems.Add(stripMenuItem);

                if (Screen.AllScreens.Length == 1)
                {
                    Settings1.Default.DisplayName = screen.DeviceName;
                    Settings1.Default.Save();
                }
            }

            return toolStripMenuItem;
        }
        #endregion

        #region private GUI Events
        private void OnGlobalKeyboardHookKeyDown(Keys key)
        {
            switch (key)
            {
                //case Keys.F6:
                case Keys.F4:
                    GetSetCurrentState();
                    OnMuteReceivedAsync(Muted.ToString());
                    break;
                case Keys.F7:
                    OnAudioCycleReceived();
                    SetIndicator(pictureBox1);
                    break;
            }

            return;
        }

        void GetSetCurrentState()
        {
            using var mmDeviceEnumerator = new MMDeviceEnumerator();

            var endpoint = mmDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).AudioEndpointVolume;
            Muted = !endpoint.Mute;
            //Debug.WriteLine(endpoint.MasterVolumeLevel.ToString());

            // Get the current volume level of the microphone
            //var currentVolume = endpoint.MasterVolumeLevelScalar * 100; // Scale volume (0.0 to 1.0) to percentage
            //Console.WriteLine($"Current microphone input volume: {currentVolume:F2}%");

            endpoint.Mute = Muted;
        }

        private void UpdateCheckedMarks()
        {
            foreach (var item in cornerSettings.DropDownItems.Cast<ToolStripMenuItem>())
                item.Checked = (int) item.Tag == Settings1.Default.CornerLocation;

            foreach (var item in displaySettings.DropDownItems.Cast<ToolStripMenuItem>())
                item.Checked = item.Tag.ToString() == Settings1.Default.DisplayName;

            foreach (var item in sizeSettings.DropDownItems.Cast<ToolStripMenuItem>())
                item.Checked = (Size) item.Tag == Settings1.Default.Size;

            foreach (var item in languageSettings.DropDownItems.Cast<ToolStripMenuItem>())
                item.Checked = (int) item.Tag == Settings1.Default.Language;

            keyboardHookSettings.Checked = Settings1.Default.KeyboardHookEnabled;
        }

        private void UpdateFormLocation()
        {
            // Get the selected display or fallback to primary
            var displayName = Settings1.Default.DisplayName;
            var screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == displayName, Screen.PrimaryScreen);
            var bounds = screen.Bounds;
    
            // Get the form size
            var formSize = Settings1.Default.Size;
    
            // Define offsets
            var horizontalOffset = 10; // Consistent left/right margin from screen edges
            var topOffset = 60;       // Extra space from top edge for title bar access
            var bottomOffset = 60;    // Extra space from bottom edge
    
            // Calculate position based on selected corner
            var p = (CornerLocation)Settings1.Default.CornerLocation switch
            {
                CornerLocation.TopLeft => new Point(
                    bounds.X + horizontalOffset, 
                    bounds.Y + topOffset),
            
                CornerLocation.TopRight => new Point(
                    bounds.X + bounds.Width - formSize.Width - horizontalOffset, 
                    bounds.Y + topOffset),
            
                CornerLocation.BottomLeft => new Point(
                    bounds.X + horizontalOffset, 
                    bounds.Y + bounds.Height - formSize.Height - bottomOffset),
            
                CornerLocation.BottomRight => new Point(
                    bounds.X + bounds.Width - formSize.Width - horizontalOffset, 
                    bounds.Y + bounds.Height - formSize.Height - bottomOffset),
            
                _ => Point.Empty
            };

            Location = p;
        }
        #endregion

        #region Mute/UI Handling
        /*
        Nicht l�nger ben�tigt, aber aufgrund der Finesse zur verfeinerten Kantengl�ttung als n�tzlicher Snippet drin gelassen
        /// <summary>
        ///     Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            // Um die Kantengl�ttung von Windows zu umgehen und so einfarbige kleinst-Bilder darstellen zu k�nnen
            // die einfachere L�sung ist es einfach einen Kreis zu zeichnen... das hier ist M�ll
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);
    
            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
    
            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    
                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
    
            return destImage;
        }*/

        private void SetIndicator(PictureBox pb)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetIndicator(pb));
                return;
            }

            var s = GetSetSizeByLevel(pb);
            //Debug.WriteLine($"CurrentLevel={CurrentLevel:P}, Level={level:P}, Size={s.Width}x{s.Height}");

            using var bmp = new Bitmap(s.Width, s.Height);
            using var g = Graphics.FromImage(bmp);

            // Don't enable high-quality-rendering! It destroys the edges of the circle by not clearing the previous drawn circle first. 
            //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            //g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.FillEllipse(new SolidBrush(MuteColor), new Rectangle(0, 0, s.Width, s.Height));

            WriteConfigIntoIndicator(g, s, GetDisplayText());

            Invoke(() => pb.Image = (Bitmap) bmp.Clone());
        }

        private Size GetSetSizeByLevel(PictureBox pb)
        {
            pb.Visible = !Settings1.Default.Hide;
            
            var s = Settings1.Default.Size;
            var level = 1 + CurrentLevel;
            
            s.Height = (int) (s.Height * level);
            s.Width = (int) (s.Width * level);
            Size = pb.Size = s;

            return s;
        }

        private static string GetDisplayText()
        {
            var displayText = string.Empty;
            if (InputLanguage.InstalledInputLanguages.Count > 1)
            {
                displayText = KeyboardLayoutInfo.GetCurrentKeyboardLanguage();
            }

            var speaker = Settings1.Default.CurSpeaker;
            var microphone = Settings1.Default.CurMicrophone;
            if (!string.IsNullOrEmpty(speaker) && !string.IsNullOrEmpty(microphone))
            {
                var speakerIndicator = GetIndicator(speaker);
                var microphoneIndicator = GetIndicator(microphone);

                if (!string.IsNullOrEmpty(displayText)) displayText += "\n";
                displayText += $"{speakerIndicator}:{microphoneIndicator}";
            }

            return displayText;
            
            string GetIndicator(string raw)
            {
                var r = new Regex(@"\((?<deviceName>[\d\- \w]+)\)");
                var group = r.Match(raw).Groups["deviceName"].Value;
                var words = group.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                return words.First(w => w.Length >= 3).First().ToString();
            }
        }

        private static void WriteConfigIntoIndicator(Graphics g, Size s, string displayText)
        {
            try
            {
                // Get the lines of text
                var lines = displayText.Split('\n');
        
                // Calculate font size based on circle size and number of lines
                // Use a smaller multiplier to ensure text fits well
                int fontSize = Math.Max(6, (int)(s.Width * 0.25 / Math.Sqrt(lines.Length)));
        
                using var f = new Font("Arial", fontSize, FontStyle.Bold);
        
                // Create a StringFormat object for center alignment
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,         // Horizontal center
                    LineAlignment = StringAlignment.Center,     // Vertical center
                    FormatFlags = StringFormatFlags.NoWrap      // Don't wrap text beyond what we provide
                };
        
                // Draw the text centered in the circle, with a slight inset
                // Define the drawing area as 80% of the circle to ensure margins
                var inset = (int)(s.Width * 0.1);  // 10% inset from each edge
                var textRect = new RectangleF(
                    inset,                  // X position with inset
                    inset,                  // Y position with inset
                    s.Width - (inset * 2),  // Width with inset on both sides
                    s.Height - (inset * 2)  // Height with inset on both sides
                );
        
                // Draw shadow text (offset by 1px)
                g.DrawString(displayText, f, new SolidBrush(Color.White), new RectangleF(textRect.X + 1, textRect.Y + 1, textRect.Width, textRect.Height), format);
                // Draw main text
                g.DrawString(displayText, f, new SolidBrush(Color.Black), textRect, format);
            }
            catch
            {
                // ignored
            }
        }

        private void OnAudioCycleReceivedAsync() => Task.Run(OnAudioCycleReceived);
        private void OnAudioCycleReceived()
        {
            if (InvokeRequired)
            {
                Invoke(new OnAudioCycleReceivedDelegate(OnAudioCycleReceived));
                return;
            }

            var current = AudioCombo.CreateFromString(Settings1.Default.CurrentCombo);
            var options = AudioComboCollection.CreateFromString(Settings1.Default.AudioCombos);
            if (options is not { Count: > 0 }) return;
            var curInd = current != null ? options.IndexOf(current) : 0;
            var nextItem = options[(curInd + 1) % options.Count];

            var idByName = AudioGetter.GetIdByName(nextItem.InputDeviceName);
            AudioSetter.SetDefault(idByName);
            idByName = AudioGetter.GetIdByName(nextItem.OutputDeviceName);
            AudioSetter.SetDefault(idByName);

            Settings1.Default.CurSpeaker = nextItem.OutputDeviceName; 
            Settings1.Default.CurMicrophone = nextItem.InputDeviceName; 
            
            Settings1.Default.CurrentCombo = AudioCombo.ConvertToString(nextItem);
            Settings1.Default.Save();
        }

        private bool OnMuteReceivedApiAsync()
        {
            using var mmDeviceEnumerator = new MMDeviceEnumerator();

            var endpoint = mmDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).AudioEndpointVolume;

            var newState = !endpoint.Mute;
            endpoint.Mute = Muted = newState;

            OnMuteReceivedAsync(newState ? "muted" : "unmuted");
            return newState;
        }

        private void OnMuteReceivedApiAsync(bool setState)
        {
            if (Muted == setState) return;

            using var mmDeviceEnumerator = new MMDeviceEnumerator();

            var endpoint = mmDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).AudioEndpointVolume;
            endpoint.Mute = Muted = setState;

            OnMuteReceivedAsync(setState ? "muted" : "unmuted");
        }

        private void OnMuteReceivedAsync(string message) => Task.Run(() => OnMuteReceived(message));

        private void OnMuteReceived(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new MuteReceivedDelegate(OnMuteReceived), message);
                return;
            }

            Muted = message.StartsWith("muted", StringComparison.CurrentCultureIgnoreCase)
                    || message.Contains("true", StringComparison.CurrentCultureIgnoreCase);

            if (Muted == _lastState) return;
            if (DateTime.Now - _lastMute < TimeSpan.FromMilliseconds(HandlingTimeout)) return;

            _lastState = Muted;
            SetIndicator(pictureBox1);

            using var soundPlayer = new SoundPlayer(Muted
                ? Resources.Mutesound
                : Resources.Unmutesound);
            soundPlayer.Play();
        }
        
        private void m_Timer2_Tick(object sender, EventArgs e)
        {
            CheckForSystemLanguageChange();
        }
        
        private string m_lastLanguageCode = string.Empty;

        private void CheckForSystemLanguageChange()
        {
            // Get the current system keyboard language
            string currentLanguageCode = KeyboardLayoutInfo.GetCurrentKeyboardLanguage();
    
            // Update if changed
            if (currentLanguageCode != m_lastLanguageCode)
            {
                m_lastLanguageCode = currentLanguageCode;
                SetIndicator(pictureBox1);
            }
        }
        #endregion

        #region private Helfer
        // Kein Erweiterungsmethode, weil eine static class erforderlich ist, welche nicht nested sein darf... eine weitere Klasse in dieser Datei fickt den Designer
        private static string GetDescription<T>(T GenericEnum)
        {
            var genericEnumType = GenericEnum.GetType();
            var memberInfo = genericEnumType.GetMember(GenericEnum.ToString());

            if (memberInfo is not {Length: > 0}) 
                return GenericEnum.ToString();

            var attribs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            return (attribs.Any() ? ((DescriptionAttribute) attribs.ElementAt(0)).Description : GenericEnum.ToString())!;
        }

        private string GetEnumLocalized(Enum e)
        {
            return ResourceManager.GetString($"{e.GetType().Name}.{e.ToString()}", CultureInfo) ?? GetDescription(e);
        }
        #endregion

        #region Fenstersteuerung nativ
        public enum GWL
        {
            ExStyle = -20
        }

        public enum WS_EX
        {
            Transparent = 0x20,
            Layered = 0x80000
        }

        public enum LWA
        {
            ColorKey = 0x1,
            Alpha = 0x2
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern long SetWindowLong(IntPtr hWnd, int nIndex, int asdf);

        [DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, LWA dwFlags);

        /// <summary>
        /// K�mmert sich um ClickThrough.
        /// SetLayeredWindowAttributes wird nicht verwendet, weil SetWindowLong damit interferiert. TransparencyKey ist "sicherer".
        /// </summary>
        /// <param name="e"></param>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var wl = GetWindowLong(Handle, (int) GWL.ExStyle);
            wl = wl | (int) WS_EX.Layered | (int) WS_EX.Transparent;
            SetWindowLong(Handle, (int) GWL.ExStyle, wl);
            //SetLayeredWindowAttributes(this.Handle, 0, 128, LWA.Alpha);
            TopMost = true;
        }

        /// <summary>
        /// Versteckt das Programm bei Alt+Tab
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // turn on WS_EX_TOOLWINDOW style bit
                cp.ExStyle |= 0x80;
                return cp;
            }
        }
        #endregion
    }
}