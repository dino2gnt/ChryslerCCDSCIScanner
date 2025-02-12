﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using DRBDBReader.DB;
using DRBDBReader.DB.Records;

namespace ChryslerCCDSCIScanner
{
    public partial class MainForm : Form
    {
        public bool GUIUpdateAvailable = false;
        public bool FWUpdateAvailabile = false;
        public string GUIVersion = string.Empty;
        public string FWVersion = string.Empty;
        public string HWVersion = string.Empty;
        public UInt64 deviceFirmwareTimestamp;
        public string selectedPort = string.Empty;
        public bool timeout = false;
        public bool deviceFound = false;
        public const uint intEEPROMsize = 4096;
        public const uint extEEPROMsize = 4096;

        public static string USBTextLogFilename;
        public static string USBBinaryLogFilename;
        public static string CCDLogFilename;
        public static string CCDB2F2LogFilename;
        public static string CCDEPROMTextFilename;
        public static string CCDEPROMBinaryFilename;
        public static string CCDEEPROMTextFilename;
        public static string CCDEEPROMBinaryFilename;
        public static string PCMLogFilename;
        public static string PCMEPROMTextFilename;
        public static string PCMEPROMBinaryFilename;
        public static string PCMEEPROMTextFilename;
        public static string PCMEEPROMBinaryFilename;
        public static string TCMLogFilename;

        public int lastCCDScrollBarPosition = 0;
        public int lastPCMScrollBarPosition = 0;
        public int lastTCMScrollBarPosition = 0;

        public static bool includeTimestampInLogFiles = false;
        public static ushort tableRefreshRate = 0;

        public List<string> CCDTableBuffer = new List<string>();
        public List<int> CCDTableBufferLocation = new List<int>();
        public List<int> CCDTableRowCountHistory = new List<int>();

        public List<string> PCMTableBuffer = new List<string>();
        public List<int> PCMTableBufferLocation = new List<int>();
        public List<int> PCMTableRowCountHistory = new List<int>();

        public List<string> TCMTableBuffer = new List<string>();
        public List<int> TCMTableBufferLocation = new List<int>();
        public List<int> TCMTableRowCountHistory = new List<int>();

        public ReadMemoryForm ReadMemory;
        public WriteMemoryForm WriteMemory;
        public AboutForm About;
        public static Packet Packet = new Packet();
        public CCD CCD = new CCD();
        public SCIPCM PCM = new SCIPCM();
        public SCITCM TCM = new SCITCM();
        public System.Timers.Timer TimeoutTimer = new System.Timers.Timer();
        public System.Timers.Timer CCDTableRefreshTimer = new System.Timers.Timer();
        public System.Timers.Timer PCMTableRefreshTimer = new System.Timers.Timer();
        public System.Timers.Timer TCMTableRefreshTimer = new System.Timers.Timer();
        public WebClient Downloader = new WebClient();
        public FileInfo fi = new FileInfo(@"DRBDBReader/database.mem");
        public Database db;

        public MainForm()
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); // set application icon
            DiagnosticsGroupBox.Visible = false; // hide the expanded view components all at once
            Size = new Size(405, 650); // resize form to collapsed view
            CenterToScreen(); // put window at the center of the screen

            if (fi.Exists) db = new Database(fi); // load DRB3 database

            GUIVersion = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            Text += "  |  GUI " + GUIVersion;

            if (!Directory.Exists("LOG")) Directory.CreateDirectory("LOG");
            if (!Directory.Exists("LOG/CCD")) Directory.CreateDirectory("LOG/CCD");
            if (!Directory.Exists("LOG/PCM")) Directory.CreateDirectory("LOG/PCM");
            if (!Directory.Exists("LOG/TCM")) Directory.CreateDirectory("LOG/TCM");
            if (!Directory.Exists("LOG/USB")) Directory.CreateDirectory("LOG/USB");
            if (!Directory.Exists("ROMs")) Directory.CreateDirectory("ROMs");
            if (!Directory.Exists("ROMs/CCD")) Directory.CreateDirectory("ROMs/CCD");
            if (!Directory.Exists("ROMs/PCM")) Directory.CreateDirectory("ROMs/PCM");

            string DateTimeNow = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            USBTextLogFilename = @"LOG/USB/usblog_" + DateTimeNow + ".txt";
            USBBinaryLogFilename = @"LOG/USB/usblog_" + DateTimeNow + ".bin";

            CCDLogFilename = @"LOG/CCD/ccdlog_" + DateTimeNow + ".txt";
            CCDB2F2LogFilename = @"LOG/CCD/ccdb2f2log_" + DateTimeNow + ".txt";
            //CCDEPROMTextFilename = @"ROMS/CCD/ccd_eprom_" + DateTimeNow + ".txt";
            //CCDEPROMBinaryFilename = @"ROMs/CCD/ccd_eprom_" + DateTimeNow + ".bin";
            //CCDEEPROMTextFilename = @"ROMs/CCD/ccd_eeprom_" + DateTimeNow + ".txt";
            //CCDEEPROMBinaryFilename = @"ROMs/CCD/ccd_eeprom_" + DateTimeNow + ".bin";

            PCMLogFilename = @"LOG/PCM/pcmlog_" + DateTimeNow + ".txt";
            PCMEPROMTextFilename = @"ROMs/PCM/pcm_eprom_" + DateTimeNow + ".txt";
            //PCMEPROMBinaryFilename = @"ROMs/PCM/pcm_eprom_" + DateTimeNow + ".bin";
            //PCMEEPROMTextFilename = @"ROMs/PCM/pcm_eeprom_" + DateTimeNow + ".txt";
            //PCMEEPROMBinaryFilename = @"ROMs/PCM/pcm_eeprom_" + DateTimeNow + ".bin";

            TCMLogFilename = @"LOG/TCM/tcmlog_" + DateTimeNow + ".txt";

            TimeoutTimer.Elapsed += new ElapsedEventHandler(TimeoutHandler);
            TimeoutTimer.Interval = 2000; // ms
            TimeoutTimer.AutoReset = false;
            TimeoutTimer.Enabled = true;
            TimeoutTimer.Stop();
            timeout = false;

            CCDTableRefreshTimer.Elapsed += new ElapsedEventHandler(CCDTableRefreshHandler);
            CCDTableRefreshTimer.Interval = 25; // ms
            CCDTableRefreshTimer.AutoReset = true;
            CCDTableRefreshTimer.Enabled = true;

            PCMTableRefreshTimer.Elapsed += new ElapsedEventHandler(PCMTableRefreshHandler);
            PCMTableRefreshTimer.Interval = 25; // ms
            PCMTableRefreshTimer.AutoReset = true;
            PCMTableRefreshTimer.Enabled = true;

            TCMTableRefreshTimer.Elapsed += new ElapsedEventHandler(TCMTableRefreshHandler);
            TCMTableRefreshTimer.Interval = 25; // ms
            TCMTableRefreshTimer.AutoReset = true;
            TCMTableRefreshTimer.Enabled = true;

            UpdateCOMPortList();

            CCDBusDiagnosticsListBox.Items.AddRange(CCD.Diagnostics.Table.ToArray());
            SCIBusPCMDiagnosticsListBox.Items.AddRange(PCM.Diagnostics.Table.ToArray());
            SCIBusTCMDiagnosticsListBox.Items.AddRange(TCM.Diagnostics.Table.ToArray());

            SCIBusModuleComboBox.SelectedIndex = 0;
            SCIBusOBDConfigurationComboBox.SelectedIndex = 0;
            SCIBusSpeedComboBox.SelectedIndex = 2;
            LCDStateComboBox.SelectedIndex = 0;
            LCDDataSourceComboBox.SelectedIndex = 0;

            // Load saved settings.
            if (Properties.Settings.Default.Units == "metric")
            {
                MetricUnitsToolStripMenuItem.Checked = true;
                ImperialUnitsToolStripMenuItem.Checked = false;
            }
            else if (Properties.Settings.Default.Units == "imperial")
            {
                MetricUnitsToolStripMenuItem.Checked = false;
                ImperialUnitsToolStripMenuItem.Checked = true;
            }

            if (Properties.Settings.Default.Timestamp == true)
            {
                IncludeTimestampInLogFilesToolStripMenuItem.Checked = true;
                includeTimestampInLogFiles = true;
            }
            else
            {
                IncludeTimestampInLogFilesToolStripMenuItem.Checked = false;
                includeTimestampInLogFiles = false;
            }

            if (Properties.Settings.Default.CCDBusOnDemand == true)
            {
                CCDBusOnDemandToolStripMenuItem.Checked = true;
            }
            else
            {
                CCDBusOnDemandToolStripMenuItem.Checked = false;
            }

            ActiveControl = ConnectButton; // put focus on the connect button
        }

        #region Methods

        private void TimeoutHandler(object source, ElapsedEventArgs e) => timeout = true;

        private void CCDTableRefreshHandler(object source, ElapsedEventArgs e)
        {
            if (CCDTableBuffer.Count > 0)
            {
                CCDBusDiagnosticsListBox.BeginInvoke((MethodInvoker)delegate
                {
                    CCDBusDiagnosticsListBox.BeginUpdate();

                    lastCCDScrollBarPosition = CCDBusDiagnosticsListBox.GetVerticalScrollPosition();

                    // Update header line.
                    CCDBusDiagnosticsListBox.Items.RemoveAt(1);
                    CCDBusDiagnosticsListBox.Items.Insert(1, CCD.Diagnostics.Table[1]);

                    // Update lines from buffer.
                    for (int i = 0; i < CCDTableBuffer.Count; i++)
                    {
                        if (CCDBusDiagnosticsListBox.Items.Count == CCDTableRowCountHistory[i])
                        {
                            CCDBusDiagnosticsListBox.Items.RemoveAt(CCDTableBufferLocation[i]);
                        }

                        CCDBusDiagnosticsListBox.Items.Insert(CCDTableBufferLocation[i], CCDTableBuffer[i]);
                    }

                    CCDBusDiagnosticsListBox.SetVerticalScrollPosition(lastCCDScrollBarPosition);

                    CCDBusDiagnosticsListBox.EndUpdate();

                    CCDTableBuffer.Clear();
                    CCDTableBufferLocation.Clear();
                    CCDTableRowCountHistory.Clear();
                });
            }
        }

        private void PCMTableRefreshHandler(object source, ElapsedEventArgs e)
        {
            if (PCMTableBuffer.Count > 0)
            {
                SCIBusPCMDiagnosticsListBox.BeginInvoke((MethodInvoker)delegate
                {
                    SCIBusPCMDiagnosticsListBox.BeginUpdate();

                    lastPCMScrollBarPosition = SCIBusPCMDiagnosticsListBox.GetVerticalScrollPosition();

                    // Update header line.
                    SCIBusPCMDiagnosticsListBox.Items.RemoveAt(1);
                    SCIBusPCMDiagnosticsListBox.Items.Insert(1, PCM.Diagnostics.Table[1]);

                    // Update lines from buffer.
                    for (int i = 0; i < PCMTableBuffer.Count; i++)
                    {
                        if (SCIBusPCMDiagnosticsListBox.Items.Count == PCMTableRowCountHistory[i])
                        {
                            SCIBusPCMDiagnosticsListBox.Items.RemoveAt(PCMTableBufferLocation[i]);
                        }

                        SCIBusPCMDiagnosticsListBox.Items.Insert(PCMTableBufferLocation[i], PCMTableBuffer[i]);
                    }

                    SCIBusPCMDiagnosticsListBox.SetVerticalScrollPosition(lastPCMScrollBarPosition);

                    SCIBusPCMDiagnosticsListBox.EndUpdate();

                    PCMTableBuffer.Clear();
                    PCMTableBufferLocation.Clear();
                    PCMTableRowCountHistory.Clear();
                });
            }
        }

        private void TCMTableRefreshHandler(object source, ElapsedEventArgs e)
        {
            if (TCMTableBuffer.Count > 0)
            {
                SCIBusTCMDiagnosticsListBox.BeginInvoke((MethodInvoker)delegate
                {
                    SCIBusTCMDiagnosticsListBox.BeginUpdate();

                    lastTCMScrollBarPosition = SCIBusTCMDiagnosticsListBox.GetVerticalScrollPosition();

                    // Update header line.
                    SCIBusTCMDiagnosticsListBox.Items.RemoveAt(1);
                    SCIBusTCMDiagnosticsListBox.Items.Insert(1, TCM.Diagnostics.Table[1]);

                    // Update lines from buffer.
                    for (int i = 0; i < TCMTableBuffer.Count; i++)
                    {
                        if (SCIBusTCMDiagnosticsListBox.Items.Count == TCMTableRowCountHistory[i])
                        {
                            SCIBusTCMDiagnosticsListBox.Items.RemoveAt(TCMTableBufferLocation[i]);
                        }

                        SCIBusTCMDiagnosticsListBox.Items.Insert(TCMTableBufferLocation[i], TCMTableBuffer[i]);
                    }

                    SCIBusTCMDiagnosticsListBox.SetVerticalScrollPosition(lastTCMScrollBarPosition);

                    SCIBusTCMDiagnosticsListBox.EndUpdate();

                    TCMTableBuffer.Clear();
                    TCMTableBufferLocation.Clear();
                    TCMTableRowCountHistory.Clear();
                });
            }
        }

        private void UpdateCOMPortList()
        {
            COMPortsComboBox.Items.Clear(); // clear combobox
            string[] ports = SerialPort.GetPortNames(); // get available ports

            if (ports.Length > 0)
            {
                COMPortsComboBox.Items.AddRange(ports);
                ConnectButton.Enabled = true;

                if (selectedPort == string.Empty) // if no port has been selected
                {
                    COMPortsComboBox.SelectedIndex = 0; // select first available port
                    selectedPort = COMPortsComboBox.Text;
                }
                else
                {
                    try
                    {
                        COMPortsComboBox.SelectedIndex = COMPortsComboBox.Items.IndexOf(selectedPort); // try to find previously selected port
                    }
                    catch
                    {
                        COMPortsComboBox.SelectedIndex = 0; // if previously selected port is not found then select first available port
                    }
                }
            }
            else
            {
                COMPortsComboBox.Items.Add("N/A");
                ConnectButton.Enabled = false;
                COMPortsComboBox.SelectedIndex = 0; // select "N/A"
                selectedPort = string.Empty;
                Util.UpdateTextBox(USBTextBox, "[INFO] No device available.");
            }
        }

        private async void SetRepeatedMessageBehavior(Packet.Target bus)
        {
            List<byte> payloadList = new List<byte>();
            bool success = false;
            int repeatInterval = 0;
            byte[] repeatIntervalArray = new byte[2];

            switch (bus)
            {
                case Packet.Target.ccd:
                    success = int.TryParse(CCDBusTxMessageRepeatIntervalTextBox.Text, out repeatInterval);
                    break;
                case Packet.Target.pcm:
                    success = int.TryParse(SCIBusTxMessageRepeatIntervalTextBox.Text, out repeatInterval);
                    break;
                case Packet.Target.tcm:
                    success = int.TryParse(SCIBusTxMessageRepeatIntervalTextBox.Text, out repeatInterval);
                    break;
                default:
                    break;
            }

            if (!success || (repeatInterval == 0))
            {
                repeatInterval = 100;

                switch (bus)
                {
                    case Packet.Target.ccd:
                        CCDBusTxMessageRepeatIntervalTextBox.Text = "100";
                        break;
                    case Packet.Target.pcm:
                        SCIBusTxMessageRepeatIntervalTextBox.Text = "100";
                        break;
                    case Packet.Target.tcm:
                        SCIBusTxMessageRepeatIntervalTextBox.Text = "100";
                        break;
                    default:
                        break;
                }
            }

            repeatIntervalArray[0] = (byte)((repeatInterval >> 8) & 0xFF);
            repeatIntervalArray[1] = (byte)(repeatInterval & 0xFF);

            byte[] repeatIncrement = new byte[] { };

            switch (bus)
            {
                case Packet.Target.ccd:
                    repeatIncrement = Util.HexStringToByte(CCDBusTxMessageRepeatIncrementTextBox.Text);
                    break;
                case Packet.Target.pcm:
                    repeatIncrement = Util.HexStringToByte(SCIBusTxMessageRepeatIncrementTextBox.Text);
                    break;
                case Packet.Target.tcm:
                    repeatIncrement = Util.HexStringToByte(SCIBusTxMessageRepeatIncrementTextBox.Text);
                    break;
                default:
                    break;
            }

            if (repeatIncrement.Length == 0)
            {
                switch (bus)
                {
                    case Packet.Target.ccd:
                        repeatIncrement = new byte[] { 0x00, 0x02 };
                        CCDBusTxMessageRepeatIncrementTextBox.Text = "00 02";
                        break;
                    case Packet.Target.pcm:
                        repeatIncrement = new byte[] { 0x00, 0x01 };
                        SCIBusTxMessageRepeatIncrementTextBox.Text = "00 01";
                        break;
                    case Packet.Target.tcm:
                        SCIBusTxMessageRepeatIncrementTextBox.Text = "00 01";
                        break;
                    default:
                        break;
                }

            }
            else if (repeatIncrement.Length != 2)
            {
                MessageBox.Show("Repeat increment length must be 2 bytes long!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            payloadList.Add((byte)bus);
            payloadList.AddRange(repeatIntervalArray);
            payloadList.AddRange(repeatIncrement);

            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.settings;
            Packet.tx.mode = (byte)Packet.SettingsMode.setRepeatBehavior;
            Packet.tx.payload = payloadList.ToArray();
            Packet.GeneratePacket();

            switch (bus)
            {
                case Packet.Target.ccd:
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Set CCD-bus message repeat behavior:", Packet.tx.buffer);
                    break;
                case Packet.Target.pcm:
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Set SCI-bus (PCM) message repeat behavior:", Packet.tx.buffer);
                    break;
                case Packet.Target.tcm:
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Set SCI-bus (TCM) message repeat behavior:", Packet.tx.buffer);
                    break;
                default:
                    break;
            }

            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private void AnalyzePacket(object sender, EventArgs e)
        {
            switch (Packet.rx.source)
            {
                case (byte)Packet.Source.device:
                    switch (Packet.rx.command)
                    {
                        case (byte)Packet.Command.reset:
                            if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 0))
                            {
                                switch (Packet.rx.mode)
                                {
                                    case (byte)Packet.ResetMode.resetInit:
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Reset in progress:", Packet.rx.buffer);
                                        Util.UpdateTextBox(USBTextBox, "[INFO] Device is resetting, please wait.");
                                        break;
                                    case (byte)Packet.ResetMode.resetDone:
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Reset done:", Packet.rx.buffer);
                                        Util.UpdateTextBox(USBTextBox, "[INFO] Device is ready to accept instructions.");
                                        break;
                                    default:
                                        Util.UpdateTextBox(USBTextBox, "[INFO] Unknown reset packet.");
                                        break;
                                }
                            }
                            else
                            {
                                Util.UpdateTextBox(USBTextBox, "[RX->] Invalid reset packet:", Packet.rx.buffer);
                            }
                            break;
                        case (byte)Packet.Command.handshake:
                            if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 20))
                            {
                                Util.UpdateTextBox(USBTextBox, "[RX->] Handshake response:", Packet.rx.buffer);

                                if (Util.CompareArrays(Packet.rx.buffer, Packet.expectedHandshake, 0, Packet.expectedHandshake.Length))
                                {
                                    Util.UpdateTextBox(USBTextBox, "[INFO] Handshake OK: " + Encoding.ASCII.GetString(Packet.rx.payload, 0, Packet.rx.payload.Length));
                                }
                                else
                                {
                                    Util.UpdateTextBox(USBTextBox, "[INFO] Handshake ERROR: " + Encoding.ASCII.GetString(Packet.rx.payload, 0, Packet.rx.payload.Length));
                                }
                            }
                            else
                            {
                                Util.UpdateTextBox(USBTextBox, "[RX->] Invalid handshake packet:", Packet.rx.buffer);
                            }
                            break;
                        case (byte)Packet.Command.status:
                            if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 52))
                            {
                                string AVRSignature = Util.ByteToHexString(Packet.rx.payload, 0, 3);
                                if ((Packet.rx.payload[0] == 0x1E) && (Packet.rx.payload[1] == 0x98) && (Packet.rx.payload[2] == 0x01)) AVRSignature += " (ATmega2560)";
                                else AVRSignature += " (unknown)";

                                string extEEPROMPresent = string.Empty;
                                if (Packet.rx.payload[3] == 0x01) extEEPROMPresent += "yes";
                                else extEEPROMPresent += "no";
                                string extEEPROMChecksum = Util.ByteToHexString(Packet.rx.payload, 4, 1);
                                if (Packet.rx.payload[4] == Packet.rx.payload[5]) extEEPROMChecksum += "=OK";
                                else extEEPROMChecksum += "!=" + Util.ByteToHexString(Packet.rx.payload, 5, 1) + ", ERROR";

                                TimeSpan elapsedMillis = TimeSpan.FromMilliseconds((Packet.rx.payload[6] << 24) + (Packet.rx.payload[7] << 16) + (Packet.rx.payload[8] << 8) + Packet.rx.payload[9]);
                                DateTime microcontrollerTimestamp = DateTime.Today.Add(elapsedMillis);
                                string microcontrollerTimestampString = microcontrollerTimestamp.ToString("HH:mm:ss.fff");

                                int freeRAM = (Packet.rx.payload[10] << 8) + Packet.rx.payload[11];
                                string freeRAMString = (100 * ((8192.0 - freeRAM) / 8192.0)).ToString("0.0") + "% (" + (8192 - freeRAM).ToString("0") + "/8192 bytes)";

                                string connectedToVehicle = string.Empty;
                                if (Packet.rx.payload[12] == 0x01) connectedToVehicle = "yes";
                                else connectedToVehicle = "no";

                                string batteryVoltageString = (((Packet.rx.payload[13] << 8) + Packet.rx.payload[14]) / 100.00).ToString("0.00").Replace(",", ".") + " V";

                                string CCDBusStateString = string.Empty;
                                string CCDBusLogicString = string.Empty;
                                string CCDTerminationBiasEnabledString = string.Empty;
                                string CCDBusSpeedString = string.Empty;
                                string CCDBusMsgRxCountString = string.Empty;
                                string CCDBusMsgTxCountString = string.Empty;

                                if (Util.IsBitClear(Packet.rx.payload[15], 4))
                                {
                                    CCDBusTransceiverOnOffCheckBox.CheckedChanged -= new EventHandler(CCDBusSettingsCheckBox_CheckedChanged);
                                    CCDBusTransceiverOnOffCheckBox.Checked = false;
                                    CCDBusTransceiverOnOffCheckBox.Text = "CCD-bus transceiver OFF";
                                    CCDBusTransceiverOnOffCheckBox.CheckedChanged += new EventHandler(CCDBusSettingsCheckBox_CheckedChanged);
                                    CCDBusStateString = "disabled";
                                }
                                else
                                {
                                    CCDBusTransceiverOnOffCheckBox.CheckedChanged -= new EventHandler(CCDBusSettingsCheckBox_CheckedChanged); // unsubscribe from this event to set checkbox without raising said event
                                    CCDBusTransceiverOnOffCheckBox.Checked = true;
                                    CCDBusTransceiverOnOffCheckBox.Text = "CCD-bus transceiver ON";
                                    CCDBusTransceiverOnOffCheckBox.CheckedChanged += new EventHandler(CCDBusSettingsCheckBox_CheckedChanged); // re-subscribe to event
                                    CCDBusStateString = "enabled";
                                }

                                if (Util.IsBitClear(Packet.rx.payload[15], 3)) CCDBusLogicString = "non-inverted";
                                else CCDBusLogicString = "inverted";

                                if (Util.IsBitClear(Packet.rx.payload[15], 2))
                                {
                                    CCDBusTerminationBiasOnOffCheckBox.CheckedChanged -= new EventHandler(this.CCDBusSettingsCheckBox_CheckedChanged);
                                    CCDBusTerminationBiasOnOffCheckBox.Checked = false;
                                    CCDBusTerminationBiasOnOffCheckBox.Text = "CCD-bus termination / bias OFF";
                                    CCDBusTerminationBiasOnOffCheckBox.CheckedChanged += new EventHandler(this.CCDBusSettingsCheckBox_CheckedChanged);
                                    CCDTerminationBiasEnabledString = "disabled";
                                }
                                else
                                {
                                    CCDBusTerminationBiasOnOffCheckBox.CheckedChanged -= new EventHandler(this.CCDBusSettingsCheckBox_CheckedChanged);
                                    CCDBusTerminationBiasOnOffCheckBox.Checked = true;
                                    CCDBusTerminationBiasOnOffCheckBox.Text = "CCD-bus termination / bias ON";
                                    CCDBusTerminationBiasOnOffCheckBox.CheckedChanged += new EventHandler(this.CCDBusSettingsCheckBox_CheckedChanged);
                                    CCDTerminationBiasEnabledString = "enabled";
                                }

                                switch (Packet.rx.payload[15] & 0x03)
                                {
                                    case 0x00:
                                        CCDBusSpeedString = "976.5 baud";
                                        break;
                                    case 0x01:
                                        CCDBusSpeedString = "7812.5 baud";
                                        break;
                                    case 0x02:
                                        CCDBusSpeedString = "62500 baud";
                                        break;
                                    case 0x03:
                                        CCDBusSpeedString = "125000 baud";
                                        break;
                                    default:
                                        CCDBusSpeedString = "unknown";
                                        break;
                                }

                                CCD.UpdateHeader(CCDBusStateString, CCDBusSpeedString, CCDBusLogicString);

                                CCDBusMsgRxCountString = ((Packet.rx.payload[16] << 24) + (Packet.rx.payload[17] << 16) + (Packet.rx.payload[18] << 8) + Packet.rx.payload[19]).ToString();
                                CCDBusMsgTxCountString = ((Packet.rx.payload[20] << 24) + (Packet.rx.payload[21] << 16) + (Packet.rx.payload[22] << 8) + Packet.rx.payload[23]).ToString();

                                string SCIBusPCMStateString = string.Empty;
                                string SCIBusPCMLogicString = string.Empty;
                                string SCIBusPCMOBDConfigurationString = string.Empty;
                                string SCIBusPCMSpeedString = string.Empty;
                                string SCIBusPCMMsgRxCountString = string.Empty;
                                string SCIBusPCMMsgTxCountString = string.Empty;

                                if (Util.IsBitSet(Packet.rx.payload[24], 4))
                                {
                                    SCIBusPCMStateString = "enabled";

                                    if (Util.IsBitClear(Packet.rx.payload[24], 3))
                                    {
                                        SCIBusPCMLogicString += "non-inverted";
                                        SCIBusOBD1EngineCableUsedCheckBox.Checked = false;
                                    }
                                    else
                                    {
                                        SCIBusPCMLogicString += "inverted";
                                        SCIBusOBD1EngineCableUsedCheckBox.Checked = true;
                                    }

                                    if (Util.IsBitClear(Packet.rx.payload[24], 2))
                                    {
                                        SCIBusPCMOBDConfigurationString = "A";
                                        SCIBusOBDConfigurationComboBox.SelectedIndex = 0;
                                    }
                                    else
                                    {
                                        SCIBusPCMOBDConfigurationString = "B";
                                        SCIBusOBDConfigurationComboBox.SelectedIndex = 1;
                                    }

                                    switch (Packet.rx.payload[24] & 0x03)
                                    {
                                        case 0x00:
                                            SCIBusPCMSpeedString = "976.5 baud";
                                            SCIBusSpeedComboBox.SelectedIndex = 1;
                                            SCIBusReadFaultCodesButton.Enabled = true;
                                            SCIBusEraseFaultCodesButton.Enabled = true;
                                            break;
                                        case 0x01:
                                            SCIBusPCMSpeedString = "7812.5 baud";
                                            SCIBusSpeedComboBox.SelectedIndex = 2;
                                            SCIBusReadFaultCodesButton.Enabled = true;
                                            SCIBusEraseFaultCodesButton.Enabled = true;
                                            break;
                                        case 0x02:
                                            SCIBusPCMSpeedString = "62500 baud";
                                            SCIBusSpeedComboBox.SelectedIndex = 3;
                                            SCIBusReadFaultCodesButton.Enabled = false;
                                            SCIBusEraseFaultCodesButton.Enabled = false;
                                            break;
                                        case 0x03:
                                            SCIBusPCMSpeedString = "125000 baud";
                                            SCIBusSpeedComboBox.SelectedIndex = 4;
                                            SCIBusReadFaultCodesButton.Enabled = true;
                                            SCIBusEraseFaultCodesButton.Enabled = true;
                                            break;
                                        default:
                                            SCIBusPCMSpeedString = "unknown";
                                            SCIBusSpeedComboBox.SelectedIndex = 0;
                                            SCIBusReadFaultCodesButton.Enabled = true;
                                            SCIBusEraseFaultCodesButton.Enabled = true;
                                            break;
                                    }
                                }
                                else
                                {
                                    SCIBusPCMStateString = "disabled";
                                    SCIBusPCMLogicString = "-";
                                    SCIBusPCMOBDConfigurationString = "-";
                                    SCIBusPCMSpeedString = "-";
                                    SCIBusSpeedComboBox.SelectedIndex = 0;
                                }

                                PCM.UpdateHeader(SCIBusPCMStateString, SCIBusPCMSpeedString, SCIBusPCMLogicString, SCIBusPCMOBDConfigurationString);

                                SCIBusPCMMsgRxCountString = ((Packet.rx.payload[25] << 24) + (Packet.rx.payload[26] << 16) + (Packet.rx.payload[27] << 8) + Packet.rx.payload[28]).ToString();
                                SCIBusPCMMsgTxCountString = ((Packet.rx.payload[29] << 24) + (Packet.rx.payload[30] << 16) + (Packet.rx.payload[31] << 8) + Packet.rx.payload[32]).ToString();

                                string SCIBusTCMStateString = string.Empty;
                                string SCIBusTCMLogicString = string.Empty;
                                string SCIBusTCMOBDConfigurationString = string.Empty;
                                string SCIBusTCMSpeedString = string.Empty;
                                string SCIBusTCMMsgRxCountString = string.Empty;
                                string SCIBusTCMMsgTxCountString = string.Empty;

                                if (Util.IsBitSet(Packet.rx.payload[33], 4))
                                {
                                    SCIBusTCMStateString = "enabled";

                                    if (Util.IsBitClear(Packet.rx.payload[33], 3))
                                    {
                                        SCIBusTCMLogicString += "non-inverted";
                                    }
                                    else
                                    {
                                        SCIBusTCMLogicString += "inverted";
                                    }

                                    if (Util.IsBitClear(Packet.rx.payload[33], 2))
                                    {
                                        SCIBusTCMOBDConfigurationString = "A";
                                    }
                                    else
                                    {
                                        SCIBusTCMOBDConfigurationString = "B";
                                    }

                                    switch (Packet.rx.payload[33] & 0x03)
                                    {
                                        case 0x00:
                                            SCIBusTCMSpeedString = "976.5 baud";
                                            break;
                                        case 0x01:
                                            SCIBusTCMSpeedString = "7812.5 baud";
                                            break;
                                        case 0x02:
                                            SCIBusTCMSpeedString = "62500 baud";
                                            break;
                                        case 0x03:
                                            SCIBusTCMSpeedString = "125000 baud";
                                            break;
                                        default:
                                            SCIBusTCMSpeedString = "unknown";
                                            break;
                                    }
                                }
                                else
                                {
                                    SCIBusTCMStateString = "disabled";
                                    SCIBusTCMLogicString = "-";
                                    SCIBusTCMOBDConfigurationString = "-";
                                    SCIBusTCMSpeedString = "-";
                                }

                                TCM.UpdateHeader(SCIBusTCMStateString, SCIBusTCMSpeedString, SCIBusTCMLogicString, SCIBusTCMOBDConfigurationString);

                                SCIBusTCMMsgRxCountString = ((Packet.rx.payload[34] << 24) + (Packet.rx.payload[35] << 16) + (Packet.rx.payload[36] << 8) + Packet.rx.payload[37]).ToString();
                                SCIBusTCMMsgTxCountString = ((Packet.rx.payload[38] << 24) + (Packet.rx.payload[39] << 16) + (Packet.rx.payload[40] << 8) + Packet.rx.payload[41]).ToString();

                                string LCDStatusString = string.Empty;
                                string LCDI2CAddressString = string.Empty;
                                string LCDSizeString = string.Empty;
                                string LCDRefreshRateString = string.Empty;
                                string LCDUnitsString = string.Empty;
                                string LCDDataSourceString = string.Empty;

                                if (Packet.rx.payload[42] == 0x00)
                                {
                                    LCDStateComboBox.SelectedIndex = 0;
                                    LCDStatusString = "disabled";
                                }
                                else
                                {
                                    LCDStateComboBox.SelectedIndex = 1;
                                    LCDStatusString = "enabled";
                                }

                                LCDI2CAddressTextBox.Text = Util.ByteToHexString(Packet.rx.payload, 43, 1);
                                LCDI2CAddressString = Util.ByteToHexString(Packet.rx.payload, 43, 1) + " (hex)";

                                LCDWidthTextBox.Text = Packet.rx.payload[44].ToString("0");
                                LCDHeightTextBox.Text = Packet.rx.payload[45].ToString("0");
                                LCDSizeString = Packet.rx.payload[44].ToString("0") + "x" + Packet.rx.payload[45].ToString("0") + " characters";

                                LCDRefreshRateTextBox.Text = Packet.rx.payload[46].ToString("0");
                                LCDRefreshRateString = Packet.rx.payload[46].ToString("0") + " Hz";

                                if (Packet.rx.payload[47] == 0x00)
                                {
                                    LCDUnitsString = "imperial";
                                }
                                else
                                {
                                    LCDUnitsString = "metric";
                                }

                                switch (Packet.rx.payload[48])
                                {
                                    case 0x01:
                                        LCDDataSourceString = "CCD-bus";
                                        LCDDataSourceComboBox.SelectedIndex = 0;
                                        break;
                                    case 0x02:
                                        LCDDataSourceString = "SCI-bus (PCM)";
                                        LCDDataSourceComboBox.SelectedIndex = 1;
                                        break;
                                    case 0x03:
                                        LCDDataSourceString = "SCI-bus (TCM)";
                                        LCDDataSourceComboBox.SelectedIndex = 2;
                                        break;
                                    default:
                                        LCDDataSourceString = "CCD-bus";
                                        LCDDataSourceComboBox.SelectedIndex = 0;
                                        break;
                                }

                                ushort LEDHeartbeatInterval = (ushort)((Packet.rx.payload[49] << 8) + Packet.rx.payload[50]);
                                ushort LEDBlinkDuration = (ushort)((Packet.rx.payload[51] << 8) + Packet.rx.payload[52]);

                                string LEDHeartbeatStateString = string.Empty;
                                if (LEDHeartbeatInterval > 0) LEDHeartbeatStateString = "enabled";
                                else LEDHeartbeatStateString = "disabled";

                                string LEDBlinkDurationString = LEDBlinkDuration.ToString() + " ms";
                                string LEDHeartbeatIntervalString = LEDHeartbeatInterval.ToString() + " ms";
                                HeartbeatIntervalTextBox.Text = LEDHeartbeatInterval.ToString();
                                LEDBlinkDurationTextBox.Text = LEDBlinkDuration.ToString();

                                Util.UpdateTextBox(USBTextBox, "[RX->] Status response:", Packet.rx.buffer);
                                Util.UpdateTextBox(USBTextBox, "[INFO] -------------Device status--------------" + Environment.NewLine +
                                                               "       AVR signature: " + AVRSignature + Environment.NewLine +
                                                               "       External EEPROM present: " + extEEPROMPresent + Environment.NewLine +
                                                               "       External EEPROM checksum: " + extEEPROMChecksum + Environment.NewLine +
                                                               "       Timestamp: " + microcontrollerTimestampString + Environment.NewLine +
                                                               "       RAM usage: " + freeRAMString + Environment.NewLine +
                                                               "       Connected to vehicle: " + connectedToVehicle + Environment.NewLine +
                                                               "       Battery voltage: " + batteryVoltageString + Environment.NewLine +
                                                               "       -------------CCD-bus status-------------" + Environment.NewLine +
                                                               "       State: " + CCDBusStateString + Environment.NewLine +
                                                               "       Logic: " + CCDBusLogicString + Environment.NewLine +
                                                               "       Termination and bias: " + CCDTerminationBiasEnabledString + Environment.NewLine +
                                                               "       Speed: " + CCDBusSpeedString + Environment.NewLine +
                                                               "       Messages received: " + CCDBusMsgRxCountString + Environment.NewLine +
                                                               "       Messages sent: " + CCDBusMsgTxCountString + Environment.NewLine +
                                                               "       ----------SCI-bus (PCM) status----------" + Environment.NewLine +
                                                               "       State: " + SCIBusPCMStateString + Environment.NewLine +
                                                               "       Logic: " + SCIBusPCMLogicString + Environment.NewLine +
                                                               "       OBD config.: " + SCIBusPCMOBDConfigurationString + Environment.NewLine +
                                                               "       Speed: " + SCIBusPCMSpeedString + Environment.NewLine +
                                                               "       Messages received: " + SCIBusPCMMsgRxCountString + Environment.NewLine +
                                                               "       Messages sent: " + SCIBusPCMMsgTxCountString + Environment.NewLine +
                                                               "       ----------SCI-bus (TCM) status----------" + Environment.NewLine +
                                                               "       State: " + SCIBusTCMStateString + Environment.NewLine +
                                                               "       Logic: " + SCIBusTCMLogicString + Environment.NewLine +
                                                               "       OBD config.: " + SCIBusTCMOBDConfigurationString + Environment.NewLine +
                                                               "       Speed: " + SCIBusTCMSpeedString + Environment.NewLine +
                                                               "       Messages received: " + SCIBusTCMMsgRxCountString + Environment.NewLine +
                                                               "       Messages sent: " + SCIBusTCMMsgTxCountString + Environment.NewLine +
                                                               "       ---------------LCD status---------------" + Environment.NewLine +
                                                               "       State: " + LCDStatusString + Environment.NewLine +
                                                               "       I2C address: " + LCDI2CAddressString + Environment.NewLine +
                                                               "       Size: " + LCDSizeString + Environment.NewLine +
                                                               "       Refresh rate: " + LCDRefreshRateString + Environment.NewLine +
                                                               "       Units: " + LCDUnitsString + Environment.NewLine +
                                                               "       Data source: " + LCDDataSourceString + Environment.NewLine +
                                                               "       ---------------LED status---------------" + Environment.NewLine +
                                                               "       Heartbeat state: " + LEDHeartbeatStateString + Environment.NewLine +
                                                               "       Heartbeat interval: " + LEDHeartbeatIntervalString + Environment.NewLine+
                                                               "       Blink duration: " + LEDBlinkDurationString);

                                UpdateLCDPreviewTextBox();
                            }
                            else
                            {
                                Util.UpdateTextBox(USBTextBox, "[RX->] Invalid status packet:", Packet.rx.buffer);
                            }
                            break;
                        case (byte)Packet.Command.settings:
                            switch (Packet.rx.mode)
                            {
                                case (byte)Packet.SettingsMode.leds:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 4))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] LED settings changed:", Packet.rx.buffer);

                                            int heartbeatInterval = (Packet.rx.payload[1] << 8) + Packet.rx.payload[2];
                                            int blinkDuration = (Packet.rx.payload[3] << 8) + Packet.rx.payload[4];
                                            string heartbeatIntervalString = heartbeatInterval.ToString() + " ms";
                                            string blinkDurationString = blinkDuration.ToString() + " ms";

                                            string heartbeatStateString = string.Empty;
                                            if (heartbeatInterval > 0) heartbeatStateString = "enabled";
                                            else heartbeatStateString = "disabled";

                                            Util.UpdateTextBox(USBTextBox, "[INFO] LED settings:" + Environment.NewLine +
                                                                            "       Heartbeat state: " + heartbeatStateString + Environment.NewLine +
                                                                            "       Heartbeat interval: " + heartbeatIntervalString + Environment.NewLine +
                                                                            "       LED blink duration: " + blinkDurationString);
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] LED settings error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid settings packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.SettingsMode.setCCDBus:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 2))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string CCDBusStateString = string.Empty;
                                            string CCDBusTerminationBiasString = string.Empty;

                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.OnOffMode.off:
                                                    CCDBusStateString = "disabled";
                                                    break;
                                                case (byte)Packet.OnOffMode.on:
                                                    CCDBusStateString = "enabled";
                                                    break;
                                                default:
                                                    CCDBusStateString = "unknown";
                                                    break;
                                            }

                                            switch (Packet.rx.payload[2])
                                            {
                                                case (byte)Packet.OnOffMode.off:
                                                    CCDBusTerminationBiasString = "disabled";
                                                    break;
                                                case (byte)Packet.OnOffMode.on:
                                                    CCDBusTerminationBiasString = "enabled";
                                                    break;
                                                default:
                                                    CCDBusTerminationBiasString = "unknown";
                                                    break;
                                            }

                                            Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus settings changed:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus settings: " + Environment.NewLine +
                                                                           "       - state: " + CCDBusStateString + Environment.NewLine +
                                                                           "       - termination and bias: " + CCDBusTerminationBiasString);

                                            CCD.UpdateHeader(CCDBusStateString, null, null);
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus settings error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid settings packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.SettingsMode.setSCIBus:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string SCIBusPCMStateString = string.Empty;
                                            string SCIBusPCMLogicString = string.Empty;
                                            string SCIBusPCMOBDConfigurationString = string.Empty;
                                            string SCIBusPCMSpeedString = string.Empty;
                                            string SCIBusTCMStateString = string.Empty;
                                            string SCIBusTCMLogicString = string.Empty;
                                            string SCIBusTCMOBDConfigurationString = string.Empty;
                                            string SCIBusTCMSpeedString = string.Empty;

                                            if (((Packet.rx.payload[1] >> 6) & 0x03) == 0x02) // PCM settings received
                                            {
                                                if (Util.IsBitClear(Packet.rx.payload[1], 4))
                                                {
                                                    SCIBusPCMStateString = "disabled";
                                                }
                                                else
                                                {
                                                    SCIBusPCMStateString = "enabled";

                                                    if (Util.IsBitClear(Packet.rx.payload[1], 3))
                                                    {
                                                        SCIBusPCMLogicString = "non-inverted";
                                                    }
                                                    else
                                                    {
                                                        SCIBusPCMLogicString = "inverted";
                                                    }

                                                    if (Util.IsBitClear(Packet.rx.payload[1], 2))
                                                    {
                                                        SCIBusPCMOBDConfigurationString = "A";
                                                    }
                                                    else
                                                    {
                                                        SCIBusPCMOBDConfigurationString = "B";
                                                    }

                                                    switch (Packet.rx.payload[1] & 0x03)
                                                    {
                                                        case 0x00:
                                                            SCIBusPCMSpeedString = "976.5 baud";
                                                            break;
                                                        case 0x01:
                                                            SCIBusPCMSpeedString = "7812.5 baud";
                                                            break;
                                                        case 0x02:
                                                            SCIBusPCMSpeedString = "62500 baud";
                                                            break;
                                                        case 0x03:
                                                            SCIBusPCMSpeedString = "125000 baud";
                                                            break;
                                                        default:
                                                            SCIBusPCMSpeedString = "unknown";
                                                            break;
                                                    }
                                                }
                                            }
                                            else if (((Packet.rx.payload[1] >> 6) & 0x03) == 0x03) // TCM settings received
                                            {
                                                if (Util.IsBitClear(Packet.rx.payload[1], 4))
                                                {
                                                    SCIBusTCMStateString = "disabled";
                                                }
                                                else
                                                {
                                                    SCIBusTCMStateString = "enabled";

                                                    if (Util.IsBitClear(Packet.rx.payload[1], 3))
                                                    {
                                                        SCIBusTCMLogicString = "non-inverted";
                                                    }
                                                    else
                                                    {
                                                        SCIBusTCMLogicString = "inverted";
                                                    }

                                                    if (Util.IsBitClear(Packet.rx.payload[1], 2))
                                                    {
                                                        SCIBusTCMOBDConfigurationString = "A";
                                                    }
                                                    else
                                                    {
                                                        SCIBusTCMOBDConfigurationString = "B";
                                                    }

                                                    switch (Packet.rx.payload[1] & 0x03)
                                                    {
                                                        case 0x00:
                                                            SCIBusTCMSpeedString = "976.5 baud";
                                                            break;
                                                        case 0x01:
                                                            SCIBusTCMSpeedString = "7812.5 baud";
                                                            break;
                                                        case 0x02:
                                                            SCIBusTCMSpeedString = "62500 baud";
                                                            break;
                                                        case 0x03:
                                                            SCIBusTCMSpeedString = "125000 baud";
                                                            break;
                                                        default:
                                                            SCIBusTCMSpeedString = "unknown";
                                                            break;
                                                    }
                                                }
                                            }

                                            Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus settings changed:", Packet.rx.buffer);

                                            if (((Packet.rx.payload[1] >> 6) & 0x03) == 0x02) // PCM settings received
                                            {
                                                if (SCIBusPCMStateString == "enabled")
                                                {
                                                    Util.UpdateTextBox(USBTextBox, "[INFO] PCM settings: " + Environment.NewLine +
                                                                                   "       - state: " + SCIBusPCMStateString + Environment.NewLine +
                                                                                   "       - logic: " + SCIBusPCMLogicString + Environment.NewLine +
                                                                                   "       - obd config.: " + SCIBusPCMOBDConfigurationString + Environment.NewLine +
                                                                                   "       - speed: " + SCIBusPCMSpeedString);
                                                }
                                                else
                                                {
                                                    Util.UpdateTextBox(USBTextBox, "[INFO] PCM settings: " + Environment.NewLine +
                                                                                   "       - state: " + SCIBusPCMStateString);
                                                }

                                                PCM.UpdateHeader(SCIBusPCMStateString, SCIBusPCMSpeedString, SCIBusPCMLogicString, SCIBusPCMOBDConfigurationString);
                                            }
                                            else if (((Packet.rx.payload[1] >> 6) & 0x03) == 0x03) // TCM settings received
                                            {
                                                if (SCIBusTCMStateString == "enabled")
                                                {
                                                    Util.UpdateTextBox(USBTextBox, "[INFO] TCM settings: " + Environment.NewLine +
                                                                                   "       - state: " + SCIBusTCMStateString + Environment.NewLine +
                                                                                   "       - logic: " + SCIBusTCMLogicString + Environment.NewLine +
                                                                                   "       - obd config.: " + SCIBusTCMOBDConfigurationString + Environment.NewLine +
                                                                                   "       - speed: " + SCIBusTCMSpeedString);
                                                }
                                                else
                                                {
                                                    Util.UpdateTextBox(USBTextBox, "[INFO] TCM settings: " + Environment.NewLine +
                                                                                   "       - state: " + SCIBusTCMStateString);
                                                }

                                                TCM.UpdateHeader(SCIBusTCMStateString, SCIBusTCMSpeedString, SCIBusTCMLogicString, SCIBusTCMOBDConfigurationString);
                                            }

                                            SCIBusModuleComboBox_SelectedIndexChanged(this, EventArgs.Empty);
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus settings error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid settings packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.SettingsMode.setRepeatBehavior:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 5))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string bus = string.Empty;

                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.Target.ccd:
                                                    bus = "CCD-bus";
                                                    break;
                                                case (byte)Packet.Target.pcm:
                                                    bus = "SCI-bus (PCM)";
                                                    break;
                                                case (byte)Packet.Target.tcm:
                                                    bus = "SCI-bus (TCM)";
                                                    break;
                                                default:
                                                    bus = "Unknown";
                                                    break;
                                            }

                                            string repeat_interval = ((Packet.rx.payload[2] << 8) + Packet.rx.payload[3]).ToString("0") + " ms";
                                            string increment = ((Packet.rx.payload[4] << 8) + Packet.rx.payload[5]).ToString("0");

                                            Util.UpdateTextBox(USBTextBox, "[RX->] Repeated message behavior changed:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] Repeated message behavior settings:" + Environment.NewLine +
                                                                           "       Bus: " + bus + Environment.NewLine +
                                                                           "       Interval: " + repeat_interval + Environment.NewLine +
                                                                           "       Increment: " + increment);
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Repeat behavior settings error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid settings packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.SettingsMode.setLCD:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 7))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string LCDState = string.Empty;

                                            switch (Packet.rx.payload[1])
                                            {
                                                case 0x00:
                                                    LCDState = "disabled";
                                                    break;
                                                case 0x01:
                                                    LCDState = "enabled";
                                                    break;
                                                default:
                                                    LCDState = "unknown";
                                                    break;
                                            }

                                            string LCDI2CAddress = Util.ByteToHexString(Packet.rx.payload, 2, 1) + " (hex)";
                                            string LCDSize = Packet.rx.payload[3].ToString("0") + "x" + Packet.rx.payload[4].ToString("0") + " characters";
                                            string LCDRefreshRate = Packet.rx.payload[5].ToString("0") + " Hz";
                                            string LCDUnits = string.Empty;

                                            if (Packet.rx.payload[6] == 0) LCDUnits = "imperial";
                                            else if (Packet.rx.payload[6] == 1) LCDUnits = "metric";
                                            else LCDUnits = "imperial";

                                            string LCDDataSource = string.Empty;
                                            switch (Packet.rx.payload[7])
                                            {
                                                case 0x01:
                                                    LCDDataSource = "CCD-bus";
                                                    break;
                                                case 0x02:
                                                    LCDDataSource = "SCI-bus (PCM)";
                                                    break;
                                                case 0x03:
                                                    LCDDataSource = "SCI-bus (RCM)";
                                                    break;
                                                default:
                                                    LCDDataSource = "CCD-bus";
                                                    break;
                                            }

                                            Util.UpdateTextBox(USBTextBox, "[RX->] LCD settings changed:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] LCD information:" + Environment.NewLine +
                                                                           "       State: " + LCDState + Environment.NewLine +
                                                                           "       I2C address: " + LCDI2CAddress + Environment.NewLine +
                                                                           "       Size: " + LCDSize + Environment.NewLine +
                                                                           "       Refresh rate: " + LCDRefreshRate + Environment.NewLine +
                                                                           "       Units: " + LCDUnits + Environment.NewLine +
                                                                           "       Data source: " + LCDDataSource);

                                            UpdateLCDPreviewTextBox();
                                        }
                                        else
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] LCD settings error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid settings packet:", Packet.rx.buffer);
                                    }
                                    break;
                                default:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Packet received:", Packet.rx.buffer);
                                    break;
                            }
                            break;
                        case (byte)Packet.Command.request:
                            break;
                        case (byte)Packet.Command.response:
                            switch (Packet.rx.mode)
                            {
                                case (byte)Packet.ResponseMode.hardwareFirmwareInfo:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 29))
                                    {
                                        double HardwareVersion = ((Packet.rx.payload[0] << 8) + Packet.rx.payload[1]) / 100.00;
                                        string HardwareVersionString = "v" + (HardwareVersion).ToString("0.00").Replace(",", ".").Insert(3, ".");
                                        DateTime HardwareDate = Util.UnixTimeStampToDateTime((Packet.rx.payload[6] << 24) + (Packet.rx.payload[7] << 16) + (Packet.rx.payload[8] << 8) + Packet.rx.payload[9]);
                                        DateTime AssemblyDate = Util.UnixTimeStampToDateTime((Packet.rx.payload[14] << 24) + (Packet.rx.payload[15] << 16) + (Packet.rx.payload[16] << 8) + Packet.rx.payload[17]);
                                        DateTime FirmwareDate = Util.UnixTimeStampToDateTime((Packet.rx.payload[22] << 24) + (Packet.rx.payload[23] << 16) + (Packet.rx.payload[24] << 8) + Packet.rx.payload[25]);
                                        deviceFirmwareTimestamp = (UInt64)((Packet.rx.payload[22] << 24) + (Packet.rx.payload[23] << 16) + (Packet.rx.payload[24] << 8) + Packet.rx.payload[25]);
                                        string HardwareDateString = HardwareDate.ToString("yyyy.MM.dd HH:mm:ss");
                                        string AssemblyDateString = AssemblyDate.ToString("yyyy.MM.dd HH:mm:ss");
                                        string FirmwareDateString = FirmwareDate.ToString("yyyy.MM.dd HH:mm:ss");
                                        string FirmwareVersionString = "v" + Packet.rx.payload[26].ToString("0") + "." + Packet.rx.payload[27].ToString("0") + "." + Packet.rx.payload[28].ToString("0");
                                        FWVersion = FirmwareVersionString;
                                        HWVersion = HardwareVersionString;

                                        Util.UpdateTextBox(USBTextBox, "[RX->] Hardware/Firmware information response:", Packet.rx.buffer);
                                        Util.UpdateTextBox(USBTextBox, "[INFO] Hardware ver.: " + HardwareVersionString + Environment.NewLine +
                                                                       "       Firmware ver.: " + FirmwareVersionString + Environment.NewLine +
                                                                       "       Hardware date: " + HardwareDateString + Environment.NewLine +
                                                                       "       Assembly date: " + AssemblyDateString + Environment.NewLine +
                                                                       "       Firmware date: " + FirmwareDateString);
                                        if (!Text.Contains("  |  FW v"))
                                        {
                                            Text += "  |  FW " + FirmwareVersionString + "  |  HW " + HardwareVersionString;
                                        }
                                        else
                                        {
                                            Text = Text.Remove(Text.Length - 20);
                                            Text += FirmwareVersionString + "  |  HW " + HardwareVersionString;
                                        }

                                        if (Math.Round(HardwareVersion * 100) < 144) // below V1.44 the hardware doesn't support these features
                                        {
                                            MeasureCCDBusVoltagesButton.Enabled = false;
                                            CCDBusTerminationBiasOnOffCheckBox.Enabled = false;
                                        }
                                        else
                                        {
                                            MeasureCCDBusVoltagesButton.Enabled = true;
                                            CCDBusTerminationBiasOnOffCheckBox.Enabled = true;
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid HW/FW info packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.ResponseMode.timestamp:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 3))
                                    {
                                        TimeSpan elapsedTime = TimeSpan.FromMilliseconds((Packet.rx.payload[0] << 24) + (Packet.rx.payload[1] << 16) + (Packet.rx.payload[2] << 8) + Packet.rx.payload[3]);
                                        DateTime timestamp = DateTime.Today.Add(elapsedTime);
                                        string timestampString = timestamp.ToString("HH:mm:ss.fff");

                                        Util.UpdateTextBox(USBTextBox, "[RX->] Timestamp response:", Packet.rx.buffer);
                                        Util.UpdateTextBox(USBTextBox, "[INFO] Device timestamp: " + timestampString);
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid timestamp packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.ResponseMode.batteryVoltage:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        string BatteryVoltageString = (((Packet.rx.payload[0] << 8) + Packet.rx.payload[1]) / 100.00).ToString("0.00").Replace(",", ".") + " V";
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Battery voltage response:", Packet.rx.buffer);
                                        Util.UpdateTextBox(USBTextBox, "[INFO] Battery voltage: " + BatteryVoltageString);
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid battery voltage packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.ResponseMode.extEEPROMChecksum:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 2))
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM checksum response:", Packet.rx.buffer);
                                        if (Packet.rx.payload[0] == 0x01) // External EEPROM present
                                        {
                                            string ExternalEEPROMChecksumReading = Util.ByteToHexString(Packet.rx.payload, 1, 1);
                                            string ExternalEEPROMChecksumCalculated = Util.ByteToHexString(Packet.rx.payload, 2, 1);
                                            if (Packet.rx.payload[1] == Packet.rx.payload[2]) // if checksum reading and checksum calculation is the same
                                            {
                                                Util.UpdateTextBox(USBTextBox, "[INFO] External EEPROM checksum: " + ExternalEEPROMChecksumReading + "=OK.");
                                            }
                                            else
                                            {
                                                Util.UpdateTextBox(USBTextBox, "[INFO] External EEPROM checksum ERROR: " + Environment.NewLine +
                                                                               "       - reads as: " + ExternalEEPROMChecksumReading + Environment.NewLine +
                                                                               "       - calculated: " + ExternalEEPROMChecksumCalculated);
                                            }
                                        }
                                        else
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[INFO] No external EEPROM found.");
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid extEEPROM checksum packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.ResponseMode.CCDBusVoltages:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 3))
                                    {
                                        string CCDPositiveVoltage = (((Packet.rx.payload[0] << 8) + Packet.rx.payload[1]) / 100.00).ToString("0.00").Replace(",", ".") + " V";
                                        string CCDNegativeVoltage = (((Packet.rx.payload[2] << 8) + Packet.rx.payload[3]) / 100.00).ToString("0.00").Replace(",", ".") + " V";

                                        Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus voltage measurements response:", Packet.rx.buffer);
                                        Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus wire voltages:" + Environment.NewLine +
                                                                       "       CCD+: " + CCDPositiveVoltage + Environment.NewLine +
                                                                       "       CCD-: " + CCDNegativeVoltage);
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid CCD-bus voltage measurements packet:", Packet.rx.buffer);
                                    }
                                    break;
                                default:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Packet received:", Packet.rx.buffer);
                                    break;
                            }
                            break;
                        case (byte)Packet.Command.msgTx:
                            switch (Packet.rx.mode)
                            {
                                case (byte)Packet.MsgTxMode.stop:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.Source.ccd:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus repeated message Tx stopped:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.pcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) repeated message Tx stopped:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.tcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) repeated message Tx stopped:", Packet.rx.buffer);
                                                    break;
                                                default:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Unknown communication bus action:", Packet.rx.buffer);
                                                    break;
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Bus essage stop error:", Packet.rx.buffer);
                                        }
                                    }
                                    else // error
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid communication bus action:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.MsgTxMode.single:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.Source.ccd:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus message prepared for Tx:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.pcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) message prepared for Tx:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.tcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) message prepared for Tx:", Packet.rx.buffer);
                                                    break;
                                                default:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Unknown communication bus action:", Packet.rx.buffer);
                                                    break;
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Bus message prepare error:", Packet.rx.buffer);
                                        }
                                    }
                                    else // error
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid communication bus action:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.MsgTxMode.list:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.Source.ccd:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus message list prepared for Tx:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.pcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) messages list prepared for Tx:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.tcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) messages list prepared for Tx:", Packet.rx.buffer);
                                                    break;
                                                default:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Unknown communication bus action:", Packet.rx.buffer);
                                                    break;
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Bus message list prepare error:", Packet.rx.buffer);
                                        }
                                    }
                                    else // error
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid communication bus action:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.MsgTxMode.repeatedSingle:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.Source.ccd:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus repeated message Tx started:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.pcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) repeated message Tx started:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.tcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) repeated message Tx started:", Packet.rx.buffer);
                                                    break;
                                                default:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Unknown communication bus action:", Packet.rx.buffer);
                                                    break;
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Repeated message start error:", Packet.rx.buffer);
                                        }
                                    }
                                    else // error
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid communication bus action:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.MsgTxMode.repeatedList:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.Source.ccd:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus repeated message list Tx started:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.pcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) repeated message list Tx started:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.Source.tcm:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) repeated message list Tx started:", Packet.rx.buffer);
                                                    break;
                                                default:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Unknown communication bus action:", Packet.rx.buffer);
                                                    break;
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Repeated message start error:", Packet.rx.buffer);
                                        }
                                    }
                                    else // error
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid communication bus action:", Packet.rx.buffer);
                                    }
                                    break;
                                default:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Packet received:", Packet.rx.buffer);
                                    break;
                            }
                            break;
                        case (byte)Packet.Command.msgRx:
                            switch (Packet.rx.mode)
                            {
                                case (byte)Packet.MsgRxMode.stop:
                                    break;
                                case (byte)Packet.MsgRxMode.single:
                                    break;
                                case (byte)Packet.MsgRxMode.list:
                                    break;
                                case (byte)Packet.MsgRxMode.repeatedSingle:
                                    break;
                                case (byte)Packet.MsgRxMode.repeatedList:
                                    break;
                                default:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Packet received:", Packet.rx.buffer);
                                    break;
                            }
                            break;
                        case (byte)Packet.Command.debug:
                            switch (Packet.rx.mode)
                            {
                                case (byte)Packet.DebugMode.randomCCDBusMessages:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            switch (Packet.rx.payload[1])
                                            {
                                                case (byte)Packet.OnOffMode.on:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Random CCD-bus messages started:", Packet.rx.buffer);
                                                    break;
                                                case (byte)Packet.OnOffMode.off:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Random CCD-bus messages stopped:", Packet.rx.buffer);
                                                    break;
                                                default:
                                                    Util.UpdateTextBox(USBTextBox, "[RX->] Unknown debug packet:", Packet.rx.buffer);
                                                    break;
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Random CCD-bus messages error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.readIntEEPROMbyte:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 3))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string value = Util.ByteToHexString(Packet.rx.payload, 3, 1);

                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM byte read response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] Internal EEPROM byte information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Value: " + value);
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM byte read error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.readIntEEPROMblock:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 4))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            int start = (Packet.rx.payload[1] << 8) + Packet.rx.payload[2];
                                            int length = Packet.rx.payload.Length - 3;
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string values = Util.ByteToHexString(Packet.rx.payload, 3, Packet.rx.payload.Length - 3);
                                            string count = length.ToString();

                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM block read response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] Internal EEPROM block information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Count: " + count + Environment.NewLine + values);

                                            if ((start == 0) && (length == 256))
                                            {
                                                double HardwareVersion = ((Packet.rx.payload[3] << 8) + Packet.rx.payload[4]) / 100.00;
                                                string HardwareVersionString = "v" + (HardwareVersion).ToString("0.00").Replace(",", ".").Insert(3, ".");
                                                DateTime HardwareDate = Util.UnixTimeStampToDateTime((Packet.rx.payload[9] << 24) + (Packet.rx.payload[10] << 16) + (Packet.rx.payload[11] << 8) + Packet.rx.payload[12]);
                                                DateTime AssemblyDate = Util.UnixTimeStampToDateTime((Packet.rx.payload[17] << 24) + (Packet.rx.payload[18] << 16) + (Packet.rx.payload[19] << 8) + Packet.rx.payload[20]);
                                                string HardwareDateString = HardwareDate.ToString("yyyy.MM.dd HH:mm:ss");
                                                string AssemblyDateString = AssemblyDate.ToString("yyyy.MM.dd HH:mm:ss");
                                                double ADCVoltage = ((Packet.rx.payload[21] << 8) + Packet.rx.payload[22]) / 100.00;
                                                string ADCVoltageString = ADCVoltage.ToString("0.00").Replace(",", ".") + " V";
                                                double RDHighResistance = ((Packet.rx.payload[23] << 8) + Packet.rx.payload[24]) / 1000.0;
                                                double RDLowResistance = ((Packet.rx.payload[25] << 8) + Packet.rx.payload[26]) / 1000.0;
                                                string RDHighResistanceString = RDHighResistance.ToString("0.000").Replace(",", ".") + " kΩ";
                                                string RDLowResistanceString = RDLowResistance.ToString("0.000").Replace(",", ".") + " kΩ";
                                                string LCDStateString = string.Empty;

                                                if (Util.IsBitSet(Packet.rx.payload[27], 0)) LCDStateString = "enabled";
                                                else LCDStateString = "disabled";

                                                string LCDI2CAddressString = Util.ByteToHexString(Packet.rx.payload, 28, 1) + " (hex)";
                                                string LCDWidthString = Packet.rx.payload[29].ToString("0") + " characters";
                                                string LCDHeightString = Packet.rx.payload[30].ToString("0") + " characters";
                                                string LCDRefreshRateString = Packet.rx.payload[31].ToString("0") + " Hz";
                                                string LCDUnitsString = string.Empty;
                                                string LCDDataSourceString = string.Empty;

                                                if (Packet.rx.payload[32] == 0x00) LCDUnitsString = "imperial";
                                                else if (Packet.rx.payload[32] == 0x01) LCDUnitsString = "metric";
                                                else LCDUnitsString = "imperial";

                                                switch (Packet.rx.payload[33])
                                                {
                                                    case 0x01:
                                                        LCDDataSourceString = "CCD-bus";
                                                        break;
                                                    case 0x02:
                                                        LCDDataSourceString = "SCI-bus (PCM)";
                                                        break;
                                                    case 0x03:
                                                        LCDDataSourceString = "SCI-bus (TCM)";
                                                        break;
                                                    default:
                                                        LCDDataSourceString = "CCD-bus";
                                                        break;
                                                }

                                                string LEDHeartbeatInterval = ((Packet.rx.payload[34] << 8) + Packet.rx.payload[35]).ToString() + " ms";
                                                string LEDBlinkDuration = ((Packet.rx.payload[36] << 8) + Packet.rx.payload[37]).ToString() + " ms";

                                                Util.UpdateTextBox(USBTextBox, "[INFO] External EEPROM settings:" + Environment.NewLine +
                                                                               "       Hardware ver.: " + Util.ByteToHexString(Packet.rx.payload, 3, 2) + " | " + HardwareVersionString + Environment.NewLine +
                                                                               "       Hardware date: " + Util.ByteToHexString(Packet.rx.payload, 5, 8) + " | " + Environment.NewLine +
                                                                               "                      " + HardwareDateString + Environment.NewLine +
                                                                               "       Assembly date: " + Util.ByteToHexString(Packet.rx.payload, 13, 8) + " | " + Environment.NewLine +
                                                                               "                      " + AssemblyDateString + Environment.NewLine +
                                                                               "       ADC supply:    " + Util.ByteToHexString(Packet.rx.payload, 21, 2) + " | " + ADCVoltageString + Environment.NewLine +
                                                                               "       RDH resistor:  " + Util.ByteToHexString(Packet.rx.payload, 23, 2) + " | " + RDHighResistanceString + Environment.NewLine +
                                                                               "       RDL resistor:  " + Util.ByteToHexString(Packet.rx.payload, 25, 2) + " | " + RDLowResistanceString + Environment.NewLine +
                                                                               "       LCD state:        " + Util.ByteToHexString(Packet.rx.payload, 27, 1) + " | " + LCDStateString + Environment.NewLine +
                                                                               "       LCD I2C addr.:    " + Util.ByteToHexString(Packet.rx.payload, 28, 1) + " | " + LCDI2CAddressString + Environment.NewLine +
                                                                               "       LCD width:        " + Util.ByteToHexString(Packet.rx.payload, 29, 1) + " | " + LCDWidthString + Environment.NewLine +
                                                                               "       LCD height:       " + Util.ByteToHexString(Packet.rx.payload, 30, 1) + " | " + LCDHeightString + Environment.NewLine +
                                                                               "       LCD refresh:      " + Util.ByteToHexString(Packet.rx.payload, 31, 1) + " | " + LCDRefreshRateString + Environment.NewLine +
                                                                               "       LCD units:        " + Util.ByteToHexString(Packet.rx.payload, 32, 1) + " | " + LCDUnitsString + Environment.NewLine +
                                                                               "       LCD data src:     " + Util.ByteToHexString(Packet.rx.payload, 33, 1) + " | " + LCDDataSourceString + Environment.NewLine +
                                                                               "       LED heartbeat: " + Util.ByteToHexString(Packet.rx.payload, 34, 2) + " | " + LEDHeartbeatInterval + Environment.NewLine +
                                                                               "       LED blink:     " + Util.ByteToHexString(Packet.rx.payload, 36, 2) + " | " + LEDBlinkDuration + Environment.NewLine +
                                                                               "       CCD settings:     " + Util.ByteToHexString(Packet.rx.payload, 38, 1) + Environment.NewLine +
                                                                               "       PCM settings:     " + Util.ByteToHexString(Packet.rx.payload, 39, 1) + Environment.NewLine +
                                                                               "       TCM settings:     " + Util.ByteToHexString(Packet.rx.payload, 40, 1));
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM block read error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.readExtEEPROMbyte:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 3))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string value = Util.ByteToHexString(Packet.rx.payload, 3, 1);

                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM byte read response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] External EEPROM byte information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Value: " + value);
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM byte read error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.readExtEEPROMblock:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 4))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            int start = (Packet.rx.payload[1] << 8) + Packet.rx.payload[2];
                                            int length = Packet.rx.payload.Length - 3;
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string values = Util.ByteToHexString(Packet.rx.payload, 3, Packet.rx.payload.Length - 3);
                                            string count = length.ToString();

                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM block read response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] External EEPROM block information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Count: " + count + Environment.NewLine + values);
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM block read error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.writeIntEEPROMbyte:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 3))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string value = Util.ByteToHexString(Packet.rx.payload, 3, 1);

                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM byte write response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] Internal EEPROM byte information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Value: " + value);

                                            byte writtenByte = Util.HexStringToByte(EEPROMWriteValuesTextBox.Text)[0];
                                            byte readByte = Packet.rx.payload[3];

                                            if (readByte == writtenByte)
                                            {
                                                MessageBox.Show("Internal EEPROM byte write successful!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                            }
                                            else
                                            {
                                                MessageBox.Show("Internal EEPROM byte write failed!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM byte write error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.writeIntEEPROMblock:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 4))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string values = Util.ByteToHexString(Packet.rx.payload, 3, Packet.rx.payload.Length - 3);
                                            string count = (Packet.rx.payload.Length - 3).ToString();

                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM block write response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] Internal EEPROM block information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Count: " + count + Environment.NewLine + values);

                                            byte[] writtenBytes = Util.HexStringToByte(EEPROMWriteValuesTextBox.Text);
                                            byte[] readBytes = new byte[writtenBytes.Length];
                                            Array.Copy(Packet.rx.payload, 3, readBytes, 0, Packet.rx.payload.Length - 3);

                                            if (Util.CompareArrays(writtenBytes, readBytes, 0, writtenBytes.Length))
                                            {
                                                MessageBox.Show("Internal EEPROM block write successful!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                            }
                                            else
                                            {
                                                MessageBox.Show("Internal EEPROM block write failed!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] Internal EEPROM block write error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.writeExtEEPROMbyte:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 3))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string value = Util.ByteToHexString(Packet.rx.payload, 3, 1);

                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM byte write response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] External EEPROM byte information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Value: " + value);

                                            byte writtenByte = Util.HexStringToByte(EEPROMWriteValuesTextBox.Text)[0];
                                            byte readByte = Packet.rx.payload[3];

                                            if (readByte == writtenByte)
                                            {
                                                MessageBox.Show("External EEPROM byte write successful!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                            }
                                            else
                                            {
                                                MessageBox.Show("External EEPROM byte write failed!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM byte write error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.writeExtEEPROMblock:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 4))
                                    {
                                        if (Packet.rx.payload[0] == 0x00) // OK
                                        {
                                            string offset = Util.ByteToHexString(Packet.rx.payload, 1, 2);
                                            string values = Util.ByteToHexString(Packet.rx.payload, 3, Packet.rx.payload.Length - 3);
                                            string count = (Packet.rx.payload.Length - 3).ToString();

                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM block write response:", Packet.rx.buffer);
                                            Util.UpdateTextBox(USBTextBox, "[INFO] External EEPROM block information:" + Environment.NewLine +
                                                                           "       Offset: " + offset + " | Count: " + count + Environment.NewLine + values);

                                            byte[] writtenBytes = Util.HexStringToByte(EEPROMWriteValuesTextBox.Text);
                                            byte[] readBytes = new byte[writtenBytes.Length];
                                            Array.Copy(Packet.rx.payload, 3, readBytes, 0, Packet.rx.payload.Length - 3);

                                            if (Util.CompareArrays(writtenBytes, readBytes, 0, writtenBytes.Length))
                                            {
                                                MessageBox.Show("External EEPROM block write successful!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                            }
                                            else
                                            {
                                                MessageBox.Show("External EEPROM block write failed!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            }
                                        }
                                        else // error
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] External EEPROM block write error:", Packet.rx.buffer);
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.DebugMode.setArbitraryUARTSpeed:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        switch (Packet.rx.payload[0])
                                        {
                                            case (byte)Packet.Source.ccd:
                                                switch (Packet.rx.payload[1])
                                                {
                                                    case (byte)Packet.BaudMode.extraLowBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus speed: 976.5 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.lowBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus speed: 7812.5 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.highBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus speed: 62500 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.extraHighBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus speed: 125000 baud");
                                                        break;
                                                    default:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus speed unchanged:", Packet.rx.buffer);
                                                        break;
                                                }
                                                break;
                                            case (byte)Packet.Source.pcm:
                                                switch (Packet.rx.payload[1])
                                                {
                                                    case (byte)Packet.BaudMode.extraLowBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) speed: 976.5 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.lowBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) speed: 7812.5 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.highBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) speed: 62500 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.extraHighBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) speed: 125000 baud");
                                                        break;
                                                    default:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) speed unchanged:", Packet.rx.buffer);
                                                        break;
                                                }
                                                break;
                                            case (byte)Packet.Source.tcm:
                                                switch (Packet.rx.payload[1])
                                                {
                                                    case (byte)Packet.BaudMode.extraLowBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) speed: 976.5 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.lowBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) speed: 7812.5 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.highBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) speed: 62500 baud");
                                                        break;
                                                    case (byte)Packet.BaudMode.extraHighBaud:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) speed changed:", Packet.rx.buffer);
                                                        Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) speed: 125000 baud");
                                                        break;
                                                    default:
                                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) speed unchanged:", Packet.rx.buffer);
                                                        break;
                                                }
                                                break;
                                            default:
                                                Util.UpdateTextBox(USBTextBox, "[RX->] Unknown baudrate change request:", Packet.rx.buffer);
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid debug packet:", Packet.rx.buffer);
                                    }
                                    break;
                                default:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Packet received:", Packet.rx.buffer);
                                    break;
                            }
                            break;
                        case (byte)Packet.Command.error:
                            switch (Packet.rx.mode)
                            {
                                case (byte)Packet.ErrorMode.ok:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] OK:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorLengthInvalidValue:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, invalid packet length:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorDatacodeInvalidCommand:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, invalid dc command:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorSubDatacodeInvalidValue:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, invalid sub-data code:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorPayloadInvalidValues:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, invalid payload value(s):", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorPacketChecksumInvalidValue:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, invalid checksum:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorPacketTimeoutOccured:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, packet timeout occured:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorBufferOverflow:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, buffer overflow:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorSCILsNoResponse:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, no response from SCI-bus:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorNotEnoughMCURAM:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, not enough MCU RAM:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorSCIHsMemoryPtrNoResponse:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 0))
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Error, SCI-bus RAM-table no response (" + Util.ByteToHexString(Packet.rx.payload, 0, 1) + "):", Packet.rx.buffer);
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Error, SCI-bus RAM-table no response:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.ErrorMode.errorSCIHsInvalidMemoryPtr:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 0))
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Error, SCI-bus RAM-table invalid (" + Util.ByteToHexString(Packet.rx.payload, 0, 1) + "):", Packet.rx.buffer);
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Error, SCI-bus RAM-table invalid:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.ErrorMode.errorSCIHsNoResponse:
                                    if ((Packet.rx.payload != null) && (Packet.rx.payload.Length > 1))
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Error, no response from SCI-bus (" + Util.ByteToHexString(Packet.rx.payload, 0, 2) + "):", Packet.rx.buffer);
                                    }
                                    else
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Error, no response from SCI-bus:", Packet.rx.buffer);
                                    }
                                    break;
                                case (byte)Packet.ErrorMode.errorEEPNotFound:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, external EEPROM not found:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorEEPRead:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, external EEPROM read failure:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorEEPWrite:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, external EEPROM write failure:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorInternal:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, internal error:", Packet.rx.buffer);
                                    break;
                                case (byte)Packet.ErrorMode.errorFatal:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error, fatal error:", Packet.rx.buffer);
                                    break;
                                default:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] Error packet received:", Packet.rx.buffer);
                                    break;
                            }
                            break;
                        default:
                            Util.UpdateTextBox(USBTextBox, "[RX->] Packet received:", Packet.rx.buffer);
                            break;
                    }
                    break;
                case (byte)Packet.Source.ccd:
                    if (CCDBusOnDemandToolStripMenuItem.Checked && (DeviceCCDSCILCDTabControl.SelectedTab.Name == "CCDBusControlTabPage") || !CCDBusOnDemandToolStripMenuItem.Checked)
                    {
                        Util.UpdateTextBox(USBTextBox, "[RX->] CCD-bus message:", Packet.rx.buffer);
                        CCD.AddMessage(Packet.rx.payload.ToArray());
                    }
                    break;
                case (byte)Packet.Source.pcm:
                    switch (Packet.rx.mode)
                    {
                        case (byte)Packet.SCISpeedMode.lowSpeed:
                            switch (Packet.rx.payload[4]) // ID byte
                            {
                                case 0x10: // fault code list request
                                    if (Packet.rx.payload.Length > 6)
                                    {
                                        byte checksum = 0;
                                        int checksumLocation = Packet.rx.payload.Length - 1;

                                        for (int i = 4; i < checksumLocation; i++)
                                        {
                                            checksum += Packet.rx.payload[i];
                                        }

                                        if (checksum == Packet.rx.payload[checksumLocation])
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) fault code list:", Packet.rx.buffer);

                                            List<byte> faultCodeList = new List<byte>();
                                            faultCodeList.AddRange(Packet.rx.payload.Skip(4).Take(Packet.rx.payload.Length - 5)); // skip first 4 bytes (timestamp)
                                            faultCodeList.Remove(0x10); // message ID byte is not part of the fault code list
                                            faultCodeList.Remove(0xFD); // not fault code related
                                            faultCodeList.Remove(0xFE); // end of fault code list signifier

                                            if (faultCodeList.Count > 0)
                                            {
                                                StringBuilder sb = new StringBuilder();

                                                foreach (byte code in faultCodeList)
                                                {
                                                    int index = PCM.EngineDTC.Rows.IndexOf(PCM.EngineDTC.Rows.Find(code));
                                                    byte[] temp = new byte[1] { code };

                                                    if (index > -1) // DTC description found
                                                    {
                                                        sb.Append(Util.ByteToHexStringSimple(temp) + ": " + PCM.EngineDTC.Rows[index]["description"] + Environment.NewLine);
                                                    }
                                                    else // no DTC description found
                                                    {
                                                        sb.Append(Util.ByteToHexStringSimple(temp) + ": -" + Environment.NewLine);
                                                    }
                                                }

                                                sb.Remove(sb.Length - 2, 2); // remove last newline character

                                                Util.UpdateTextBox(USBTextBox, "[INFO] PCM fault code(s) found:" + Environment.NewLine + sb.ToString());
                                            }
                                            else
                                            {
                                                Util.UpdateTextBox(USBTextBox, "[INFO] No PCM fault code found.");
                                            }
                                        }
                                        else
                                        {
                                            Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) fault code checksum error:", Packet.rx.buffer);
                                        }
                                    }
                                    else // error
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid SCI-bus (PCM) fault code list:", Packet.rx.buffer);
                                    }
                                    break;
                                case 0x17: // erase fault codes
                                    if (Packet.rx.payload.Length > 5)
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) erase fault code list:", Packet.rx.buffer);

                                        if (Packet.rx.payload[5] == 0xE0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) erase fault code list success.");
                                        else Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) erase fault code list error.");
                                    }
                                    else // error
                                    {
                                        Util.UpdateTextBox(USBTextBox, "[RX->] Invalid SCI-bus (PCM) erase fault code list response:", Packet.rx.buffer);
                                    }
                                    break;
                                default:
                                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) low-speed message:", Packet.rx.buffer);
                                    break;
                            }
                            break;
                        case (byte)Packet.SCISpeedMode.highSpeed:
                            Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) high-speed message:", Packet.rx.buffer);
                            break;
                        default:
                            Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (PCM) message:", Packet.rx.buffer);
                            break;
                    }
                    PCM.AddMessage(Packet.rx.payload.ToArray());
                    break;
                case (byte)Packet.Source.tcm:
                    Util.UpdateTextBox(USBTextBox, "[RX->] SCI-bus (TCM) message:", Packet.rx.buffer);
                    TCM.AddMessage(Packet.rx.payload.ToArray());
                    break;
                default:
                    Util.UpdateTextBox(USBTextBox, "[RX->] Packet received:", Packet.rx.buffer);
                    break;
            }
        }

        private void UpdateCCDTable(object sender, EventArgs e)
        {
            if (CCDBusDiagnosticsListBox.Items.Count == 0)
            {
                CCDBusDiagnosticsListBox.Items.AddRange(CCD.Diagnostics.Table.ToArray());
                return;
            }

            // Add current line to the buffer.
            CCDTableBuffer.Add(CCD.Diagnostics.Table[CCD.Diagnostics.lastUpdatedLine]);
            CCDTableBufferLocation.Add(CCD.Diagnostics.lastUpdatedLine);
            CCDTableRowCountHistory.Add(CCD.Diagnostics.Table.Count);

            //if (CCD.Diagnostics.lastUpdatedLine == CCD.Diagnostics.B2Row)
            //{
            //    CCDTableBuffer.Add(CCD.Diagnostics.Table[CCD.Diagnostics.F2Row]);
            //    CCDTableBufferLocation.Add(CCD.Diagnostics.F2Row);
            //    CCDTableRowCountHistory.Add(CCD.Diagnostics.Table.Count);
            //}
        }

        private void UpdateSCIPCMTable(object sender, EventArgs e)
        {
            if (SCIBusPCMDiagnosticsListBox.Items.Count == 0)
            {
                SCIBusPCMDiagnosticsListBox.Items.AddRange(PCM.Diagnostics.Table.ToArray());
                return;
            }

            // Add current line to the buffer.
            PCMTableBuffer.Add(PCM.Diagnostics.Table[PCM.Diagnostics.lastUpdatedLine]);
            PCMTableBufferLocation.Add(PCM.Diagnostics.lastUpdatedLine);
            PCMTableRowCountHistory.Add(PCM.Diagnostics.Table.Count);

            // Add visible RAM-table to the buffer.
            if ((PCM.speed == "62500 baud") && PCM.Diagnostics.RAMDumpTableVisible)
            {
                for (int i = 0; i < 23; i++) // RAM-table has 22 lines + 1 empty line below main table
                {
                    PCMTableBuffer.Add(PCM.Diagnostics.Table[PCM.Diagnostics.Table.Count - 23 + i]);
                    PCMTableBufferLocation.Add(PCM.Diagnostics.Table.Count - 23 + i);
                    PCMTableRowCountHistory.Add(PCM.Diagnostics.Table.Count);
                }
            }
        }

        private void UpdateSCITCMTable(object sender, EventArgs e)
        {
            if (SCIBusTCMDiagnosticsListBox.Items.Count == 0)
            {
                SCIBusTCMDiagnosticsListBox.Items.AddRange(TCM.Diagnostics.Table.ToArray());
                return;
            }

            // Add current line to the buffer.
            TCMTableBuffer.Add(TCM.Diagnostics.Table[TCM.Diagnostics.lastUpdatedLine]);
            TCMTableBufferLocation.Add(TCM.Diagnostics.lastUpdatedLine);
            TCMTableRowCountHistory.Add(TCM.Diagnostics.Table.Count);
        }

        public async void TransmitUSBPacket(string description)
        {
            if (!USBSendPacketComboBox.Items.Contains(USBSendPacketComboBox.Text)) // only add unique items (no repeat!)
            {
                USBSendPacketComboBox.Items.Add(USBSendPacketComboBox.Text); // add command to the list so it can be selected later
            }

            Util.UpdateTextBox(USBTextBox, description, Packet.tx.buffer); // Packet class fields must be previously filled with data
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        #endregion

        #region USB communication

        private async void USBSendPacketButton_Click(object sender, EventArgs e)
        {
            if (USBSendPacketComboBox.Text != string.Empty)
            {
                byte[] bytes = Util.HexStringToByte(USBSendPacketComboBox.Text);
                if ((bytes != null) && (bytes.Length > 5))
                {
                    if (!USBSendPacketComboBox.Items.Contains(USBSendPacketComboBox.Text)) // only add unique items (no repeat!)
                    {
                        USBSendPacketComboBox.Items.Add(USBSendPacketComboBox.Text); // add command to the list so it can be selected later
                    }

                    Util.UpdateTextBox(USBTextBox, "[<-TX] Data transmitted:", bytes);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, bytes);
                }
                else
                {
                    MessageBox.Show("At least 6 bytes are necessary for a valid packet!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void USBSendPacketComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                USBSendPacketButton_Click(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Control panel

        private void COMPortsRefreshButton_Click(object sender, EventArgs e)
        {
            UpdateCOMPortList();
        }

        private void COMPortsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedPort = COMPortsComboBox.Text;
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            UpdateCOMPortList();

            if ((COMPortsComboBox.Text != "N/A") && (COMPortsComboBox.Text != string.Empty))
            {
                if (ConnectButton.Text == "Connect")
                {
                    byte[] buffer = new byte[Packet.expectedHandshake.Length];
                    byte ConnectionCounter = 0;

                    ConnectButton.Enabled = false; // no double-click
                    COMPortsRefreshButton.Enabled = false;

                    while (ConnectionCounter < 5) // try connecting to the scanner 5 times before giving up
                    {
                        Thread.Sleep(1);

                        if (Packet.Serial.IsOpen) Packet.Serial.Close(); // can't overwrite fields if serial port is open
                        Packet.Serial.PortName = COMPortsComboBox.Text;
                        Packet.Serial.BaudRate = 250000;
                        Packet.Serial.DataBits = 8;
                        Packet.Serial.StopBits = StopBits.One;
                        Packet.Serial.Parity = Parity.None;
                        Packet.Serial.ReadTimeout = 500;
                        Packet.Serial.WriteTimeout = 500;

                        Util.UpdateTextBox(USBTextBox, "[INFO] Connecting to " + Packet.Serial.PortName + ".");

                        try
                        {
                            Packet.Serial.Open(); // open current serial port
                        }
                        catch
                        {
                            Util.UpdateTextBox(USBTextBox, "[INFO] " + Packet.Serial.PortName + " is opened by another application.");
                            Util.UpdateTextBox(USBTextBox, "[INFO] Device not found on " + Packet.Serial.PortName + ".");
                            break;
                        }

                        if (Packet.Serial.IsOpen)
                        {
                            //Packet.tx.source = (byte)Packet.Source.device;
                            //Packet.tx.target = (byte)Packet.Target.device;
                            //Packet.tx.command = (byte)Packet.Command.handshake;
                            //Packet.tx.mode = (byte)Packet.HandshakeMode.handshakeOnly;
                            //Packet.tx.payload = null;
                            //Packet.GeneratePacket();
                            //Util.UpdateTextBox(USBTextBox, "[<-TX] Handshake request (" + Packet.Serial.PortName + "):", Packet.tx.buffer);
                            //await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                            //TimeoutTimer.Start();

                            while (!timeout && !deviceFound)
                            {
                                Thread.Sleep(1);
                                
                                //if (Packet.Serial.BytesToRead == Packet.expectedHandshake.Length)
                                //{
                                //    try
                                //    {
                                //        Packet.Serial.Read(buffer, 0, Packet.expectedHandshake.Length);
                                //    }
                                //    catch
                                //    {
                                //        Util.UpdateTextBox(USBTextBox, "[INFO] Cannot read enough bytes from " + Packet.Serial.PortName + ".");
                                //        break;
                                //    }

                                //    string expectedHandshake = "CHRYSLERCCDSCISCANNER";
                                //    string receivedHandshake = Encoding.ASCII.GetString(buffer, 5, 21);
                                //    Util.UpdateTextBox(USBTextBox, "[RX->] Handshake response:", buffer);

                                //    if (receivedHandshake == expectedHandshake)
                                //    {
                                        //TimeoutTimer.Stop();
                                        //timeout = false;
                                        //Util.UpdateTextBox(USBTextBox, "[INFO] Handshake OK: " + receivedHandshake);
                                        Util.UpdateTextBox(USBTextBox, "[INFO] Device connected (" + Packet.Serial.PortName + ").");
                                        deviceFound = true;
                                        ConnectButton.Text = "Disconnect";
                                        COMPortsComboBox.Enabled = false;
                                        COMPortsRefreshButton.Enabled = false;
                                        USBCommunicationGroupBox.Enabled = true;
                                        DeviceCCDSCILCDTabControl.Enabled = true;
                                        DiagnosticsGroupBox.Enabled = true;
                                        ReadMemoryToolStripMenuItem.Enabled = true;
                                        WriteMemoryToolStripMenuItem.Enabled = true;
                                        Packet.PacketReceived += AnalyzePacket; // subscribe to the OnPacketReceived event
                                        CCD.Diagnostics.TableUpdated += UpdateCCDTable; // subscribe to the CCD-bus OnTableUpdated event
                                        PCM.Diagnostics.TableUpdated += UpdateSCIPCMTable; // subscribe to the SCI-bus (PCM) OnTableUpdated event
                                        TCM.Diagnostics.TableUpdated += UpdateSCITCMTable; // subscribe to the SCI-bus (TCM) OnTableUpdated event
                                        Packet.MonitorSerialPort();
                                        VersionInfoButton_Click(this, EventArgs.Empty);
                                        StatusButton_Click(this, EventArgs.Empty);
                                        //UpdateToolStripMenuItem_Click(this, EventArgs.Empty); // check for updates
                                //    }
                                //    else
                                //    {
                                //        Util.UpdateTextBox(USBTextBox, "[INFO] Handshake ERROR: " + receivedHandshake);
                                //    }
                                //}
                            }

                            //if (timeout)
                            //{
                            //    TimeoutTimer.Stop();
                            //    timeout = false;
                            //    Packet.Serial.Close();
                            //    ConnectionCounter++; // increase counter value and try again
                            //    Util.UpdateTextBox(USBTextBox, "[INFO] Device is not responding on " + Packet.Serial.PortName + ".");
                            //}

                            if (deviceFound) break;
                        }
                    }

                    ConnectButton.Enabled = true;

                    if (!deviceFound)
                    {
                        COMPortsRefreshButton.Enabled = true;
                        COMPortsComboBox.Enabled = true;
                    }

                }
                else if (ConnectButton.Text == "Disconnect")
                {
                    if (Packet.Serial.IsOpen)
                    {
                        Packet.Serial.DiscardInBuffer();
                        Packet.Serial.DiscardOutBuffer();
                        Packet.Serial.BaseStream.Flush();
                        Packet.Serial.Close();
                        Packet.PacketReceived -= AnalyzePacket; // unsubscribe from the OnPacketReceived event
                        CCD.Diagnostics.TableUpdated -= UpdateCCDTable; // unsubscribe from the CCD-bus OnTableUpdated event
                        PCM.Diagnostics.TableUpdated -= UpdateSCIPCMTable; // unsubscribe from the SCI-bus (PCM) OnTableUpdated event
                        TCM.Diagnostics.TableUpdated -= UpdateSCITCMTable; // unsubscribe from the SCI-bus (PCM) OnTableUpdated event
                        ConnectButton.Text = "Connect";
                        COMPortsComboBox.Enabled = true;
                        COMPortsRefreshButton.Enabled = true;
                        USBCommunicationGroupBox.Enabled = false;
                        DeviceCCDSCILCDTabControl.Enabled = false;
                        DiagnosticsGroupBox.Enabled = false;
                        ReadMemoryToolStripMenuItem.Enabled = false;
                        WriteMemoryToolStripMenuItem.Enabled = false;
                        deviceFound = false;
                        timeout = false;
                        Util.UpdateTextBox(USBTextBox, "[INFO] Device disconnected (" + Packet.Serial.PortName + ").");
                        if (ReadMemory != null) ReadMemory.Close();
                        Text = "Chrysler CCD/SCI Scanner  |  GUI " + GUIVersion;
                    }
                }
            }
        }

        private void ExpandButton_Click(object sender, EventArgs e)
        {
            if (ExpandButton.Text == "Expand >>")
            {
                DiagnosticsGroupBox.Visible = true;
                Size = new Size(1300, 650);
                CenterToScreen();
                ExpandButton.Text = "<< Collapse";
            }
            else if (ExpandButton.Text == "<< Collapse")
            {
                Size = new Size(405, 650);
                CenterToScreen();
                DiagnosticsGroupBox.Visible = false;
                ExpandButton.Text = "Expand >>";
            }
        }

        #endregion

        #region Device tab 

        private async void ResetButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.reset;
            Packet.tx.mode = (byte)Packet.ResetMode.resetInit;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Reset device:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void HandshakeButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.handshake;
            Packet.tx.mode = (byte)Packet.HandshakeMode.handshakeOnly;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Handshake request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void StatusButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.status;
            Packet.tx.mode = (byte)Packet.StatusMode.none;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Status request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void VersionInfoButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.request;
            Packet.tx.mode = (byte)Packet.RequestMode.hardwareFirmwareInfo;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Hardware/Firmware information request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void TimestampButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.request;
            Packet.tx.mode = (byte)Packet.RequestMode.timestamp;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Timestamp request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void BatteryVoltageButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.request;
            Packet.tx.mode = (byte)Packet.RequestMode.batteryVoltage;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Battery voltage request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void EEPROMChecksumButton_Click(object sender, EventArgs e)
        {
            if (ExternalEEPROMRadioButton.Checked)
            {
                Packet.tx.source = (byte)Packet.Source.device;
                Packet.tx.target = (byte)Packet.Target.device;
                Packet.tx.command = (byte)Packet.Command.request;
                Packet.tx.mode = (byte)Packet.RequestMode.extEEPROMChecksum;
                Packet.tx.payload = null;
                Packet.GeneratePacket();
                Util.UpdateTextBox(USBTextBox, "[<-TX] External EEPROM checksum request:", Packet.tx.buffer);
                await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
            }
            else if (InternalEEPROMRadioButton.Checked)
            {
                MessageBox.Show("The internal EEPROM has no assigned checksum!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void ReadEEPROMButton_Click(object sender, EventArgs e)
        {
            byte[] address = Util.HexStringToByte(EEPROMReadAddressTextBox.Text);

            if (address.Length != 2)
            {
                MessageBox.Show("Read address needs to be 2 bytes long!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (address.Length == 0) return;

            int offset = (address[0] << 8) + address[1];
            bool success = int.TryParse(EEPROMReadCountTextBox.Text, out int readCount);

            if (!success)
            {
                EEPROMReadCountTextBox.Text = "1";
                readCount = 1;
            }

            byte[] readCountBytes = new byte[] { (byte)(readCount >> 8), (byte)(readCount & 0xFF) };

            if (InternalEEPROMRadioButton.Checked)
            {
                if (readCount == 1)
                {
                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.readIntEEPROMbyte;
                    Packet.tx.payload = address;
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Internal EEPROM byte read request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if (offset > (intEEPROMsize - 1))
                    {
                        MessageBox.Show("Internal EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (readCount > 1)
                {
                    List<byte> payloadList = new List<byte>();
                    payloadList.AddRange(address);
                    payloadList.AddRange(readCountBytes);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.readIntEEPROMblock;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Internal EEPROM block read request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if ((offset + (readCount - 1)) > (intEEPROMsize - 1))
                    {
                        MessageBox.Show("Internal EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Count value cannot be 0!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else if (ExternalEEPROMRadioButton.Checked)
            {
                if (readCount == 1)
                {
                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.readExtEEPROMbyte;
                    Packet.tx.payload = address;
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] External EEPROM byte read request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if (offset > (extEEPROMsize - 1))
                    {
                        MessageBox.Show("External EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (readCount > 1)
                {
                    List<byte> payloadList = new List<byte>();
                    payloadList.AddRange(address);
                    payloadList.AddRange(readCountBytes);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.readExtEEPROMblock;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] External EEPROM block read request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if ((offset + (readCount - 1)) > (extEEPROMsize - 1))
                    {
                        MessageBox.Show("External EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Count value cannot be 0!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private async void WriteEEPROMButton_Click(object sender, EventArgs e)
        {
            byte[] address = Util.HexStringToByte(EEPROMWriteAddressTextBox.Text);

            if (address.Length != 2)
            {
                MessageBox.Show("Write address needs to be 2 bytes long!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (address.Length == 0) return;

            int offset = (address[0] << 8) + address[1];
            byte[] values = Util.HexStringToByte(EEPROMWriteValuesTextBox.Text);
            if (values.Length == 0) return;
            int writeCount = values.Length;

            if (InternalEEPROMRadioButton.Checked)
            {
                if (writeCount == 1)
                {
                    List<byte> payloadList = new List<byte>();
                    payloadList.AddRange(address);
                    payloadList.Add(values[0]);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.writeIntEEPROMbyte;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Internal EEPROM byte write request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if (offset > (intEEPROMsize - 1))
                    {
                        MessageBox.Show("Internal EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (writeCount > 1)
                {
                    List<byte> payloadList = new List<byte>();
                    payloadList.AddRange(address);
                    payloadList.AddRange(values);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.writeIntEEPROMblock;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Internal EEPROM block write request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if ((offset + (writeCount - 1)) > (intEEPROMsize - 1))
                    {
                        MessageBox.Show("Internal EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Count value cannot be 0!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else if (ExternalEEPROMRadioButton.Checked)
            {
                if (writeCount == 1)
                {
                    List<byte> payloadList = new List<byte>();
                    payloadList.AddRange(address);
                    payloadList.Add(values[0]);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.writeExtEEPROMbyte;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] External EEPROM byte write request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if (offset > (extEEPROMsize - 1))
                    {
                        MessageBox.Show("External EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (writeCount > 1)
                {
                    List<byte> payloadList = new List<byte>();
                    payloadList.AddRange(address);
                    payloadList.AddRange(values);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.writeExtEEPROMblock;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] External EEPROM block write request:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                    if ((offset + (writeCount - 1)) > (extEEPROMsize - 1))
                    {
                        MessageBox.Show("External EEPROM size exceeded (4096 bytes)!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Count value cannot be 0!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private async void SetLEDsButton_Click(object sender, EventArgs e)
        {
            bool success;
            List<byte> payloadList = new List<byte>();
            byte heartbeatInterval_HB, heartbeatInterval_LB, LEDBlinkDuration_HB, LEDBlinkDuration_LB;

            success = int.TryParse(HeartbeatIntervalTextBox.Text, out int heartbeatInterval);
            if (!success)
            {
                HeartbeatIntervalTextBox.Text = "5000";
                heartbeatInterval = 5000;
            }

            success = int.TryParse(LEDBlinkDurationTextBox.Text, out int LEDBlinkDuration);
            if (!success)
            {
                LEDBlinkDurationTextBox.Text = "50";
                LEDBlinkDuration = 50;
            }

            heartbeatInterval_HB = (byte)((heartbeatInterval >> 8) & 0xFF);
            heartbeatInterval_LB = (byte)(heartbeatInterval & 0xFF);
            LEDBlinkDuration_HB = (byte)((LEDBlinkDuration >> 8) & 0xFF);
            LEDBlinkDuration_LB = (byte)(LEDBlinkDuration & 0xFF);

            payloadList.AddRange(new byte[] { heartbeatInterval_HB, heartbeatInterval_LB, LEDBlinkDuration_HB, LEDBlinkDuration_LB });

            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.settings;
            Packet.tx.mode = (byte)Packet.SettingsMode.leds;
            Packet.tx.payload = payloadList.ToArray();
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Change LED settings:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private void EEPROMWriteEnableCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (EEPROMWriteEnableCheckBox.Checked)
            {
                if (MessageBox.Show("Modifying EEPROM values can cause the device to behave unpredictably!" + Environment.NewLine + 
                                    "Are you sure you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    WriteEEPROMButton.Enabled = true;
                    EEPROMWriteAddressLabel.Enabled = true;
                    EEPROMWriteAddressTextBox.Enabled = true;
                    EEPROMWriteValuesLabel.Enabled = true;
                    EEPROMWriteValuesTextBox.Enabled = true;
                }
                else
                {
                    EEPROMWriteEnableCheckBox.Checked = false;
                }
            }
            else
            {
                WriteEEPROMButton.Enabled = false;
                EEPROMWriteAddressLabel.Enabled = false;
                EEPROMWriteAddressTextBox.Enabled = false;
                EEPROMWriteValuesLabel.Enabled = false;
                EEPROMWriteValuesTextBox.Enabled = false;
            }
        }

        private void EEPROMReadAddressTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                ReadEEPROMButton_Click(this, EventArgs.Empty);
            }
        }

        private void EEPROMReadCountTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                ReadEEPROMButton_Click(this, EventArgs.Empty);
            }
        }

        private void EEPROMWriteAddressTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                WriteEEPROMButton_Click(this, EventArgs.Empty);
            }
        }

        private void EEPROMWriteValuesTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                WriteEEPROMButton_Click(this, EventArgs.Empty);
            }
        }

        private void HeartbeatIntervalTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                SetLEDsButton_Click(this, EventArgs.Empty);
            }
        }

        private void LEDBlinkDurationTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                SetLEDsButton_Click(this, EventArgs.Empty);
            }
        }

        #endregion

        #region CCD-bus tab 

        private void CCDBusTxMessageAddButton_Click(object sender, EventArgs e)
        {
            string[] messageStrings = CCDBusTxMessageComboBox.Text.Split(','); // multiple messages can be separated by commas

            foreach (string element in messageStrings)
            {
                byte[] message = Util.HexStringToByte(element);
                if (message.Length > 0)
                {
                    byte messageID = message[0];
                    bool match = false;
                    int rowNumber = 0;

                    if (CCDBusTxMessageCalculateChecksumCheckBox.Checked && (message.Length > 1))
                    {
                        byte checksum = 0;
                        int checksumLocation = message.Length - 1;

                        for (int i = 0; i < checksumLocation; i++)
                        {
                            checksum += message[i];
                        }

                        message[checksumLocation] = checksum;

                        CCDBusTxMessageComboBox.Text = Util.ByteToHexStringSimple(message);
                        CCDBusTxMessageComboBox.SelectionStart = CCDBusTxMessageComboBox.Text.Length;
                        CCDBusTxMessageComboBox.SelectionLength = 0;
                    }

                    for (int i = 0; i < CCDBusTxMessagesListBox.Items.Count; i++)
                    {
                        byte[] item = Util.HexStringToByte(CCDBusTxMessagesListBox.Items[i].ToString());
                        if (item[0] == messageID)
                        {
                            match = true;
                            rowNumber = i;
                            break;
                        }
                    }

                    string itemToInsert = Util.ByteToHexStringSimple(message);

                    if (!match)
                    {
                        CCDBusTxMessagesListBox.Items.Add(itemToInsert);
                    }
                    else
                    {
                        if (CCDBusOverwriteDuplicateIDCheckBox.Checked && (!CCDBusTxMessageRepeatIntervalCheckBox.Checked || !CCDBusTxMessageRepeatIncrementCheckBox.Checked))
                        {
                            CCDBusTxMessagesListBox.Items.RemoveAt(rowNumber);
                            CCDBusTxMessagesListBox.Items.Insert(rowNumber, itemToInsert);
                        }
                        else
                        {
                            CCDBusTxMessagesListBox.Items.Add(itemToInsert);
                        }
                    }

                    if (CCDBusTxMessagesListBox.Items.Count > 0)
                    {
                        CCDBusTxMessageRemoveItemButton.Enabled = true;
                        CCDBusTxMessageClearListButton.Enabled = true;
                        CCDBusSendMessagesButton.Enabled = true;
                        CCDBusStopRepeatedMessagesButton.Enabled = true;
                    }
                    else
                    {
                        CCDBusTxMessageRemoveItemButton.Enabled = false;
                        CCDBusTxMessageClearListButton.Enabled = false;
                        CCDBusSendMessagesButton.Enabled = false;
                        CCDBusStopRepeatedMessagesButton.Enabled = false;
                    }

                    if (!CCDBusTxMessageComboBox.Items.Contains(CCDBusTxMessageComboBox.Text)) // only add unique items (no repeat!)
                    {
                        CCDBusTxMessageComboBox.Items.Add(CCDBusTxMessageComboBox.Text); // add message to the list so it can be selected later
                    }

                    if (CCDBusTxMessageAddButton.Text == "Edit") CCDBusTxMessageAddButton.Text = "Add";
                }
            }

            CCDBusTxMessagesListBox.TopIndex = CCDBusTxMessagesListBox.Items.Count - 1; // scroll down
        }

        private void CCDBusTxMessageRemoveItemButton_Click(object sender, EventArgs e)
        {
            if (CCDBusTxMessagesListBox.SelectedIndex > -1)
            {
                for (int i = CCDBusTxMessagesListBox.SelectedIndices.Count - 1; i >= 0; i--)
                {
                    int index = CCDBusTxMessagesListBox.SelectedIndices[i];
                    CCDBusTxMessagesListBox.Items.RemoveAt(index);
                }

                if (CCDBusTxMessagesListBox.Items.Count > 0)
                {
                    CCDBusTxMessageRemoveItemButton.Enabled = true;
                    CCDBusTxMessageClearListButton.Enabled = true;
                    CCDBusSendMessagesButton.Enabled = true;
                    CCDBusStopRepeatedMessagesButton.Enabled = true;
                }
                else
                {
                    CCDBusTxMessageRemoveItemButton.Enabled = false;
                    CCDBusTxMessageClearListButton.Enabled = false;
                    CCDBusSendMessagesButton.Enabled = false;
                    CCDBusStopRepeatedMessagesButton.Enabled = false;
                }
            }
        }

        private void CCDBusTxMessageClearListButton_Click(object sender, EventArgs e)
        {
            CCDBusTxMessagesListBox.Items.Clear();
            CCDBusTxMessageRemoveItemButton.Enabled = false;
            CCDBusTxMessageClearListButton.Enabled = false;
            CCDBusSendMessagesButton.Enabled = false;
            CCDBusStopRepeatedMessagesButton.Enabled = false;
        }

        private async void CCDBusSendMessagesButton_Click(object sender, EventArgs e)
        {
            if (DebugRandomCCDBusMessagesButton.Text == "Stop random messages")
            {
                DebugRandomCCDBusMessagesButton_Click(this, EventArgs.Empty);
            }

            if (!CCDBusTxMessageRepeatIntervalCheckBox.Checked) // no repeat
            {
                if (CCDBusTxMessagesListBox.Items.Count == 1) // single message once
                {
                    byte[] message = Util.HexStringToByte(CCDBusTxMessagesListBox.Items[0].ToString());

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.ccd;
                    Packet.tx.command = (byte)Packet.Command.msgTx;
                    Packet.tx.mode = (byte)Packet.MsgTxMode.single;
                    Packet.tx.payload = message;
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Send a CCD-bus message once:", Packet.tx.buffer);
                    if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus message Tx list:" + Environment.NewLine +
                                                                           "       " + Util.ByteToHexStringSimple(message));
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                }
                else // list of messages once
                {
                    List<byte[]> messages = new List<byte[]>();
                    List<byte> payloadList = new List<byte>();
                    byte messageCount = (byte)CCDBusTxMessagesListBox.Items.Count;

                    for (int i = 0; i < messageCount; i++)
                    {
                        messages.Add(Util.HexStringToByte(CCDBusTxMessagesListBox.Items[i].ToString()));
                    }

                    payloadList.Add(0x00); // no iteration with incrementing
                    payloadList.Add(messageCount); // number of messages

                    for (int i = 0; i < messageCount; i++)
                    {
                        payloadList.Add((byte)messages[i].Length); // message length
                        payloadList.AddRange(messages[i]); // message
                    }

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.ccd;
                    Packet.tx.command = (byte)Packet.Command.msgTx;
                    Packet.tx.mode = (byte)Packet.MsgTxMode.list;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Send CCD-bus message list once:", Packet.tx.buffer);

                    StringBuilder messageList = new StringBuilder();

                    foreach (byte[] item in messages)
                    {
                        messageList.Append("       " + Util.ByteToHexStringSimple(item) + Environment.NewLine);
                    }
                    messageList.Replace(Environment.NewLine, string.Empty, messageList.Length - 2, 2); // remove last newline character

                    if (messages.Count > 0) Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus message Tx list:" + Environment.NewLine + messageList.ToString());

                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                }
            }
            else // repeat message(s) forever
            {
                if (CCDBusTxMessagesListBox.Items.Count == 1) // repeated single message
                {
                    if (CCDBusTxMessageRepeatIncrementCheckBox.Checked) // message iteration with parameter incrementing is not supported in this case
                    {
                        MessageBox.Show("Two messages (start/end) are required for iteration with parameter incrementing." + Environment.NewLine +
                                        "CCD-bus example for BCM ROM reading:" + Environment.NewLine +
                                        "- start message: B2 20 22 00 00 F4" + Environment.NewLine +
                                        "- end message: B2 20 22 FF FE F1" + Environment.NewLine +
                                        "- increment: 00 02", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    // First send a settings packet to configure repeat behavior
                    SetRepeatedMessageBehavior(Packet.Target.ccd);

                    byte[] message = Util.HexStringToByte(CCDBusTxMessagesListBox.Items[0].ToString());
                    List<byte> payloadList = new List<byte>();

                    payloadList.Add(0x00); // no iteration with parameter incrementing
                    payloadList.Add(0x01); // 1 message only
                    payloadList.Add((byte)message.Length); // message length
                    payloadList.AddRange(message);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.ccd;
                    Packet.tx.command = (byte)Packet.Command.msgTx;
                    Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedSingle;
                    Packet.tx.payload = payloadList.ToArray();
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Send a repeated CCD-bus message:", Packet.tx.buffer);
                    if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus message Tx list:" + Environment.NewLine +
                                                                           "       " + Util.ByteToHexStringSimple(message));
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

                }
                else // repeated list of messages
                {
                    if (!CCDBusTxMessageRepeatIncrementCheckBox.Checked) // no message iteration with parameter incrementing
                    {
                        // First send a settings packet to configure repeat behavior
                        SetRepeatedMessageBehavior(Packet.Target.ccd);

                        List<byte[]> messages = new List<byte[]>();
                        List<byte> payloadList = new List<byte>();
                        byte messageCount = (byte)CCDBusTxMessagesListBox.Items.Count;

                        for (int i = 0; i < messageCount; i++)
                        {
                            messages.Add(Util.HexStringToByte(CCDBusTxMessagesListBox.Items[i].ToString()));
                        }

                        payloadList.Add(0x00); // no iteration with parameter incrementing
                        payloadList.Add(messageCount); // number of messages

                        for (int i = 0; i < messageCount; i++)
                        {
                            payloadList.Add((byte)messages[i].Length); // message length
                            payloadList.AddRange(messages[i]); // message
                        }

                        Packet.tx.source = (byte)Packet.Source.device;
                        Packet.tx.target = (byte)Packet.Target.ccd;
                        Packet.tx.command = (byte)Packet.Command.msgTx;
                        Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedList;
                        Packet.tx.payload = payloadList.ToArray();
                        Packet.GeneratePacket();
                        Util.UpdateTextBox(USBTextBox, "[<-TX] Send repeated CCD-bus message list:", Packet.tx.buffer);

                        StringBuilder messageList = new StringBuilder();

                        foreach (byte[] item in messages)
                        {
                            messageList.Append("       " + Util.ByteToHexStringSimple(item) + Environment.NewLine);
                        }
                        messageList.Replace(Environment.NewLine, string.Empty, messageList.Length - 2, 2); // remove last newline character

                        if (messages.Count > 0) Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus message Tx list:" + Environment.NewLine + messageList.ToString());

                        await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                    }
                    else // message iteration with parameter incrementing
                    {
                        if (CCDBusTxMessagesListBox.Items.Count != 2) // message iteration with parameter incrementing is not supported in this case
                        {
                            MessageBox.Show("Two messages (start/end) are required for iteration with parameter incrementing." + Environment.NewLine +
                                            "CCD-bus example for BCM ROM reading:" + Environment.NewLine +
                                            "- start message: B2 20 22 00 00 F4" + Environment.NewLine +
                                            "- end message: B2 20 22 FF FE F1" + Environment.NewLine +
                                            "- increment: 00 02", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        byte[] messageStart = Util.HexStringToByte(CCDBusTxMessagesListBox.Items[0].ToString());
                        byte[] messageEnd = Util.HexStringToByte(CCDBusTxMessagesListBox.Items[1].ToString());
                        byte messageStartID = messageStart[0];
                        byte messageEndID = messageEnd[0];
                        List<byte> payloadList = new List<byte>();

                        if (!((messageStartID == 0xB2) && (messageStart.Length == 6) && (messageEndID == 0xB2) && (messageEnd.Length == 6)))
                        {
                            MessageBox.Show("This message structure is not supported." + Environment.NewLine +
                                            "In this mode the 6 bytes long B2 request message is supported only." + Environment.NewLine +
                                            "CCD-bus example for BCM ROM reading:" + Environment.NewLine +
                                            "- start message: B2 20 22 00 00 F4" + Environment.NewLine +
                                            "- end message: B2 20 22 FF FE F1" + Environment.NewLine +
                                            "- increment: 00 02", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        // First send a settings packet to configure repeat behavior
                        SetRepeatedMessageBehavior(Packet.Target.ccd);

                        payloadList.Add(0x01); // message iteration with parameter incrementing
                        payloadList.Add(0x01); // 1 message only, although there's two in the listbox: 1 for start message that is being incremented until the second message is reached
                        payloadList.Add((byte)messageStart.Length); // message length
                        payloadList.AddRange(messageStart);
                        payloadList.AddRange(messageEnd);

                        Packet.tx.source = (byte)Packet.Source.device;
                        Packet.tx.target = (byte)Packet.Target.ccd;
                        Packet.tx.command = (byte)Packet.Command.msgTx;
                        Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedSingle;
                        Packet.tx.payload = payloadList.ToArray();
                        Packet.GeneratePacket();
                        Util.UpdateTextBox(USBTextBox, "[<-TX] Send an iterated CCD-bus message:", Packet.tx.buffer);
                        Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus iterated message information:" + Environment.NewLine +
                                                       "       - start: " + Util.ByteToHexStringSimple(messageStart) + Environment.NewLine +
                                                       "       - end:   " + Util.ByteToHexStringSimple(messageEnd) + Environment.NewLine +
                                                       "       - incr.: " + Util.ByteToHexStringSimple(Util.HexStringToByte(CCDBusTxMessageRepeatIncrementTextBox.Text)));
                        await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                    }
                }
            }
        }

        private async void CCDBusStopRepeatedMessagesButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.ccd;
            Packet.tx.command = (byte)Packet.Command.msgTx;
            Packet.tx.mode = (byte)Packet.MsgTxMode.stop;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Stop repeated message Tx on CCD-bus:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void DebugRandomCCDBusMessagesButton_Click(object sender, EventArgs e)
        {
            if (DebugRandomCCDBusMessagesButton.Text == "Send random messages")
            {
                if (MessageBox.Show("Use this debug mode with caution!" + Environment.NewLine + "The random generated messages may confuse modules on a live CCD-bus network.", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.OK)
                {
                    bool success;
                    byte minIntervalHB, minIntervalLB, maxIntervalHB, maxIntervalLB;

                    success = int.TryParse(CCDBusRandomMessageIntervalMinTextBox.Text, out int minInterval);
                    if (!success || (minInterval == 0))
                    {
                        minInterval = 200;
                        CCDBusRandomMessageIntervalMinTextBox.Text = "200";
                    }

                    success = int.TryParse(CCDBusRandomMessageIntervalMaxTextBox.Text, out int maxInterval);
                    if (!success || (maxInterval == 0))
                    {
                        maxInterval = 1000;
                        CCDBusRandomMessageIntervalMaxTextBox.Text = "1000";
                    }

                    minIntervalHB = (byte)((minInterval >> 8) & 0xFF);
                    minIntervalLB = (byte)(minInterval & 0xFF);
                    maxIntervalHB = (byte)((maxInterval >> 8) & 0xFF);
                    maxIntervalLB = (byte)(maxInterval & 0xFF);

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.device;
                    Packet.tx.command = (byte)Packet.Command.debug;
                    Packet.tx.mode = (byte)Packet.DebugMode.randomCCDBusMessages;
                    Packet.tx.payload = new byte[5] { 0x01, minIntervalHB, minIntervalLB, maxIntervalHB, maxIntervalLB };
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Send random CCD-bus messages:", Packet.tx.buffer);
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                    DebugRandomCCDBusMessagesButton.Text = "Stop random messages";
                }
            }
            else if (DebugRandomCCDBusMessagesButton.Text == "Stop random messages")
            {
                Packet.tx.source = (byte)Packet.Source.device;
                Packet.tx.target = (byte)Packet.Target.device;
                Packet.tx.command = (byte)Packet.Command.debug;
                Packet.tx.mode = (byte)Packet.DebugMode.randomCCDBusMessages;
                Packet.tx.payload = new byte[5] { 0x00, 0x00, 0x00, 0x00, 0x00 };
                Packet.GeneratePacket();
                Util.UpdateTextBox(USBTextBox, "[<-TX] Stop random CCD-bus messages:", Packet.tx.buffer);
                await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                DebugRandomCCDBusMessagesButton.Text = "Send random messages";
            }
        }

        private async void MeasureCCDBusVoltagesButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.request;
            Packet.tx.mode = (byte)Packet.RequestMode.CCDBusVoltages;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Measure CCD-bus voltages request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private void CCDBusTxMessagesListBox_DoubleClick(object sender, EventArgs e)
        {
            if (CCDBusTxMessageRepeatIncrementCheckBox.Checked)
            {
                MessageBox.Show("Message editing is not possible if incrementing/iteration is enabled.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if ((CCDBusTxMessagesListBox.Items.Count > 0) && (CCDBusTxMessagesListBox.SelectedIndex > -1))
            {
                string selectedItem = CCDBusTxMessagesListBox.SelectedItem.ToString();
                CCDBusTxMessageComboBox.Text = selectedItem;
                CCDBusTxMessageComboBox.Focus();
                CCDBusTxMessageComboBox.SelectionStart = CCDBusTxMessageComboBox.Text.Length;
                CCDBusTxMessageAddButton.Text = "Edit";
            }
        }

        private void CCDBusTxMessageRepeatIntervalCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (CCDBusTxMessageRepeatIntervalCheckBox.Checked)
            {
                CCDBusTxMessageRepeatIntervalTextBox.Enabled = true;
                MillisecondsLabel03.Enabled = true;
                CCDBusTxMessageRepeatIncrementCheckBox.Enabled = true;
            }
            else
            {
                CCDBusTxMessageRepeatIntervalTextBox.Enabled = false;
                MillisecondsLabel03.Enabled = false;
                CCDBusTxMessageRepeatIncrementCheckBox.Enabled = false;
            }
        }

        private void CCDBusTxMessageRepeatIncrementCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (CCDBusTxMessageRepeatIncrementCheckBox.Checked)
            {
                CCDBusTxMessageRepeatIncrementTextBox.Enabled = true;
            }
            else
            {
                CCDBusTxMessageRepeatIncrementTextBox.Enabled = false;
            }
        }

        private async void CCDBusSettingsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            byte CCDBusState;
            byte CCDBusTerminationBiasOnOff;

            if (CCDBusTransceiverOnOffCheckBox.Checked)
            {
                CCDBusState = 0x01;
                CCDBusTransceiverOnOffCheckBox.Text = "CCD-bus transceiver ON";
                CCD.UpdateHeader("enabled", null, null);
            }
            else
            {
                CCDBusState = 0x00;
                CCDBusTransceiverOnOffCheckBox.Text = "CCD-bus transceiver OFF";
                CCD.UpdateHeader("disabled", null, null);
            }

            if (CCDBusTerminationBiasOnOffCheckBox.Checked)
            {
                CCDBusTerminationBiasOnOff = 0x01;
                CCDBusTerminationBiasOnOffCheckBox.Text = "CCD-bus termination / bias ON";
            }
            else
            {
                CCDBusTerminationBiasOnOff = 0x00;
                CCDBusTerminationBiasOnOffCheckBox.Text = "CCD-bus termination / bias OFF";
            }

            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.settings;
            Packet.tx.mode = (byte)Packet.SettingsMode.setCCDBus;
            Packet.tx.payload = new byte[2] { CCDBusState, CCDBusTerminationBiasOnOff };
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Change CCD-bus settings:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private void CCDBusTxMessageRepeatIntervalTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                SetRepeatedMessageBehavior(Packet.Target.ccd);
            }
        }

        private void CCDBusTxMessageRepeatIncrementTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                SetRepeatedMessageBehavior(Packet.Target.ccd);
            }
        }

        private void CCDBusRandomMessageIntervalMinTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                DebugRandomCCDBusMessagesButton_Click(this, EventArgs.Empty);
            }
        }

        private void CCDBusRandomMessageIntervalMaxTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                DebugRandomCCDBusMessagesButton_Click(this, EventArgs.Empty);
            }
        }

        private async void CCDBusTxMessageComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;

                if (CCDBusTxMessageComboBox.Text.Length < 2) return;

                if (CCDBusTxMessageComboBox.Text.Contains(","))
                {
                    CCDBusTxMessageAddButton_Click(this, EventArgs.Empty);

                    if (!CCDBusTxMessageComboBox.Items.Contains(CCDBusTxMessageComboBox.Text)) // only add unique items (no repeat!)
                    {
                        CCDBusTxMessageComboBox.Items.Add(CCDBusTxMessageComboBox.Text); // add message to the list so it can be selected later
                    }

                    CCDBusTxMessageComboBox.Text = string.Empty;
                    return;
                }

                byte[] message = Util.HexStringToByte(CCDBusTxMessageComboBox.Text);

                if (CCDBusTxMessageAddButton.Text != "Edit")
                {
                    if (CCDBusTxMessageCalculateChecksumCheckBox.Checked && (message.Length > 1))
                    {
                        byte checksum = 0;
                        int checksumLocation = message.Length - 1;

                        for (int i = 0; i < checksumLocation; i++)
                        {
                            checksum += message[i];
                        }

                        message[checksumLocation] = checksum;

                        CCDBusTxMessageComboBox.Text = Util.ByteToHexStringSimple(message);
                        CCDBusTxMessageComboBox.SelectionStart = CCDBusTxMessageComboBox.Text.Length;
                        CCDBusTxMessageComboBox.SelectionLength = 0;
                    }

                    Packet.tx.source = (byte)Packet.Source.device;
                    Packet.tx.target = (byte)Packet.Target.ccd;
                    Packet.tx.command = (byte)Packet.Command.msgTx;
                    Packet.tx.mode = (byte)Packet.MsgTxMode.single;
                    Packet.tx.payload = message;
                    Packet.GeneratePacket();
                    Util.UpdateTextBox(USBTextBox, "[<-TX] Send a CCD-bus message once:", Packet.tx.buffer);
                    if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] CCD-bus message Tx list:" + Environment.NewLine +
                                                                           "       " + Util.ByteToHexStringSimple(message));
                    await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                }
                else
                {
                    if (CCDBusTxMessageCalculateChecksumCheckBox.Checked && (message.Length > 1))
                    {
                        byte checksum = 0;
                        int checksumLocation = message.Length - 1;

                        for (int i = 0; i < checksumLocation; i++)
                        {
                            checksum += message[i];
                        }

                        message[checksumLocation] = checksum;

                        CCDBusTxMessageComboBox.Text = Util.ByteToHexStringSimple(message);
                        CCDBusTxMessageComboBox.SelectionStart = CCDBusTxMessageComboBox.Text.Length;
                        CCDBusTxMessageComboBox.SelectionLength = 0;
                    }

                    CCDBusTxMessageAddButton_Click(this, EventArgs.Empty);
                }

                if (!CCDBusTxMessageComboBox.Items.Contains(CCDBusTxMessageComboBox.Text)) // only add unique items (no repeat!)
                {
                    CCDBusTxMessageComboBox.Items.Add(CCDBusTxMessageComboBox.Text); // add message to the list so it can be selected later
                }
            }
        }

        #endregion

        #region SCI-bus tab 

        private void SCIBusTxMessageAddButton_Click(object sender, EventArgs e)
        {
            string[] messageStrings = SCIBusTxMessageComboBox.Text.Split(','); // multiple messages can be separated by commas

            foreach (string element in messageStrings)
            {
                byte[] message = Util.HexStringToByte(element);
                if (message.Length > 0)
                {
                    byte messageID = message[0];
                    bool match = false;
                    int rowNumber = 0;

                    if (SCIBusTxMessageCalculateChecksumCheckBox.Checked && (message.Length > 1))
                    {
                        byte checksum = 0;
                        int checksumLocation = message.Length - 1;

                        for (int i = 0; i < checksumLocation; i++)
                        {
                            checksum += message[i];
                        }

                        message[checksumLocation] = checksum;
                    }

                    for (int i = 0; i < SCIBusTxMessagesListBox.Items.Count; i++)
                    {
                        byte[] item = Util.HexStringToByte(SCIBusTxMessagesListBox.Items[i].ToString());
                        if (item[0] == messageID)
                        {
                            match = true;
                            rowNumber = i;
                            break;
                        }
                    }

                    string itemToInsert = Util.ByteToHexStringSimple(message);

                    if (!match)
                    {
                        SCIBusTxMessagesListBox.Items.Add(itemToInsert);
                    }
                    else
                    {
                        if (SCIBusOverwriteDuplicateIDCheckBox.Checked && (!SCIBusTxMessageRepeatIntervalCheckBox.Checked || !SCIBusTxMessageRepeatIncrementCheckBox.Checked))
                        {
                            SCIBusTxMessagesListBox.Items.RemoveAt(rowNumber);
                            SCIBusTxMessagesListBox.Items.Insert(rowNumber, itemToInsert);
                        }
                        else
                        {
                            SCIBusTxMessagesListBox.Items.Add(itemToInsert);
                        }
                    }

                    if (SCIBusTxMessagesListBox.Items.Count > 0)
                    {
                        SCIBusTxMessageRemoveItemButton.Enabled = true;
                        SCIBusTxMessageClearListButton.Enabled = true;
                        SCIBusSendMessagesButton.Enabled = true;
                        SCIBusStopRepeatedMessagesButton.Enabled = true;
                    }
                    else
                    {
                        SCIBusTxMessageRemoveItemButton.Enabled = false;
                        SCIBusTxMessageClearListButton.Enabled = false;
                        SCIBusSendMessagesButton.Enabled = false;
                        SCIBusStopRepeatedMessagesButton.Enabled = false;
                    }

                    if (!SCIBusTxMessageComboBox.Items.Contains(SCIBusTxMessageComboBox.Text)) // only add unique items (no repeat!)
                    {
                        SCIBusTxMessageComboBox.Items.Add(SCIBusTxMessageComboBox.Text); // add message to the list so it can be selected later
                    }

                    if (SCIBusTxMessageAddButton.Text == "Edit") SCIBusTxMessageAddButton.Text = "Add";
                }
            }

            SCIBusTxMessagesListBox.TopIndex = SCIBusTxMessagesListBox.Items.Count - 1; // scroll down
        }

        private void SCIBusTxMessageRemoveItemButton_Click(object sender, EventArgs e)
        {
            if (SCIBusTxMessagesListBox.SelectedIndex > -1)
            {
                for (int i = SCIBusTxMessagesListBox.SelectedIndices.Count - 1; i >= 0; i--)
                {
                    int index = SCIBusTxMessagesListBox.SelectedIndices[i];
                    SCIBusTxMessagesListBox.Items.RemoveAt(index);
                }

                if (SCIBusTxMessagesListBox.Items.Count > 0)
                {
                    SCIBusTxMessageRemoveItemButton.Enabled = true;
                    SCIBusTxMessageClearListButton.Enabled = true;
                    SCIBusSendMessagesButton.Enabled = true;
                    SCIBusStopRepeatedMessagesButton.Enabled = true;
                }
                else
                {
                    SCIBusTxMessageRemoveItemButton.Enabled = false;
                    SCIBusTxMessageClearListButton.Enabled = false;
                    SCIBusSendMessagesButton.Enabled = false;
                    SCIBusStopRepeatedMessagesButton.Enabled = false;
                }
            }
        }

        private void SCIBusTxMessageClearListButton_Click(object sender, EventArgs e)
        {
            SCIBusTxMessagesListBox.Items.Clear();
            SCIBusTxMessageRemoveItemButton.Enabled = false;
            SCIBusTxMessageClearListButton.Enabled = false;
            SCIBusSendMessagesButton.Enabled = false;
            SCIBusStopRepeatedMessagesButton.Enabled = false;
        }

        private async void SCIBusSendMessagesButton_Click(object sender, EventArgs e)
        {
            if (!SCIBusTxMessageRepeatIntervalCheckBox.Checked) // no repeat
            {
                if (SCIBusTxMessagesListBox.Items.Count == 1) // single message once
                {
                    byte[] message = Util.HexStringToByte(SCIBusTxMessagesListBox.Items[0].ToString());

                    switch (SCIBusModuleComboBox.SelectedIndex)
                    {
                        case 0: // engine
                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.pcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.single;
                            Packet.tx.payload = message;
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send an SCI-bus (PCM) message once:", Packet.tx.buffer);
                            if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) message Tx list:" + Environment.NewLine +
                                                                                   "       " + Util.ByteToHexStringSimple(message));
                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                        case 1: // transmission
                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.tcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.single;
                            Packet.tx.payload = message;
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send an SCI-bus (TCM) message once:", Packet.tx.buffer);
                            if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) message Tx list:" + Environment.NewLine +
                                                                                   "       " + Util.ByteToHexStringSimple(message));
                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                    }
                   
                }
                else // list of messages once
                {
                    List<byte[]> messages = new List<byte[]>();
                    List<byte> payloadList = new List<byte>();
                    byte messageCount = (byte)SCIBusTxMessagesListBox.Items.Count;

                    for (int i = 0; i < messageCount; i++)
                    {
                        messages.Add(Util.HexStringToByte(SCIBusTxMessagesListBox.Items[i].ToString()));
                    }

                    payloadList.Add(0x00); // no iteration with incrementing
                    payloadList.Add(messageCount); // number of messages

                    for (int i = 0; i < messageCount; i++)
                    {
                        payloadList.Add((byte)messages[i].Length); // message length
                        payloadList.AddRange(messages[i]); // message
                    }

                    StringBuilder messageList = new StringBuilder();

                    switch (SCIBusModuleComboBox.SelectedIndex)
                    {
                        case 0: // engine
                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.pcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.list;
                            Packet.tx.payload = payloadList.ToArray();
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send SCI-bus (PCM) message list once:", Packet.tx.buffer);

                            foreach (byte[] item in messages)
                            {
                                messageList.Append("       " + Util.ByteToHexStringSimple(item) + Environment.NewLine);
                            }
                            messageList.Replace(Environment.NewLine, string.Empty, messageList.Length - 2, 2); // remove last newline character

                            if (messages.Count > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) message Tx list:" + Environment.NewLine + messageList.ToString());

                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                        case 1: // transmission
                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.tcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.list;
                            Packet.tx.payload = payloadList.ToArray();
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send SCI-bus (TCM) message list once:", Packet.tx.buffer);

                            foreach (byte[] item in messages)
                            {
                                messageList.Append("       " + Util.ByteToHexStringSimple(item) + Environment.NewLine);
                            }
                            messageList.Replace(Environment.NewLine, string.Empty, messageList.Length - 2, 2); // remove last newline character

                            if (messages.Count > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) message Tx list:" + Environment.NewLine + messageList.ToString());

                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                    }
                }
            }
            else // repeat message(s) forever
            {
                if (SCIBusTxMessagesListBox.Items.Count == 1) // repeated single message
                {
                    if (SCIBusTxMessageRepeatIncrementCheckBox.Checked) // message iteration with parameter incrementing is not supported in this case
                    {
                        MessageBox.Show("Two messages (start/end) are required for iteration with parameter incrementing." + Environment.NewLine +
                                        "Example for PCM ROM reading:" + Environment.NewLine +
                                        "- start message: 26 00 00 00" + Environment.NewLine +
                                        "- end message: 26 01 FF FF" + Environment.NewLine +
                                        "- increment: 00 01", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    byte[] message = Util.HexStringToByte(SCIBusTxMessagesListBox.Items[0].ToString());
                    List<byte> payloadList = new List<byte>();

                    payloadList.Add(0x00); // no iteration with parameter incrementing
                    payloadList.Add(0x01); // 1 message only
                    payloadList.Add((byte)message.Length); // message length
                    payloadList.AddRange(message);

                    switch (SCIBusModuleComboBox.SelectedIndex)
                    {
                        case 0: // engine 
                            SetRepeatedMessageBehavior(Packet.Target.pcm); // first send a settings packet to configure repeat behavior

                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.pcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedSingle;
                            Packet.tx.payload = payloadList.ToArray();
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send a repeated SCI-bus (PCM) message:", Packet.tx.buffer);
                            if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) message Tx list:" + Environment.NewLine +
                                                                                   "       " + Util.ByteToHexStringSimple(message));
                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                        case 1: // transmission
                            SetRepeatedMessageBehavior(Packet.Target.tcm); // first send a settings packet to configure repeat behavior

                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.tcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedSingle;
                            Packet.tx.payload = payloadList.ToArray();
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send a repeated SCI-bus (TCM) message:", Packet.tx.buffer);
                            if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) message Tx list:" + Environment.NewLine +
                                                                                   "       " + Util.ByteToHexStringSimple(message));
                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                    }
                }
                else // repeated list of messages
                {
                    if (!SCIBusTxMessageRepeatIncrementCheckBox.Checked) // no message iteration with parameter incrementing
                    {
                        List<byte[]> messages = new List<byte[]>();
                        List<byte> payloadList = new List<byte>();
                        byte messageCount = (byte)SCIBusTxMessagesListBox.Items.Count;

                        for (int i = 0; i < messageCount; i++)
                        {
                            messages.Add(Util.HexStringToByte(SCIBusTxMessagesListBox.Items[i].ToString()));
                        }

                        payloadList.Add(0x00); // no iteration with parameter incrementing
                        payloadList.Add(messageCount); // number of messages

                        for (int i = 0; i < messageCount; i++)
                        {
                            payloadList.Add((byte)messages[i].Length); // message length
                            payloadList.AddRange(messages[i]); // message
                        }

                        StringBuilder messageList = new StringBuilder();

                        switch (SCIBusModuleComboBox.SelectedIndex)
                        {
                            case 0: // engine
                                SetRepeatedMessageBehavior(Packet.Target.pcm); // first send a settings packet to configure repeat behavior

                                Packet.tx.source = (byte)Packet.Source.device;
                                Packet.tx.target = (byte)Packet.Target.pcm;
                                Packet.tx.command = (byte)Packet.Command.msgTx;
                                Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedList;
                                Packet.tx.payload = payloadList.ToArray();
                                Packet.GeneratePacket();
                                Util.UpdateTextBox(USBTextBox, "[<-TX] Send repeated SCI-bus (PCM) message list:", Packet.tx.buffer);

                                foreach (byte[] item in messages)
                                {
                                    messageList.Append("       " + Util.ByteToHexStringSimple(item) + Environment.NewLine);
                                }
                                messageList.Replace(Environment.NewLine, string.Empty, messageList.Length - 2, 2); // remove last newline character

                                if (messages.Count > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) message Tx list:" + Environment.NewLine + messageList.ToString());

                                await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                                break;
                            case 1: // transmission
                                SetRepeatedMessageBehavior(Packet.Target.tcm); // first send a settings packet to configure repeat behavior

                                Packet.tx.source = (byte)Packet.Source.device;
                                Packet.tx.target = (byte)Packet.Target.tcm;
                                Packet.tx.command = (byte)Packet.Command.msgTx;
                                Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedList;
                                Packet.tx.payload = payloadList.ToArray();
                                Packet.GeneratePacket();
                                Util.UpdateTextBox(USBTextBox, "[<-TX] Send repeated SCI-bus (TCM) message list:", Packet.tx.buffer);

                                foreach (byte[] item in messages)
                                {
                                    messageList.Append("       " + Util.ByteToHexStringSimple(item) + Environment.NewLine);
                                }
                                messageList.Replace(Environment.NewLine, string.Empty, messageList.Length - 2, 2); // remove last newline character

                                if (messages.Count > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) message Tx list:" + Environment.NewLine + messageList.ToString());

                                await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                                break;
                        }
                    }
                    else // message iteration with parameter incrementing
                    {
                        if (SCIBusTxMessagesListBox.Items.Count != 2) // message iteration with parameter incrementing is not supported in this case
                        {
                            MessageBox.Show("Two messages (start/end) are required for iteration with parameter incrementing." + Environment.NewLine +
                                            "Example for PCM ROM reading:" + Environment.NewLine +
                                            "- start message: 26 00 00 00" + Environment.NewLine +
                                            "- end message: 26 01 FF FF" + Environment.NewLine +
                                            "- increment: 00 01", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        byte[] messageStart = Util.HexStringToByte(SCIBusTxMessagesListBox.Items[0].ToString());
                        byte[] messageEnd = Util.HexStringToByte(SCIBusTxMessagesListBox.Items[1].ToString());
                        byte messageStartID = messageStart[0];
                        byte messageEndID = messageEnd[0];
                        List<byte> payloadList = new List<byte>();

                        if (((messageStartID != messageEndID) || (messageStart.Length != messageEnd.Length) || (messageStart.Length > 4) || (messageEnd.Length > 4)) && (messageStart.Length != 1))
                        {
                            MessageBox.Show("This message structure is not supported." + Environment.NewLine +
                                            "Make sure to follow these rules:" + Environment.NewLine +
                                            "- length: 1-4 bytes," + Environment.NewLine +
                                            "- both messages are of the same length," + Environment.NewLine +
                                            "- same ID byte." + Environment.NewLine +
                                            "Example for PCM ROM reading:" + Environment.NewLine +
                                            "- start message: 26 00 00 00" + Environment.NewLine +
                                            "- end message: 26 01 FF FF" + Environment.NewLine +
                                            "- increment: 00 01", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        switch (SCIBusModuleComboBox.SelectedIndex)
                        {
                            case 0: // engine
                                SetRepeatedMessageBehavior(Packet.Target.pcm); // first send a settings packet to configure repeat behavior

                                payloadList.Add(0x01); // message iteration with parameter incrementing
                                payloadList.Add(0x01); // 1 message only, although there's two in the listbox: 1 for start message that is being incremented until the second message is reached
                                payloadList.Add((byte)messageStart.Length); // message length
                                payloadList.AddRange(messageStart);
                                payloadList.AddRange(messageEnd);

                                Packet.tx.source = (byte)Packet.Source.device;
                                Packet.tx.target = (byte)Packet.Target.pcm;
                                Packet.tx.command = (byte)Packet.Command.msgTx;
                                Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedSingle;
                                Packet.tx.payload = payloadList.ToArray();
                                Packet.GeneratePacket();
                                Util.UpdateTextBox(USBTextBox, "[<-TX] Send an iterated SCI-bus (PCM) message:", Packet.tx.buffer);
                                Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) iterated message information:" + Environment.NewLine +
                                                               "       - start: " + Util.ByteToHexStringSimple(messageStart) + Environment.NewLine +
                                                               "       - end:   " + Util.ByteToHexStringSimple(messageEnd) + Environment.NewLine +
                                                               "       - incr.: " + Util.ByteToHexStringSimple(Util.HexStringToByte(SCIBusTxMessageRepeatIncrementTextBox.Text)));
                                await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                                break;
                            case 1: // tranmission
                                SetRepeatedMessageBehavior(Packet.Target.tcm); // first send a settings packet to configure repeat behavior

                                payloadList.Add(0x01); // message iteration with parameter incrementing
                                payloadList.Add(0x01); // 1 message only, although there's two in the listbox: 1 for start message that is being incremented until the second message is reached
                                payloadList.Add((byte)messageStart.Length); // message length
                                payloadList.AddRange(messageStart);
                                payloadList.AddRange(messageEnd);

                                Packet.tx.source = (byte)Packet.Source.device;
                                Packet.tx.target = (byte)Packet.Target.tcm;
                                Packet.tx.command = (byte)Packet.Command.msgTx;
                                Packet.tx.mode = (byte)Packet.MsgTxMode.repeatedSingle;
                                Packet.tx.payload = payloadList.ToArray();
                                Packet.GeneratePacket();
                                Util.UpdateTextBox(USBTextBox, "[<-TX] Send an iterated SCI-bus (TCM) message:", Packet.tx.buffer);
                                Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) iterated message information:" + Environment.NewLine +
                                                               "       - start: " + Util.ByteToHexStringSimple(messageStart) + Environment.NewLine +
                                                               "       - end:   " + Util.ByteToHexStringSimple(messageEnd) + Environment.NewLine +
                                                               "       - incr.: " + Util.ByteToHexStringSimple(Util.HexStringToByte(SCIBusTxMessageRepeatIncrementTextBox.Text)));
                                await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                                break;
                        }
                    }
                }
            }
        }

        private async void SCIBusStopRepeatedMessagesButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.pcm;
            Packet.tx.command = (byte)Packet.Command.msgTx;
            Packet.tx.mode = (byte)Packet.MsgTxMode.stop;
            Packet.tx.payload = null;
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Stop repeated message Tx on SCI-bus (PCM):", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void SCIBusModuleConfigSpeedApplyButton_Click(object sender, EventArgs e)
        {
            byte config = 0x00;

            switch (SCIBusModuleComboBox.SelectedIndex)
            {
                case 0: // engine
                    config = Util.SetBit(config, 7); // PCM
                    config = Util.ClearBit(config, 6); // PCM
                    config = Util.SetBit(config, 5); // set change bit

                    if (SCIBusOBD1EngineCableUsedCheckBox.Checked)
                    {
                        config = Util.SetBit(config, 3);
                    }
                    else
                    {
                        config = Util.ClearBit(config, 3);
                    }

                    if (SCIBusOBDConfigurationComboBox.SelectedIndex == 0)
                    {
                        config = Util.ClearBit(config, 2);
                    }
                    else
                    {
                        config = Util.SetBit(config, 2);
                    }

                    switch (SCIBusSpeedComboBox.SelectedIndex)
                    {
                        case 0: // off
                            config = Util.ClearBit(config, 4); // clear state bit (disabled)
                            config = Util.ClearBit(config, 1); // clear speed bit (dummy 976.5 baud)
                            config = Util.ClearBit(config, 0); // clear speed bit (dummy 976.5 baud)
                            break;
                        case 1: // 976.5 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.ClearBit(config, 1);
                            config = Util.ClearBit(config, 0);
                            break;
                        case 2: // 7812.5 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.ClearBit(config, 1);
                            config = Util.SetBit(config, 0);
                            break;
                        case 3: // 62500 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.SetBit(config, 1);
                            config = Util.ClearBit(config, 0);
                            break;
                        case 4: // 125000 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.SetBit(config, 1);
                            config = Util.SetBit(config, 0);
                            break;
                        default: // 7812.5 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.ClearBit(config, 1);
                            config = Util.SetBit(config, 0);
                            break;
                    }
                    break;
                case 1: // transmission
                    config = Util.SetBit(config, 7); // TCM
                    config = Util.SetBit(config, 6); // TCM
                    config = Util.SetBit(config, 5); // set change bit
                    config = Util.ClearBit(config, 3); // OBD1 engine cable not applicable here

                    if (SCIBusOBDConfigurationComboBox.SelectedIndex == 0)
                    {
                        config = Util.ClearBit(config, 2);
                    }
                    else
                    {
                        config = Util.SetBit(config, 2);
                    }

                    switch (SCIBusSpeedComboBox.SelectedIndex)
                    {
                        case 0: // off
                            config = Util.ClearBit(config, 4); // clear state bit (disabled)
                            config = Util.ClearBit(config, 1); // clear speed bit (dummy 976.5 baud)
                            config = Util.ClearBit(config, 0); // clear speed bit (dummy 976.5 baud)
                            break;
                        case 1: // 976.5 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.ClearBit(config, 1);
                            config = Util.ClearBit(config, 0);
                            break;
                        case 2: // 7812.5 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.ClearBit(config, 1);
                            config = Util.SetBit(config, 0);
                            break;
                        case 3: // 62500 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.SetBit(config, 1);
                            config = Util.ClearBit(config, 0);
                            break;
                        case 4: // 125000 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.SetBit(config, 1);
                            config = Util.SetBit(config, 0);
                            break;
                        default: // 7812.5 baud
                            config = Util.SetBit(config, 4); // set state bit (enabled)
                            config = Util.ClearBit(config, 1);
                            config = Util.SetBit(config, 0);
                            break;
                    }
                    break;
                default:
                    break;
            }

            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.settings;
            Packet.tx.mode = (byte)Packet.SettingsMode.setSCIBus;
            Packet.tx.payload = new byte[1] { config };
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Change SCI-bus settings:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void SCIBusReadFaultCodesButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.pcm;
            Packet.tx.command = (byte)Packet.Command.msgTx;
            Packet.tx.mode = (byte)Packet.MsgTxMode.single;
            Packet.tx.payload = new byte[1] { 0x10 };
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] PCM fault code list request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private async void SCIBusEraseFaultCodesButton_Click(object sender, EventArgs e)
        {
            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.pcm;
            Packet.tx.command = (byte)Packet.Command.msgTx;
            Packet.tx.mode = (byte)Packet.MsgTxMode.single;
            Packet.tx.payload = new byte[1] { 0x17 };
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Erase PCM fault code(s) request:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
        }

        private void SCIBusTxMessagesListBox_DoubleClick(object sender, EventArgs e)
        {
            if (SCIBusTxMessageRepeatIncrementCheckBox.Checked)
            {
                MessageBox.Show("Message editing is not possible if incrementing/iteration is enabled.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if ((SCIBusTxMessagesListBox.Items.Count > 0) && (SCIBusTxMessagesListBox.SelectedIndex > -1))
            {
                string selectedItem = SCIBusTxMessagesListBox.SelectedItem.ToString();
                SCIBusTxMessageComboBox.Text = selectedItem;
                SCIBusTxMessageComboBox.Focus();
                SCIBusTxMessageComboBox.SelectionStart = SCIBusTxMessageComboBox.Text.Length;
                SCIBusTxMessageAddButton.Text = "Edit";
            }
        }

        private void SCIBusTxMessageRepeatIntervalCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (SCIBusTxMessageRepeatIntervalCheckBox.Checked)
            {
                SCIBusTxMessageRepeatIntervalTextBox.Enabled = true;
                MillisecondsLabel05.Enabled = true;
                SCIBusTxMessageRepeatIncrementCheckBox.Enabled = true;
            }
            else
            {
                SCIBusTxMessageRepeatIntervalTextBox.Enabled = false;
                MillisecondsLabel05.Enabled = false;
                SCIBusTxMessageRepeatIncrementCheckBox.Enabled = false;
            }
        }

        private void SCIBusTxMessageRepeatIncrementCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (SCIBusTxMessageRepeatIncrementCheckBox.Checked)
            {
                SCIBusTxMessageRepeatIncrementTextBox.Enabled = true;
            }
            else
            {
                SCIBusTxMessageRepeatIncrementTextBox.Enabled = false;
            }
        }

        private void SCIBusModuleComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (SCIBusModuleComboBox.SelectedIndex)
            {
                case 0: // PCM
                    SCIBusFaultCodesLabel.Enabled = true;
                    SCIBusReadFaultCodesButton.Enabled = true;
                    SCIBusEraseFaultCodesButton.Enabled = true;
                    SCIBusOBD1EngineCableUsedCheckBox.Enabled = true;

                    if (PCM.state == "enabled")
                    {
                        if (PCM.speed == "976.5 baud") SCIBusSpeedComboBox.SelectedIndex = 1;
                        else if (PCM.speed == "7812.5 baud") SCIBusSpeedComboBox.SelectedIndex = 2;
                        else if (PCM.speed == "62500 baud") SCIBusSpeedComboBox.SelectedIndex = 3;
                        else if (PCM.speed == "125000 baud") SCIBusSpeedComboBox.SelectedIndex = 4;
                    }
                    else if (PCM.state == "disabled")
                    {
                        SCIBusSpeedComboBox.SelectedIndex = 0; // off
                    }

                    if (PCM.logic == "non-inverted") SCIBusOBD1EngineCableUsedCheckBox.Checked = false;
                    else if (PCM.logic == "inverted") SCIBusOBD1EngineCableUsedCheckBox.Checked = true;

                    if (PCM.configuration == "A") SCIBusOBDConfigurationComboBox.SelectedIndex = 0;
                    else if (PCM.configuration == "B") SCIBusOBDConfigurationComboBox.SelectedIndex = 1;

                    break;
                case 1: // TCM
                    SCIBusFaultCodesLabel.Enabled = false;
                    SCIBusReadFaultCodesButton.Enabled = false;
                    SCIBusEraseFaultCodesButton.Enabled = false;
                    SCIBusOBD1EngineCableUsedCheckBox.Enabled = false;

                    if (TCM.state == "enabled")
                    {
                        if (TCM.speed == "976.5 baud") SCIBusSpeedComboBox.SelectedIndex = 1;
                        else if (TCM.speed == "7812.5 baud") SCIBusSpeedComboBox.SelectedIndex = 2;
                        else if (TCM.speed == "62500 baud") SCIBusSpeedComboBox.SelectedIndex = 3;
                        else if (TCM.speed == "125000 baud") SCIBusSpeedComboBox.SelectedIndex = 4;
                    }
                    else if (TCM.state == "disabled")
                    {
                        SCIBusSpeedComboBox.SelectedIndex = 0; // off
                    }

                    if (TCM.logic == "non-inverted") SCIBusOBD1EngineCableUsedCheckBox.Checked = false;
                    else if (TCM.logic == "inverted") SCIBusOBD1EngineCableUsedCheckBox.Checked = true;

                    if (TCM.configuration == "A") SCIBusOBDConfigurationComboBox.SelectedIndex = 0;
                    else if (TCM.configuration == "B") SCIBusOBDConfigurationComboBox.SelectedIndex = 1;

                    break;
                default:
                    break;
            }
        }

        private void SCIBusTxMessageRepeatIntervalTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                switch (SCIBusModuleComboBox.SelectedIndex)
                {
                    case 0: // engine
                        SetRepeatedMessageBehavior(Packet.Target.pcm);
                        break;
                    case 1: // transmission
                        SetRepeatedMessageBehavior(Packet.Target.tcm);
                        break;
                }
            }
        }

        private void SCIBusTxMessageRepeatIncrementTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                switch (SCIBusModuleComboBox.SelectedIndex)
                {
                    case 0: // engine
                        SetRepeatedMessageBehavior(Packet.Target.pcm);
                        break;
                    case 1: // transmission
                        SetRepeatedMessageBehavior(Packet.Target.tcm);
                        break;
                }
            }
        }

        private async void SCIBusTxMessageComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;

                if (SCIBusTxMessageComboBox.Text.Length < 2) return;

                if (SCIBusTxMessageComboBox.Text.Contains(","))
                {
                    SCIBusTxMessageAddButton_Click(this, EventArgs.Empty);

                    if (!SCIBusTxMessageComboBox.Items.Contains(SCIBusTxMessageComboBox.Text)) // only add unique items (no repeat!)
                    {
                        SCIBusTxMessageComboBox.Items.Add(SCIBusTxMessageComboBox.Text); // add message to the list so it can be selected later
                    }

                    SCIBusTxMessageComboBox.Text = string.Empty;
                    return;
                }
                
                byte[] message = Util.HexStringToByte(SCIBusTxMessageComboBox.Text);

                if (SCIBusTxMessageAddButton.Text != "Edit")
                {
                    if (SCIBusTxMessageCalculateChecksumCheckBox.Checked)
                    {
                        byte checksum = 0;
                        int checksumLocation = message.Length - 1;

                        for (int i = 0; i < checksumLocation; i++)
                        {
                            checksum += message[i];
                        }

                        message[checksumLocation] = checksum;

                        SCIBusTxMessageComboBox.Text = Util.ByteToHexStringSimple(message);
                        SCIBusTxMessageComboBox.SelectionStart = SCIBusTxMessageComboBox.Text.Length;
                        SCIBusTxMessageComboBox.SelectionLength = 0;
                    }

                    switch (SCIBusModuleComboBox.SelectedIndex)
                    {
                        case 0: // engine
                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.pcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.single;
                            Packet.tx.payload = message;
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send an SCI-bus (PCM) message once:", Packet.tx.buffer);
                            if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (PCM) message Tx list:" + Environment.NewLine +
                                                                                   "       " + Util.ByteToHexStringSimple(message));
                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                        case 1: // transmission
                            Packet.tx.source = (byte)Packet.Source.device;
                            Packet.tx.target = (byte)Packet.Target.tcm;
                            Packet.tx.command = (byte)Packet.Command.msgTx;
                            Packet.tx.mode = (byte)Packet.MsgTxMode.single;
                            Packet.tx.payload = message;
                            Packet.GeneratePacket();
                            Util.UpdateTextBox(USBTextBox, "[<-TX] Send an SCI-bus (TCM) message once:", Packet.tx.buffer);
                            if (message.Length > 0) Util.UpdateTextBox(USBTextBox, "[INFO] SCI-bus (TCM) message Tx list:" + Environment.NewLine +
                                                                                   "       " + Util.ByteToHexStringSimple(message));
                            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);
                            break;
                    }
                }
                else
                {
                    if (SCIBusTxMessageCalculateChecksumCheckBox.Checked)
                    {
                        byte checksum = 0;
                        int checksumLocation = message.Length - 1;

                        for (int i = 0; i < checksumLocation; i++)
                        {
                            checksum += message[i];
                        }

                        message[checksumLocation] = checksum;

                        SCIBusTxMessageComboBox.Text = Util.ByteToHexStringSimple(message);
                        SCIBusTxMessageComboBox.SelectionStart = SCIBusTxMessageComboBox.Text.Length;
                        SCIBusTxMessageComboBox.SelectionLength = 0;
                    }

                    SCIBusTxMessageAddButton_Click(this, EventArgs.Empty);
                }

                if (!SCIBusTxMessageComboBox.Items.Contains(SCIBusTxMessageComboBox.Text)) // only add unique items (no repeat!)
                {
                    SCIBusTxMessageComboBox.Items.Add(SCIBusTxMessageComboBox.Text); // add message to the list so it can be selected later
                }
            }
        }

        #endregion

        #region Diagnostics tab

        private void DiagnosticsRefreshButton_Click(object sender, EventArgs e)
        {
            switch (DiagnosticsTabControl.SelectedTab.Name)
            {
                case "CCDBusDiagnosticsTabPage":
                    CCDBusDiagnosticsListBox.Items.Clear();
                    CCDBusDiagnosticsListBox.Items.AddRange(CCD.Diagnostics.Table.ToArray());
                    break;
                case "SCIBusPCMDiagnosticsTabPage":
                    SCIBusPCMDiagnosticsListBox.Items.Clear();
                    SCIBusPCMDiagnosticsListBox.Items.AddRange(PCM.Diagnostics.Table.ToArray());
                    break;
                case "SCIBusTCMDiagnosticsTabPage":
                    SCIBusTCMDiagnosticsListBox.Items.Clear();
                    SCIBusTCMDiagnosticsListBox.Items.AddRange(TCM.Diagnostics.Table.ToArray());
                    break;
                default:
                    break;
            }
        }

        private void DiagnosticsResetViewButton_Click(object sender, EventArgs e)
        {
            switch (DiagnosticsTabControl.SelectedTab.Name)
            {
                case "CCDBusDiagnosticsTabPage":
                    CCD.Diagnostics.IDByteList.Clear();
                    CCD.Diagnostics.UniqueIDByteList.Clear();
                    CCD.Diagnostics.B2F2IDByteList.Clear();
                    CCDTableBuffer.Clear();
                    CCDTableBufferLocation.Clear();
                    CCDTableRowCountHistory.Clear();
                    CCD.Diagnostics.lastUpdatedLine = 1;
                    CCD.Diagnostics.InitCCDTable();
                    CCDBusDiagnosticsListBox.Items.Clear();
                    CCDBusDiagnosticsListBox.Items.AddRange(CCD.Diagnostics.Table.ToArray());
                    break;
                case "SCIBusPCMDiagnosticsTabPage":
                    PCM.Diagnostics.IDByteList.Clear();
                    PCM.Diagnostics.UniqueIDByteList.Clear();
                    PCMTableBuffer.Clear();
                    PCMTableBufferLocation.Clear();
                    PCMTableRowCountHistory.Clear();
                    PCM.Diagnostics.lastUpdatedLine = 1;
                    PCM.Diagnostics.InitSCIPCMTable();
                    PCM.Diagnostics.InitRAMDumpTable();
                    PCM.Diagnostics.RAMDumpTableVisible = false;
                    PCM.Diagnostics.RAMTableAddress = 0;
                    SCIBusPCMDiagnosticsListBox.Items.Clear();
                    SCIBusPCMDiagnosticsListBox.Items.AddRange(PCM.Diagnostics.Table.ToArray());
                    break;
                case "SCIBusTCMDiagnosticsTabPage":
                    TCM.Diagnostics.IDByteList.Clear();
                    TCM.Diagnostics.UniqueIDByteList.Clear();
                    TCMTableBuffer.Clear();
                    TCMTableBufferLocation.Clear();
                    TCMTableRowCountHistory.Clear();
                    TCM.Diagnostics.lastUpdatedLine = 1;
                    TCM.Diagnostics.InitSCITCMTable();
                    //TCM.Diagnostics.InitRAMDumpTable();
                    //TCM.Diagnostics.RAMDumpTableVisible = false;
                    //TCM.Diagnostics.RAMTableAddress = 0;
                    SCIBusTCMDiagnosticsListBox.Items.Clear();
                    SCIBusTCMDiagnosticsListBox.Items.AddRange(TCM.Diagnostics.Table.ToArray());
                    break;
                default:
                    break;
            }
        }

        private void DiagnosticsCopyToClipboardButton_Click(object sender, EventArgs e)
        {
            Clipboard.Clear();

            switch (DiagnosticsTabControl.SelectedTab.Name)
            {
                case "CCDBusDiagnosticsTabPage":
                    Clipboard.SetText(string.Join(Environment.NewLine, CCD.Diagnostics.Table.ToArray()) + Environment.NewLine);
                    break;
                case "SCIBusPCMDiagnosticsTabPage":
                    Clipboard.SetText(string.Join(Environment.NewLine, PCM.Diagnostics.Table.ToArray()) + Environment.NewLine);
                    break;
                case "SCIBusTCMDiagnosticsTabPage":
                    Clipboard.SetText(string.Join(Environment.NewLine, TCM.Diagnostics.Table.ToArray()) + Environment.NewLine);
                    break;
                default:
                    break;
            }
        }

        private void DiagnosticsSnapshotButton_Click(object sender, EventArgs e)
        {
            string DateTimeNow;
            int counter;

            switch (DiagnosticsTabControl.SelectedTab.Name)
            {
                case "CCDBusDiagnosticsTabPage":
                    DateTimeNow = DateTime.Now.ToString("yyyyMMdd_HHmmss"); // new time for each snapshot
                    string CCDDiagnosticsSnapshotFilename = @"LOG/CCD/ccdsnapshot_" + DateTimeNow;
                    counter = 1;

                    while (File.Exists(CCDDiagnosticsSnapshotFilename + ".txt"))
                    {
                        if (CCDDiagnosticsSnapshotFilename.Length > 35) CCDDiagnosticsSnapshotFilename = CCDDiagnosticsSnapshotFilename.Remove(CCDDiagnosticsSnapshotFilename.Length - 3, 3); // remove last 3 characters
                        CCDDiagnosticsSnapshotFilename += "_";
                        if (counter < 10) CCDDiagnosticsSnapshotFilename += "0";
                        CCDDiagnosticsSnapshotFilename += counter.ToString();
                        counter++;
                    }

                    CCDDiagnosticsSnapshotFilename += ".txt";
                    File.AppendAllText(CCDDiagnosticsSnapshotFilename, string.Join(Environment.NewLine, CCD.Diagnostics.Table.ToArray()) + Environment.NewLine);
                    break;
                case "SCIBusPCMDiagnosticsTabPage":
                    DateTimeNow = DateTime.Now.ToString("yyyyMMdd_HHmmss"); // new time for each snapshot
                    string PCMDiagnosticsSnapshotFilename = @"LOG/PCM/pcmsnapshot_" + DateTimeNow;
                    counter = 1;

                    while (File.Exists(PCMDiagnosticsSnapshotFilename + ".txt"))
                    {
                        if (PCMDiagnosticsSnapshotFilename.Length > 35) PCMDiagnosticsSnapshotFilename = PCMDiagnosticsSnapshotFilename.Remove(PCMDiagnosticsSnapshotFilename.Length - 3, 3); // remove last 3 characters
                        PCMDiagnosticsSnapshotFilename += "_";
                        if (counter < 10) PCMDiagnosticsSnapshotFilename += "0";
                        PCMDiagnosticsSnapshotFilename += counter.ToString();
                        counter++;
                    }

                    PCMDiagnosticsSnapshotFilename += ".txt";
                    File.AppendAllText(PCMDiagnosticsSnapshotFilename, string.Join(Environment.NewLine, PCM.Diagnostics.Table.ToArray()) + Environment.NewLine);
                    break;
                case "SCIBusTCMDiagnosticsTabPage":
                    DateTimeNow = DateTime.Now.ToString("yyyyMMdd_HHmmss"); // new time for each snapshot
                    string TCMDiagnosticsSnapshotFilename = @"LOG/TCM/tcmsnapshot_" + DateTimeNow;
                    counter = 1;

                    while (File.Exists(TCMDiagnosticsSnapshotFilename + ".txt"))
                    {
                        if (TCMDiagnosticsSnapshotFilename.Length > 35) TCMDiagnosticsSnapshotFilename = TCMDiagnosticsSnapshotFilename.Remove(TCMDiagnosticsSnapshotFilename.Length - 3, 3); // remove last 3 characters
                        TCMDiagnosticsSnapshotFilename += "_";
                        if (counter < 10) TCMDiagnosticsSnapshotFilename += "0";
                        TCMDiagnosticsSnapshotFilename += counter.ToString();
                        counter++;
                    }

                    TCMDiagnosticsSnapshotFilename += ".txt";
                    File.AppendAllText(TCMDiagnosticsSnapshotFilename, string.Join(Environment.NewLine, TCM.Diagnostics.Table.ToArray()) + Environment.NewLine);
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region LCD tab

        private async void LCDApplySettingsButton_Click(object sender, EventArgs e)
        {
            byte LCDState = (byte)LCDStateComboBox.SelectedIndex;

            bool success = byte.TryParse(Util.HexStringToByte(LCDI2CAddressTextBox.Text)[0].ToString(), out byte LCDI2CAddress);
            if (!success)
            {
                LCDI2CAddress = 0x27;
                LCDI2CAddressTextBox.Text = "27";
            }

            success = byte.TryParse(LCDWidthTextBox.Text, out byte LCDWidth);
            if (!success)
            {
                LCDWidth = 20;
                LCDWidthTextBox.Text = "20";
            }

            success = byte.TryParse(LCDHeightTextBox.Text, out byte LCDHeight);
            if (!success)
            {
                LCDHeight = 4;
                LCDHeightTextBox.Text = "4";
            }

            success = byte.TryParse(LCDRefreshRateTextBox.Text, out byte LCDRefreshRate);
            if (!success)
            {
                LCDRefreshRate = 20;
                LCDRefreshRateTextBox.Text = "20";
            }

            byte LCDUnits;
            if (Properties.Settings.Default.Units == "imperial") LCDUnits = 0;
            else if (Properties.Settings.Default.Units == "metric") LCDUnits = 1;
            else LCDUnits = 0;

            byte LCDDataSource = (byte)(LCDDataSourceComboBox.SelectedIndex + 1);

            Packet.tx.source = (byte)Packet.Source.device;
            Packet.tx.target = (byte)Packet.Target.device;
            Packet.tx.command = (byte)Packet.Command.settings;
            Packet.tx.mode = (byte)Packet.SettingsMode.setLCD;
            Packet.tx.payload = new byte[7] { LCDState, LCDI2CAddress, LCDWidth, LCDHeight, LCDRefreshRate, LCDUnits, LCDDataSource };
            Packet.GeneratePacket();
            Util.UpdateTextBox(USBTextBox, "[<-TX] Change LCD settings:", Packet.tx.buffer);
            await SerialPortExtension.WritePacketAsync(Packet.Serial, Packet.tx.buffer);

            UpdateLCDPreviewTextBox();
        }

        private void LCDDataSourceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (LCDDataSourceComboBox.SelectedIndex == 2)
            {
                MessageBox.Show("Currently the transmission controller cannot be selected as LCD data source.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LCDDataSourceComboBox.SelectedIndex = 1;
            }
        }

        private void UpdateLCDPreviewTextBox()
        {
            switch (LCDStateComboBox.SelectedIndex)
            {
                case 0: // disabled
                    if ((LCDWidthTextBox.Text == "20") && (LCDHeightTextBox.Text == "4"))
                    {
                        LCDPreviewTextBox.Clear();
                        LCDPreviewTextBox.AppendText("--------------------" + Environment.NewLine);
                        LCDPreviewTextBox.AppendText("  CHRYSLER CCD/SCI  " + Environment.NewLine);
                        LCDPreviewTextBox.AppendText("   SCANNER VX.XX    " + Environment.NewLine);
                        LCDPreviewTextBox.AppendText("--------------------");
                    }
                    else if ((LCDWidthTextBox.Text == "16") && (LCDHeightTextBox.Text == "2"))
                    {
                        LCDPreviewTextBox.Clear();
                        LCDPreviewTextBox.AppendText("CHRYSLER CCD/SCI" + Environment.NewLine);
                        LCDPreviewTextBox.AppendText(" SCANNER VX.XX  ");
                    }
                    else
                    {
                        LCDPreviewTextBox.Clear();
                        LCDPreviewTextBox.AppendText("CCD/SCI");
                    }
                    break;
                case 1: // enabled
                    if ((LCDWidthTextBox.Text == "20") && (LCDHeightTextBox.Text == "4"))
                    {
                        if (Properties.Settings.Default.Units == "imperial")
                        {
                            LCDPreviewTextBox.Clear();
                            LCDPreviewTextBox.AppendText("  0mph     0rpm   0%" + Environment.NewLine);
                            LCDPreviewTextBox.AppendText("  0/  0°F     0.0psi" + Environment.NewLine);
                            LCDPreviewTextBox.AppendText(" 0.0/ 0.0V          " + Environment.NewLine);
                            LCDPreviewTextBox.AppendText("     0.000mi        ");
                        }
                        else if (Properties.Settings.Default.Units == "metric")
                        {
                            LCDPreviewTextBox.Clear();
                            LCDPreviewTextBox.AppendText("  0km/h    0rpm   0%" + Environment.NewLine);
                            LCDPreviewTextBox.AppendText("  0/  0°C     0.0kPa" + Environment.NewLine);
                            LCDPreviewTextBox.AppendText(" 0.0/ 0.0V          " + Environment.NewLine);
                            LCDPreviewTextBox.AppendText("     0.000km        ");
                        }
                    }
                    else if ((LCDWidthTextBox.Text == "16") && (LCDHeightTextBox.Text == "2"))
                    {
                        if (Properties.Settings.Default.Units == "imperial")
                        {
                            LCDPreviewTextBox.Clear();
                            LCDPreviewTextBox.AppendText("  0mph      0rpm" + Environment.NewLine);
                            LCDPreviewTextBox.AppendText("  0°F     0.0psi");
                        }
                        else if (Properties.Settings.Default.Units == "metric")
                        {
                            LCDPreviewTextBox.Clear();
                            LCDPreviewTextBox.AppendText("  0km/h     0rpm" + Environment.NewLine);
                            LCDPreviewTextBox.AppendText("  0°C     0.0kPa");
                        }
                    }
                    else
                    {
                        LCDPreviewTextBox.Clear();
                        LCDPreviewTextBox.AppendText("    0rpm");
                    }
                    break;
            }
        }

        #endregion

        #region Menu strip

        private void UpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Uri GUIAssemblyInfoFile = new Uri("https://raw.githubusercontent.com/laszlodaniel/ChryslerCCDSCIScanner/master/GUI/ChryslerCCDSCIScanner/Properties/AssemblyInfo.cs");
            Uri GUIZIPDownload = new Uri("https://github.com/laszlodaniel/ChryslerCCDSCIScanner/raw/master/GUI/ChryslerCCDSCIScanner/bin/Debug/ChryslerCCDSCIScanner_GUI.zip");
            Uri FWSourceFile = new Uri("https://raw.githubusercontent.com/laszlodaniel/ChryslerCCDSCIScanner/master/Arduino/ChryslerCCDSCIScanner/ChryslerCCDSCIScanner.ino");
            Uri FWFlashFile = new Uri("https://raw.githubusercontent.com/laszlodaniel/ChryslerCCDSCIScanner/master/Arduino/ChryslerCCDSCIScanner/ChryslerCCDSCIScanner.ino.mega.hex");

            // First check if GUI update is available.
            // Download the latest AssemblyInfo.cs file from GitHub and compare version numbers.
            if (!Directory.Exists("Update")) Directory.CreateDirectory("Update"); // create Update folder first if it doesn't exist

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            try
            {
                Downloader.DownloadFile(GUIAssemblyInfoFile, @"Update/AssemblyInfo.cs");
            }
            catch
            {
                MessageBox.Show("GUI update availability cannot be checked.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (File.Exists(@"Update/AssemblyInfo.cs"))
            {
                // Get latest version number from the downloaded file.
                string line = string.Empty;
                bool done = false;
                using (Stream stream = File.Open(@"Update/AssemblyInfo.cs", FileMode.Open))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (!done)
                        {
                            Thread.Sleep(1);
                            
                            line = reader.ReadLine();

                            if (line != null)
                            {
                                if (line.StartsWith("[assembly: AssemblyVersion"))
                                {
                                    done = true;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                if (line != null)
                {
                    string latestGUIVersionString = "v" + line.Substring(28, line.IndexOf(")") - 29);

                    if (latestGUIVersionString == GUIVersion)
                    {
                        MessageBox.Show("You are using the latest GUI version.", "No GUI update available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        if (MessageBox.Show("Latest GUI version: " + latestGUIVersionString + Environment.NewLine +
                                            "Current GUI version: " + GUIVersion + Environment.NewLine +
                                            "There is a new GUI version available." + Environment.NewLine +
                                            "Do you want to download it?", "GUI update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                        {
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            try
                            {
                                //Downloader.DownloadProgressChanged += new DownloadProgressChangedEventHandler(Downloader_DownloadProgressChanged);
                                //Downloader.DownloadFileCompleted += new AsyncCompletedEventHandler(Downloader_DownloadFileCompleted);
                                //Downloader.QueryString.Add("ChryslerCCDSCIScanner_V14X_GUI.zip", "ChryslerCCDSCIScanner_V14X_GUI");
                                Downloader.DownloadFile(GUIZIPDownload, @"Update/ChryslerCCDSCIScanner_GUI.zip");
                                MessageBox.Show("Updated GUI download finished." + Environment.NewLine +
                                                "Close this application and unpack the .zip-file from the Update folder!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch
                            {
                                MessageBox.Show("GUI download error.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show("GUI update cancelled.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("GUI download error." + Environment.NewLine + "Please download the latest GUI .zip file from GitHub and overwrite the old executable file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                File.Delete(@"Update/AssemblyInfo.cs");
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            try
            {
                Downloader.DownloadFile(FWSourceFile, @"Update/ChryslerCCDSCIScanner.ino");
            }
            catch
            {
                MessageBox.Show("Firmware update availability cannot be checked.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (File.Exists(@"Update/ChryslerCCDSCIScanner.ino") && deviceFound)
            {
                // Get new version/UNIX time value from the downloaded file
                string line = string.Empty;
                bool done = false;
                using (Stream stream = File.Open(@"Update/ChryslerCCDSCIScanner.ino", FileMode.Open))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (!done)
                        {
                            Thread.Sleep(1);
                            
                            line = reader.ReadLine();

                            if (line != null)
                            {
                                if (line.Contains("#define FW_VERSION") || line.Contains("#define FW_DATE"))
                                {
                                    done = true;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                if (line != null)
                {
                    UInt32 ver = 0;

                    if (line.StartsWith("#define FW_DATE"))
                    {
                        string hexline = line.Substring(16, 10);
                        ver = Convert.ToUInt32(hexline, 16);
                    }
                    else if (line.StartsWith("#define FW_VERSION"))
                    {
                        string hexline = line.Substring(19, 10);
                        ver = Convert.ToUInt32(hexline, 16);
                    }

                    byte major = (byte)(ver >> 24);
                    byte minor = (byte)(ver >> 16);
                    byte patch = (byte)(ver >> 8);
                    string latestFWVersionString = "v" + major.ToString("0") + "." + minor.ToString("0") + "." + patch.ToString("0");

                    if (latestFWVersionString == FWVersion)
                    {
                        MessageBox.Show("The diagnostic scanner uses the latest firmware version.", "No firmware update available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        if (MessageBox.Show("Latest firmware version: " + latestFWVersionString + Environment.NewLine +
                                            "Current firmware version: " + FWVersion + Environment.NewLine +
                                            "There is a new device firmware version available." + Environment.NewLine +
                                            "Do you want to update the device?", "Device firmware update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                        {
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            try
                            {
                                Downloader.DownloadFile(FWFlashFile, @"Tools/ChryslerCCDSCIScanner.ino.mega.hex");
                            }
                            catch
                            {
                                MessageBox.Show("Firmware download error.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }

                            ConnectButton.PerformClick(); // disconnect
                            Thread.Sleep(500); // wait until UI updates its controls
                            this.Refresh();
                            Process process = new Process();
                            process.StartInfo.WorkingDirectory = "Tools";
                            process.StartInfo.FileName = "avrdude.exe";
                            process.StartInfo.Arguments = "-C avrdude.conf -p m2560 -c wiring -P " + selectedPort + " -b 115200 -D -U flash:w:ChryslerCCDSCIScanner.ino.mega.hex:i";
                            process.Start();
                            process.WaitForExit();
                            this.Refresh();
                            File.Delete(@"Tools/ChryslerCCDSCIScanner.ino.mega.hex");
                            MessageBox.Show("Device firmware update finished." + Environment.NewLine +
                                            "Connect again manually.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            FWVersion = latestFWVersionString;
                        }
                        else
                        {
                            MessageBox.Show("Device firmware update cancelled.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Firmware download error." + Environment.NewLine + "Please download the latest .hex flash file from GitHub and perform a manual update." + Environment.NewLine + "For more information check the \"Tools\\update.txt\" file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                File.Delete(@"Update/ChryslerCCDSCIScanner.ino");
            }
            else if (!deviceFound)
            {
                File.Delete(@"Update/ChryslerCCDSCIScanner.ino");
                MessageBox.Show("Device firmware update cannot be checked." + Environment.NewLine +
                                "Connect to the device and try again!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Downloader_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            // TODO
            return;
        }

        private void Downloader_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            // TODO
            return;
        }

        private void ReadMemoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ReadMemory == null)
            {
                ReadMemory = new ReadMemoryForm(this)
                {
                    StartPosition = FormStartPosition.CenterParent
                };

                ReadMemory.FormClosed += delegate { ReadMemory = null; };
                ReadMemory.Show();
            }
            else
            {
                ReadMemory.WindowState = FormWindowState.Normal;
                ReadMemory.Focus();
            }
        }

        private void WriteMemoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (WriteMemory == null)
            {
                WriteMemory = new WriteMemoryForm(this)
                {
                    StartPosition = FormStartPosition.CenterParent
                };

                WriteMemory.FormClosed += delegate { WriteMemory = null; };
                WriteMemory.Show();
            }
            else
            {
                WriteMemory.WindowState = FormWindowState.Normal;
                WriteMemory.Focus();
            }
        }

        private void MetricUnitsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImperialUnitsToolStripMenuItem.Checked = false;
            MetricUnitsToolStripMenuItem.Checked = true;

            Properties.Settings.Default.Units = "metric";
            Properties.Settings.Default.Save(); // save setting in application configuration file

            if (WriteMemory != null) WriteMemory.UpdateMileageUnit();
        }

        private void ImperialUnitsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImperialUnitsToolStripMenuItem.Checked = true;
            MetricUnitsToolStripMenuItem.Checked = false;

            Properties.Settings.Default.Units = "imperial";
            Properties.Settings.Default.Save(); // save setting in application configuration file

            if (WriteMemory != null) WriteMemory.UpdateMileageUnit();
        }

        private void IncludeTimestampInLogFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IncludeTimestampInLogFilesToolStripMenuItem.Checked)
            {
                Properties.Settings.Default.Timestamp = true;
                Properties.Settings.Default.Save(); // save setting in application configuration file
                includeTimestampInLogFiles = true;
            }
            else
            {
                Properties.Settings.Default.Timestamp = false;
                Properties.Settings.Default.Save(); // save setting in application configuration file
                includeTimestampInLogFiles = false;
            }
        }

        private void CCDBusOnDemandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CCDBusOnDemandToolStripMenuItem.Checked)
            {
                Properties.Settings.Default.CCDBusOnDemand = true;
                Properties.Settings.Default.Save(); // save setting in application configuration file
            }
            else
            {
                Properties.Settings.Default.CCDBusOnDemand = false;
                Properties.Settings.Default.Save(); // save setting in application configuration file
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About = new AboutForm(this)
            {
                StartPosition = FormStartPosition.CenterParent
            };

            About.FormClosed += delegate { About = null; };
            About.ShowDialog();
        }

        #endregion
    }

    public static class StringExt
    {
        public static bool IsNumeric(this string text) => double.TryParse(text, out _);
    }
}
