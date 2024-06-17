using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MuteIndicator
{
    public partial class SelectAudioCombo : Form
    {
        private AudioComboCollection m_ConfiguredCombos;

        public SelectAudioCombo(IEnumerable<string> inputDevices, IEnumerable<string> outputDevices, AudioComboCollection configuredCombos)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;

            m_ListBox_InputDevices.DataSource = inputDevices;
            m_ListBox_OutputDevices.DataSource = outputDevices;
            m_ConfiguredCombos = configuredCombos;
            m_ListBox_AudioCombos.DataSource = m_ConfiguredCombos;
            m_ListBox_AudioCombos.DisplayMember = "DisplayText";

            configuredCombos.CollectionChanged += ConfiguredCombosOnCollectionChanged;
        }

        private void ConfiguredCombosOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            m_ListBox_AudioCombos.DataSource = null;
            m_ConfiguredCombos = (AudioComboCollection) sender;
            m_ListBox_AudioCombos.DataSource = m_ConfiguredCombos;
            m_ListBox_AudioCombos.DisplayMember = "DisplayText";
        }

        private void m_Button_DeleteCombo_Click(object sender, EventArgs e)
        {
            if (m_ListBox_OutputDevices.SelectedItem == null) return;

            var audioCombo = (AudioCombo) m_ListBox_AudioCombos.SelectedItem;
            m_ConfiguredCombos.Remove(audioCombo);
        }

        private void m_Button_AddCombo_Click(object sender, EventArgs e)
        {
            var inputDevice = m_ListBox_InputDevices.SelectedItem?.ToString();
            var outputDevice = m_ListBox_OutputDevices.SelectedItem?.ToString();
            if (inputDevice == null || outputDevice == null) throw new Exception("is null");
            m_ConfiguredCombos.Add(new AudioCombo(inputDevice, outputDevice));
        }
    }
}