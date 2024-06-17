namespace MuteIndicator
{
    partial class SelectAudioCombo
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            m_ListBox_InputDevices = new ListBox();
            m_ListBox_OutputDevices = new ListBox();
            m_Button_AddCombo = new Button();
            m_Button_DeleteCombo = new Button();
            m_ListBox_AudioCombos = new ListBox();
            SuspendLayout();
            // 
            // m_ListBox_InputDevices
            // 
            m_ListBox_InputDevices.FormattingEnabled = true;
            m_ListBox_InputDevices.ItemHeight = 32;
            m_ListBox_InputDevices.Location = new Point(12, 12);
            m_ListBox_InputDevices.Name = "m_ListBox_InputDevices";
            m_ListBox_InputDevices.Size = new Size(402, 196);
            m_ListBox_InputDevices.TabIndex = 0;
            // 
            // m_ListBox_OutputDevices
            // 
            m_ListBox_OutputDevices.FormattingEnabled = true;
            m_ListBox_OutputDevices.ItemHeight = 32;
            m_ListBox_OutputDevices.Location = new Point(430, 12);
            m_ListBox_OutputDevices.Name = "m_ListBox_OutputDevices";
            m_ListBox_OutputDevices.Size = new Size(402, 196);
            m_ListBox_OutputDevices.TabIndex = 1;
            // 
            // m_Button_AddCombo
            // 
            m_Button_AddCombo.Location = new Point(838, 32);
            m_Button_AddCombo.Name = "m_Button_AddCombo";
            m_Button_AddCombo.Size = new Size(196, 66);
            m_Button_AddCombo.TabIndex = 2;
            m_Button_AddCombo.Text = "Add";
            m_Button_AddCombo.UseVisualStyleBackColor = true;
            m_Button_AddCombo.Click += m_Button_AddCombo_Click;
            // 
            // m_Button_DeleteCombo
            // 
            m_Button_DeleteCombo.Location = new Point(838, 128);
            m_Button_DeleteCombo.Name = "m_Button_DeleteCombo";
            m_Button_DeleteCombo.Size = new Size(196, 66);
            m_Button_DeleteCombo.TabIndex = 3;
            m_Button_DeleteCombo.Text = "Delete";
            m_Button_DeleteCombo.UseVisualStyleBackColor = true;
            m_Button_DeleteCombo.Click += m_Button_DeleteCombo_Click;
            // 
            // m_ListBox_AudioCombos
            // 
            m_ListBox_AudioCombos.FormattingEnabled = true;
            m_ListBox_AudioCombos.ItemHeight = 32;
            m_ListBox_AudioCombos.Location = new Point(12, 214);
            m_ListBox_AudioCombos.Name = "m_ListBox_AudioCombos";
            m_ListBox_AudioCombos.Size = new Size(1022, 132);
            m_ListBox_AudioCombos.TabIndex = 4;
            // 
            // SelectAudioCombo
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1040, 352);
            Controls.Add(m_ListBox_AudioCombos);
            Controls.Add(m_Button_DeleteCombo);
            Controls.Add(m_Button_AddCombo);
            Controls.Add(m_ListBox_OutputDevices);
            Controls.Add(m_ListBox_InputDevices);
            Name = "SelectAudioCombo";
            Text = "SelectAudioCombo";
            ResumeLayout(false);
        }

        #endregion

        private ListBox m_ListBox_InputDevices;
        private ListBox m_ListBox_OutputDevices;
        private Button m_Button_AddCombo;
        private Button m_Button_DeleteCombo;
        private ListBox m_ListBox_AudioCombos;
    }
}