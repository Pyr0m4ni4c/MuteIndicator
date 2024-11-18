using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AudioStuff;
using MuteIndicator.Properties;
using NAudio.CoreAudioApi;

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
            [Description("en")] English, // quasi Platzhalter für unbekannte Sprachen...
            [Description("de")] Deutsch
        }

        #region Tray Icon separat erzeugen, weil der Designer das Tray Icon an die Form hängen würde
        private readonly ContextMenuStrip m_contextMenuStrip = new();
        private ToolStripMenuItem displaySettings = null;
        private ToolStripMenuItem cornerSettings = null;
        private ToolStripMenuItem sizeSettings = null;
        private ToolStripMenuItem languageSettings = null;
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
            BackColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00);
            //TransparencyKey = Color.LimeGreen;
            TransparencyKey = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00);
        
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Dock = DockStyle.Fill;
        
            UpdateCheckedMarks();
            UpdateFormLocation();

            _lastMute = DateTime.Now;
            SimpleMessageHandler.MuteReceived += OnMuteReceived;
            SimpleMessageHandler.CycleReceived += OnAudioCycleReceived;
            timer1.Enabled = true;

            // NUR mittels StopListening kann der Thread gestoppt werden
            var listenerThread = new Thread(AsynchronousSocketListener.StartListening);
            listenerThread.Start();
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

        private static string ReplaceDeviceAt(int insertAt, string nameArray, string toInsert)
        {
            const string separator = "|";
            if (string.IsNullOrEmpty(nameArray))
            {
                var list = new List<string>(new string[insertAt]) { toInsert, };
                return string.Join("|", list);
            }
            var names = nameArray.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (names.Length > insertAt)
                names[insertAt] = toInsert;
            else
            {
                var tmp = new List<string>(names) { toInsert };
                names = tmp.ToArray();
            }
            return string.Join(separator, names);
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
                    SetIndicator(pictureBox1, _lastState ? _colorMuted : _colorUnMuted, Settings1.Default.Size);
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
            }

            return toolStripMenuItem;
        }
        #endregion

        #region private GUI Events
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
        }

        private void UpdateFormLocation()
        {
            var displayName = Settings1.Default.DisplayName;
            var screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == displayName, Screen.PrimaryScreen);
            var bounds = screen.Bounds;

            var p = (CornerLocation) Settings1.Default.CornerLocation switch
            {
                CornerLocation.TopLeft => new Point(bounds.X, bounds.Y + 30),
                CornerLocation.TopRight => new Point(bounds.X + bounds.Width - 30, bounds.Y + 30),
                CornerLocation.BottomLeft => new Point(bounds.X, bounds.Y + bounds.Height - 60),
                CornerLocation.BottomRight => new Point(bounds.X + bounds.Width - 30, bounds.Y + bounds.Height - 60),
                _ => Point.Empty
            };

            Location = p;
        }
        #endregion

        #region Mute/UI Handling
        /*
        Nicht länger benötigt, aber aufgrund der Finesse zur verfeinerten Kantenglättung als nützlicher Snippet drin gelassen
        /// <summary>
        ///     Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            // Um die Kantenglättung von Windows zu umgehen und so einfarbige kleinst-Bilder darstellen zu können
            // die einfachere Lösung ist es einfach einen Kreis zu zeichnen... das hier ist Müll
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

        private static void SetIndicator(PictureBox pb, Color c, Size s)
        {
            using var bmp = new Bitmap(s.Width, s.Height);
            using var g = Graphics.FromImage(bmp);

            g.FillEllipse(new SolidBrush(c), new Rectangle(0, 0, s.Width, s.Height));

            try
            {
                Font f = new Font("Arial", 12);
                var defaultInputDeviceName = AudioGetter.DefaultInputDeviceName;
                var defaultOutputDeviceName = AudioGetter.DefaultOutputDeviceName;
                g.DrawString($"I:{defaultInputDeviceName[0]}", f, new SolidBrush(Color.Black), new PointF(10, 10));
                g.DrawString($"O:{defaultOutputDeviceName[0]}", f, new SolidBrush(Color.Black), new PointF(10, 20));
            }
            catch
            {
                // ignored
            }

            pb.Image = (Bitmap) bmp.Clone();
        }

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

            Settings1.Default.CurrentCombo = AudioCombo.ConvertToString(nextItem);
            Settings1.Default.Save();
        }

        private void OnMuteReceived(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new MuteReceivedDelegate(OnMuteReceived), message);
                return;
            }

            var muted = message.StartsWith("muted", StringComparison.CurrentCultureIgnoreCase)
                        || message.Contains("true", StringComparison.CurrentCultureIgnoreCase);

            if (muted == _lastState) return;
            if (DateTime.Now - _lastMute < TimeSpan.FromMilliseconds(HandlingTimeout)) return;

            _lastState = muted;
            SetIndicator(pictureBox1, muted ? _colorMuted : _colorUnMuted, Settings1.Default.Size);

            using var soundPlayer = new SoundPlayer(muted
                ? Resources.Mutesound
                : Resources.Unmutesound);
            soundPlayer.Play();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var muted = false;
            try
            {
                muted = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                    .AudioEndpointVolume.Mute;
            }
            catch (Exception exception)
            {
                ErrorLogging.WriteException(exception);
            }

            if (_lastState == muted)
                return;

            OnMuteReceived(muted.ToString());
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
        /// Kümmert sich um ClickThrough.
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