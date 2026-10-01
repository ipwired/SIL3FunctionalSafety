using System;
using System.Drawing;
using System.Threading.Tasks;
using TwinCAT.Ads;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowsFormsAppRZT2FUSA
{
    public partial class Form1 : Form
    {
        private Panel dashboardPanel;

        private Label title;

        // Speed gauge is a custom control that displays the motor speed in RPM.
        private SpeedGauge speedGauge;

        // Shows the DC bus voltage in volts
        private Label lblVoltage;

        // Shows the safety state of the motor
        private Label lblSafety;

        // Shows the description of the safety state
        private Label lblSafetyDescription;

        // Shows the motor status (running or stopped)
        private Label lblMotorStatus;

        // Shows the rotor position in degrees.
        private RotorPosition rotorPosition;

        // Shows the rotor position angle in degrees
        private Label lblPositionAngle;

        // Shows the system log messages
        private TextBox txtLog;

        // Start button to initiate the motor start sequence
        private Button btnStart;

        private AdsClient adsClient;
        private uint hControlword;
        private uint hTargetVelocity;
        private uint hErrAckIn;
        private uint hStatusword;
        private uint hErrorCode;
        private uint hModeDisplay;
        private uint hActualPosition;
        private uint hActualVelocity;

        private bool adsConnected = false;
        private bool motorRunning = false;
        private bool startInProgress = false;
        private Timer adsPollTimer;

        private const int PLC_ADS_PORT = 851;
        private const string ADS_ADDRESS = "127.0.0.1";

        private const string CONTROLWORD = "MAIN.wControlword";
        private const string TARGET_VELOCITY = "MAIN.diTargetVelocity";
        private const string ERROR_ACK = "MAIN.bERRAckIn";

        // Below values must be linked in TwinCAT to the corresponding EtherCAT input PDOs before real feedback appears.
        private const string STATUSWORD = "MAIN.wStatusword";
        private const string ERROR_CODE = "MAIN.wErrorCode";
        private const string MODE_DISPLAY = "MAIN.byModeDisplay";
        private const string ACTUAL_POSITION = "MAIN.diActualPosition";
        private const string ACTUAL_VELOCITY = "MAIN.diActualVelocity";

        // Screen scaling factor for high-DPI monitors. This allows the HMI to scale up the interface elements for better visibility on larger screens.
        private const float DESIGN_SCALE = 1.5f;

        private const int BASE_WIDTH = 1220;

        private const int BASE_HEIGHT = 630;

        public Form1()
        {
            InitializeComponent();
            SetupFullScreen();
            BuildInterface();
        }

        private void SetupFullScreen()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = false;
            BackColor = Color.FromArgb(3, 8, 13);
            KeyPreview = true;
            KeyDown += Form1_KeyDown;
        }

        // Pressing the Escape key will close the application. This is useful for quickly exiting the full-screen HMI interface during testing or demonstration.
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        // Scale a value based on the DESIGN_SCALE factor. This is used to scale the size of interface elements for high-DPI displays.
        private int S(int value)
        {
            return (int)Math.Round(value * DESIGN_SCALE);
        }

        // Create a Font object with the specified family, size, and style, scaled by the DESIGN_SCALE factor. This is used to create consistent font sizes across the HMI interface.
        private Font F(string family, float size, FontStyle style)
        {
            return new Font(family, size * DESIGN_SCALE, style);
        }

        private void BuildInterface()
        {
            Text = "RENESAS RZ/T2M RZ/T2L FUNCTIONAL SAFETY SYSTEM";
            BackColor = Color.FromArgb(3, 8, 13);
            dashboardPanel = new Panel();
            dashboardPanel.Size = new Size(S(BASE_WIDTH), S(BASE_HEIGHT));
            dashboardPanel.BackColor = Color.Transparent;
            Controls.Add(dashboardPanel);

            title = new Label();
            title.Text = "RENESAS  RZ/T2M RZ/T2L FUNCTIONAL SAFETY SYSTEM";
            title.Font = F("Arial", 23, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Location = new Point(0, 0);
            title.Size = new Size(S(1220), S(55));
            dashboardPanel.Controls.Add(title);

            Panel speedPanel = CreatePanel(0, 70, 350, 380);
            AddTitle(speedPanel, "MOTOR SPEED");

            speedGauge = new SpeedGauge();
            speedGauge.Minimum = 0;
            speedGauge.Maximum = 6000;
            speedGauge.Size = new Size(S(300), S(300));
            speedGauge.Location = new Point((speedPanel.ClientSize.Width - speedGauge.Width) / 2, S(55));
            speedPanel.Controls.Add(speedGauge);
            dashboardPanel.Controls.Add(speedPanel);

            Panel dcPanel = CreatePanel(640, 70, 250, 180);
            AddTitle(dcPanel, "DC BUS");

            lblVoltage = CreateLargeValue("24.0 V", Color.DeepSkyBlue);
            lblVoltage.Size = new Size(S(230), S(60));
            lblVoltage.Location = new Point((dcPanel.ClientSize.Width - lblVoltage.Width) / 2, S(60));
            dcPanel.Controls.Add(lblVoltage);

            Label dcText = CreateDescription("DC VOLTAGE");
            dcText.Size = new Size(S(230), S(30));
            dcText.Location = new Point((dcPanel.ClientSize.Width - dcText.Width) / 2, S(125));
            dcPanel.Controls.Add(dcText);
            dashboardPanel.Controls.Add(dcPanel);

            Panel positionPanel = CreatePanel(370, 70, 250, 180);
            AddTitle(positionPanel, "POSITION");

            rotorPosition = new RotorPosition();
            rotorPosition.Size = new Size(S(120), S(120));
            rotorPosition.Location = new Point((positionPanel.ClientSize.Width - rotorPosition.Width) / 2, S(42));
            positionPanel.Controls.Add(rotorPosition);

            lblPositionAngle = new Label();
            lblPositionAngle.Text = "0.0°";
            lblPositionAngle.Font = F("Arial", 10, FontStyle.Bold);
            lblPositionAngle.ForeColor = Color.Wheat;
            lblPositionAngle.TextAlign = ContentAlignment.BottomRight;
            lblPositionAngle.Size = new Size(S(230), S(30));
            lblPositionAngle.Location = new Point((positionPanel.ClientSize.Width - lblPositionAngle.Width) / 2, S(145));
            positionPanel.Controls.Add(lblPositionAngle);
            dashboardPanel.Controls.Add(positionPanel);

            Panel safetyPanel = CreatePanel(910, 70, 290, 180);
            AddTitle(safetyPanel, "SAFETY STATE");

            lblSafety = CreateLargeValue("STO ACTIVE", Color.Red);
            lblSafety.Font = F("Arial", 23, FontStyle.Bold);
            lblSafety.Size = new Size(S(270), S(55));
            lblSafety.Location = new Point((safetyPanel.ClientSize.Width - lblSafety.Width) / 2, S(55));
            safetyPanel.Controls.Add(lblSafety);

            lblSafetyDescription = CreateDescription("Safe Torque Off");
            lblSafetyDescription.Size = new Size(S(270), S(30));
            lblSafetyDescription.Location = new Point((safetyPanel.ClientSize.Width - lblSafetyDescription.Width) / 2, S(120));
            safetyPanel.Controls.Add(lblSafetyDescription);
            dashboardPanel.Controls.Add(safetyPanel);

            Panel motorPanel = CreatePanel(370, 270, 250, 180);
            AddTitle(motorPanel, "MOTOR STATUS");

            lblMotorStatus = new Label();
            lblMotorStatus.Text = "●  MOTOR STOPPED";
            lblMotorStatus.Font = F("Arial", 17, FontStyle.Bold);
            lblMotorStatus.ForeColor = Color.Red;
            lblMotorStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblMotorStatus.Size = new Size(S(230), S(50));
            lblMotorStatus.Location = new Point((motorPanel.ClientSize.Width - lblMotorStatus.Width) / 2, S(60));
            motorPanel.Controls.Add(lblMotorStatus);

            Label motorIcon = new Label();
            motorIcon.Text = "⚙";
            motorIcon.Font = F("Arial", 48, FontStyle.Regular);
            motorIcon.ForeColor = Color.LightGray;
            motorIcon.TextAlign = ContentAlignment.MiddleCenter;
            motorIcon.Size = new Size(S(230), S(65));
            motorIcon.Location = new Point((motorPanel.ClientSize.Width - motorIcon.Width) / 2, S(100));
            motorPanel.Controls.Add(motorIcon);
            dashboardPanel.Controls.Add(motorPanel);

            Panel logPanel = CreatePanel(640, 270, 560, 180);
            AddTitle(logPanel, "SYSTEM LOG");

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.FromArgb(2, 8, 12);
            txtLog.ForeColor = Color.LimeGreen;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Font = F("Consolas", 10, FontStyle.Bold);
            txtLog.Size = new Size(S(530), S(120));
            txtLog.Location = new Point((logPanel.ClientSize.Width - txtLog.Width) / 2, S(45));
            logPanel.Controls.Add(txtLog);
            dashboardPanel.Controls.Add(logPanel);

            btnStart = new Button();
            btnStart.Text = "START SYSTEM";
            btnStart.Font = F("Arial", 25, FontStyle.Bold);
            btnStart.ForeColor = Color.White;
            btnStart.BackColor = Color.FromArgb(0, 175, 0);
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.FlatAppearance.BorderColor = Color.LimeGreen;
            btnStart.FlatAppearance.BorderSize = S(2);
            btnStart.Size = new Size(S(550), S(100));
            btnStart.Cursor = Cursors.Hand;
            btnStart.Location = new Point((dashboardPanel.ClientSize.Width - btnStart.Width) / 2, S(500));
            btnStart.Click += BtnStart_Click;
            dashboardPanel.Controls.Add(btnStart);

            AddLog("System Ready");
            AddLog("STO Active");
            AddLog("Motor Inactive");
            AddLog("Waiting For Start");

            adsPollTimer = new Timer();
            adsPollTimer.Interval = 100;
            adsPollTimer.Tick += AdsPollTimer_Tick;
            adsPollTimer.Start();

            // Connect after the HMI has been created so any ADS/EtherCAT error can be shown in the System Log instead of terminating the Windows Forms application.
            ConnectAds();

            CenterDashboard();

            Resize += Form1_Resize;
            FormClosing += Form1_FormClosing;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopAds();
        }

        private Panel CreatePanel(int x, int y, int width, int height)
        {
            Panel panel = new Panel();
            panel.Location = new Point(S(x), S(y));
            panel.Size = new Size(S(width), S(height));
            panel.BackColor = Color.FromArgb(8, 16, 24);
            panel.BorderStyle = BorderStyle.FixedSingle;

            return panel;
        }

        private void AddTitle(Panel panel, string text)
        {
            Label panelTitle = new Label();
            panelTitle.Text = text;
            panelTitle.Font = F("Arial", 11, FontStyle.Bold);
            panelTitle.ForeColor = Color.White;
            panelTitle.TextAlign = ContentAlignment.MiddleCenter;
            panelTitle.Location = new Point(S(5), S(5));
            panelTitle.Size = new Size(panel.Width - S(10), S(30));

            panel.Controls.Add(panelTitle);
        }


        private Label CreateLargeValue(string text, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = F("Arial", 30, FontStyle.Bold);
            label.ForeColor = color;
            label.TextAlign = ContentAlignment.MiddleCenter;

            return label;
        }

        private Label CreateDescription(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = F("Arial", 11, FontStyle.Bold);
            label.ForeColor = Color.LightGray;
            label.TextAlign = ContentAlignment.MiddleCenter;

            return label;
        }

        private void CenterDashboard()
        {
            if (dashboardPanel == null) return;

            int x = (ClientSize.Width - dashboardPanel.Width) / 2;
            int y = (ClientSize.Height - dashboardPanel.Height) / 2;

            // If the monitor is smaller than the dashboard, keep the dashboard pinned to the top-left
            if (x < 0) x = 0;
            if (y < 0) y = 0;

            dashboardPanel.Location = new Point(x, y);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            CenterDashboard();
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (startInProgress) return;

            startInProgress = true;
            btnStart.Enabled = false;
            btnStart.Text = "STARTING...";

            try
            {
                AddLog("START SYSTEM requested");

                // CASE 1: ADS / TwinCAT is not available
                if (!adsConnected)
                {
                    AddLog("TwinCAT ADS not connected.");
                    AddLog("Attempting connection to ADS Runtime 1...");

                    ConnectAds();

                    if (!adsConnected)
                    {
                        AddLog("No TwinCAT ADS connection");
                        AddLog("EtherCAT motor not connected");
                        SetOfflineState("NO ETHERCAT");
                        return;
                    }

                    AddLog("TwinCAT ADS connection established.");
                }

                // CASE 2: PLC Runtime is not RUN
                StateInfo state;
                try
                {
                    state = adsClient.ReadState();
                }
                catch (Exception ex)
                {
                    AddLog("ADS read failed: " + ShortError(ex));
                    SetOfflineState("ADS ERROR");
                    return;
                }

                if (state.AdsState != AdsState.Run)
                {
                    AddLog("PLC Runtime is not RUN");
                    AddLog("ADS state = " + state.AdsState);
                    SetOfflineState("PLC NOT RUN");
                    return;
                }

                // CASE 3: Required PLC command symbols are missing
                if (hControlword == 0 || hTargetVelocity == 0 || hErrAckIn == 0)
                {
                    AddLog("Required ADS symbols are missing");
                    AddLog("Check MAIN.wControlword / MAIN.diTargetVelocity / MAIN.bERRAckIn");
                    SetOfflineState("SYMBOL ERROR");
                    return;
                }

                // MOTOR START SEQUENCE
                AddLog("TwinSAFE acknowledge");
                await PulseTwinSafeErrorAckAsync();

                AddLog("Controlword = 128");
                adsClient.WriteAny(hControlword, (ushort)0x0080);
                await Task.Delay(100);

                AddLog("Controlword = 0");
                adsClient.WriteAny(hControlword, (ushort)0x0000);
                await Task.Delay(200);

                AddLog("Controlword = 6");
                adsClient.WriteAny(hControlword, (ushort)0x0006);
                await Task.Delay(200);

                AddLog("Controlword = 7");
                adsClient.WriteAny(hControlword, (ushort)0x0007);
                await Task.Delay(200);

                AddLog("Controlword = 15");
                adsClient.WriteAny(hControlword, (ushort)0x000F);
                await Task.Delay(200);

                AddLog("Velocity = 333");
                adsClient.WriteAny(hTargetVelocity, 333);

                motorRunning = true;
                btnStart.Text = "SYSTEM RUNNING";
                AddLog("Motor start command completed");

                if (hActualVelocity == 0) AddLog("Actual velocity feedback unavailable");
            }
            catch (Exception ex)
            {
                motorRunning = false;
                AddLog("START FAILED: " + ShortError(ex));
                SetOfflineState("START ERROR");
            }
            finally
            {
                startInProgress = false;
                if (!IsDisposed && !Disposing) btnStart.Enabled = true;
            }
        }

        // ============================================================
        // CONNECT TO TWINCAT / ADS
        // ============================================================

        private void ConnectAds()
        {
            try
            {
                adsClient = new AdsClient();
                adsClient.Connect(PLC_ADS_PORT);

                StateInfo state = adsClient.ReadState();
                adsConnected = true;

                AddLog("ADS connected: 127.0.0.1:851");
                AddLog("PLC ADS state: " + state.AdsState);

                CreateRequiredHandle(CONTROLWORD, ref hControlword);
                CreateRequiredHandle(TARGET_VELOCITY, ref hTargetVelocity);
                CreateRequiredHandle(ERROR_ACK, ref hErrAckIn);

                // Run even when the feedback PDO symbols have not yet been added.
                CreateOptionalHandle(STATUSWORD, ref hStatusword);
                CreateOptionalHandle(ERROR_CODE, ref hErrorCode);
                CreateOptionalHandle(MODE_DISPLAY, ref hModeDisplay);
                CreateOptionalHandle(ACTUAL_POSITION, ref hActualPosition);
                CreateOptionalHandle(ACTUAL_VELOCITY, ref hActualVelocity);

                if (hActualVelocity == 0)
                {
                    AddLog("Actual velocity feedback: NOT LINKED");
                }
                else
                {
                    AddLog("Actual velocity feedback: available");
                }

                SetReadyState();
            }
            catch (Exception ex)
            {
                adsConnected = false;
                AddLog("ADS connection unavailable");
                AddLog("EtherCAT offline: " + ShortError(ex));
                SetOfflineState("NO ETHERCAT");
            }
        }

        private void CreateRequiredHandle(string symbol, ref uint handle)
        {
            try
            {
                handle = adsClient.CreateVariableHandle(symbol);
            }
            catch (Exception ex)
            {
                handle = 0;
                AddLog("Missing required symbol: " + symbol);
                throw new InvalidOperationException(
                    "Required ADS symbol is unavailable: " + symbol, ex);
            }
        }

        private void CreateOptionalHandle(string symbol, ref uint handle)
        {
            try
            {
                handle = adsClient.CreateVariableHandle(symbol);
            }
            catch
            {
                handle = 0;
            }
        }

        private async Task PulseTwinSafeErrorAckAsync()
        {
            adsClient.WriteAny(hErrAckIn, false);
            await Task.Delay(50);
            adsClient.WriteAny(hErrAckIn, true);
            await Task.Delay(100);
            adsClient.WriteAny(hErrAckIn, false);
            await Task.Delay(100);
        }

        // MOTOR FEEDBACK
        private void AdsPollTimer_Tick(object sender, EventArgs e)
        {
            if (!adsConnected || adsClient == null) return;

            try
            {
                if (hActualVelocity != 0)
                {
                    int actualVelocity = Convert.ToInt32(adsClient.ReadAny(hActualVelocity, typeof(int)));
                    int actualPosition = Convert.ToInt32(adsClient.ReadAny(hActualPosition, typeof(int)));

                    speedGauge.Value = Math.Max(speedGauge.Minimum, Math.Min(speedGauge.Maximum, Math.Abs(actualVelocity)));

                    //Change degree value to motor encoder position value 12 bit
                    lblPositionAngle.Text = Math.Abs(actualPosition).ToString("0.0");

                    if (actualPosition != 0)
                    {
                        try
                        {
                            double angle = rotorPosition.PositionCountsToDegrees(actualPosition);
                            rotorPosition.Angle = angle;
                            rotorPosition.Invalidate();
                        }
                        catch (Exception ex)
                        {
                            AddLog("Position feedback read failed: " +
                                   ShortError(ex));
                        }
                    }

                    if (Math.Abs(actualVelocity) > 0)
                    {
                        lblMotorStatus.Text = "●  MOTOR RUNNING";
                        lblMotorStatus.ForeColor = Color.LimeGreen;
                    }
                    else if (!motorRunning)
                    {
                        lblMotorStatus.Text = "●  MOTOR STOPPED";
                        lblMotorStatus.ForeColor = Color.Red;
                    }
                }
                else
                {
                    speedGauge.Value = 0;
                    lblMotorStatus.Text = "●  FEEDBACK N/A";
                    lblMotorStatus.ForeColor = Color.Orange;
                }

                if (hStatusword != 0)
                {
                    ushort statusword = Convert.ToUInt16(adsClient.ReadAny(hStatusword, typeof(ushort)));

                    if ((statusword & 0x0008) != 0)
                    {
                        lblMotorStatus.Text = "●  MOTOR FAULT";
                        lblMotorStatus.ForeColor = Color.Red;
                    }
                }

                if (hErrorCode != 0)
                {
                    ushort errorCode = Convert.ToUInt16(adsClient.ReadAny(hErrorCode, typeof(ushort)));

                    if (errorCode != 0) AddLog("Drive error code = 0x" + errorCode.ToString("X4"));
                }
            }
            catch (Exception ex)
            {
                AddLog("EtherCAT feedback lost: " + ShortError(ex));
                adsConnected = false;
                SetOfflineState("FEEDBACK LOST");
            }
        }

        private void SetReadyState()
        {
            lblSafety.Text = "SYSTEM READY";
            lblSafety.ForeColor = Color.LimeGreen;
            lblSafetyDescription.Text = "TwinCAT / ADS Connected";
            lblMotorStatus.Text = "●  MOTOR STOPPED";
            lblMotorStatus.ForeColor = Color.Red;
            btnStart.Text = "START SYSTEM";
        }

        private void SetOfflineState(string state)
        {
            motorRunning = false;
            speedGauge.Value = 0;
            rotorPosition.Angle = 0;
            lblPositionAngle.Text = "0.0°";

            lblSafety.Text = state;
            lblSafety.ForeColor = Color.Red;
            lblSafetyDescription.Text = "EtherCAT / ADS Offline";
            lblMotorStatus.Text = "●  MOTOR STOPPED";
            lblMotorStatus.ForeColor = Color.Red;
            btnStart.Text = "RETRY CONNECTION";
        }

        private string ShortError(Exception ex)
        {
            if (ex == null) return "Unknown error";

            return ex.Message.Replace(Environment.NewLine, " ");
        }

        private void StopAds()
        {
            try
            {
                if (adsPollTimer != null) adsPollTimer.Stop();

                if (adsClient == null) return;

                DeleteHandle(ref hActualVelocity);
                DeleteHandle(ref hActualPosition);
                DeleteHandle(ref hModeDisplay);
                DeleteHandle(ref hErrorCode);
                DeleteHandle(ref hStatusword);
                DeleteHandle(ref hErrAckIn);
                DeleteHandle(ref hTargetVelocity);
                DeleteHandle(ref hControlword);

                adsClient.Dispose();
                adsClient = null;
            }
            catch
            {
            }
        }

        private void DeleteHandle(ref uint handle)
        {
            if (handle == 0 || adsClient == null) return;

            try
            {
                adsClient.DeleteVariableHandle(handle);
            }
            catch
            {
            }

            handle = 0;
        }

        private void AddLog(string message)
        {
            if (txtLog == null) return;

            string time = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText("[" + time + "] " + message + Environment.NewLine);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CenterDashboard();
        }
    }

    public class RotorPosition : Control
    {
        private double angle = 0.0;
        private const int POSITION_COUNTS_PER_REVOLUTION = 4096;

        public double Angle
        {
            get
            {
                return angle;
            }

            set
            {
                angle = value % 360.0;

                if (angle < 0)
                {
                    angle += 360.0;
                }

                Invalidate();
            }
        }

        public RotorPosition()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(8, 16, 24);
        }

        public double PositionCountsToDegrees(int actualPosition)
        {
            int counts = actualPosition % POSITION_COUNTS_PER_REVOLUTION;

            counts *= -1;

            if (counts < 0)
                counts += POSITION_COUNTS_PER_REVOLUTION;

            return counts * 360.0 / POSITION_COUNTS_PER_REVOLUTION;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float width = ClientSize.Width;
            float height = ClientSize.Height;
            float centerX = width / 2f;
            float centerY = height / 2f;
            float radius = Math.Min(width, height) / 2f - 8;

            using (Pen ringPen = new Pen(Color.DeepSkyBlue, 3))
            {
                g.DrawEllipse(ringPen, centerX - radius, centerY - radius, radius * 2, radius * 2);
            }

            float innerRadius = radius * 0.72f;

            using (Brush rotorBrush = new SolidBrush(Color.FromArgb(15, 30, 42)))
            {
                g.FillEllipse(rotorBrush, centerX - innerRadius, centerY - innerRadius, innerRadius * 2, innerRadius * 2);
            }

            using (Pen rotorPen = new Pen(Color.FromArgb(70, 100, 120), 2))
            {
                g.DrawEllipse(rotorPen, centerX - innerRadius, centerY - innerRadius, innerRadius * 2, innerRadius * 2);
            }

            for (int i = 0; i < 8; i++)
            {
                double poleAngle = i * 45.0;
                double radians = (poleAngle - 90.0) * Math.PI / 180.0;
                float x = centerX + (float)Math.Cos(radians) * innerRadius * 0.72f;
                float y = centerY + (float)Math.Sin(radians) * innerRadius * 0.72f;
                float size = 7;

                using (Brush poleBrush = new SolidBrush(Color.Gray))
                {
                    g.FillEllipse(poleBrush, x - size / 2, y - size / 2, size, size);
                }
            }

            double rotorRadians = (Angle - 90.0) * Math.PI / 180.0;
            float markerRadius = innerRadius * 0.82f;
            float markerX = centerX + (float)Math.Cos(rotorRadians) * markerRadius;
            float markerY = centerY + (float)Math.Sin(rotorRadians) * markerRadius;

            using (Pen rotorArm = new Pen(Color.LimeGreen, 4))
            {
                rotorArm.StartCap = LineCap.Round;
                rotorArm.EndCap = LineCap.Round;

                g.DrawLine(rotorArm, centerX, centerY, markerX, markerY);
            }

            using (Brush markerBrush = new SolidBrush(Color.LimeGreen))
            {
                g.FillEllipse(markerBrush, markerX - 6, markerY - 6, 12, 12);
            }

            float shaftRadius = 10;

            using (Brush shaftBrush = new SolidBrush(Color.LightGray))
            {
                g.FillEllipse(shaftBrush, centerX - shaftRadius, centerY - shaftRadius, shaftRadius * 2, shaftRadius * 2);
            }

            using (Pen shaftPen = new Pen(Color.White, 2))
            {
                g.DrawEllipse(shaftPen, centerX - shaftRadius, centerY - shaftRadius, shaftRadius * 2, shaftRadius * 2);
            }

            using (Pen zeroPen = new Pen(Color.Red, 2))
            {
                g.DrawLine(zeroPen, centerX, centerY - radius, centerX, centerY - radius + 12);
            }
        }
    }

    public class SpeedGauge : Control
    {
        private int minimum = 0;
        private int maximum = 6000;
        private int value = 0;

        public int Minimum
        {
            get 
            { 
                return minimum; 
            }
            set
            {
                minimum = value;
                if (maximum <= minimum) maximum = minimum + 1;
                Value = this.value;
                Invalidate();
            }
        }

        public int Maximum
        {
            get 
            { 
                return maximum; 
            }
            set
            {
                maximum = Math.Max(value, minimum + 1);
                Value = this.value;
                Invalidate();
            }
        }

        public int Value
        {
            get 
            { 
                return value; 
            }
            set
            {
                this.value = Math.Max(minimum, Math.Min(maximum, value));
                Invalidate();
            }
        }

        public SpeedGauge()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(8, 16, 24);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            float cx = Width / 2f;
            float cy = Height / 2f + 8f;
            float radius = Math.Min(Width, Height) * 0.40f;

            RectangleF arcRect = new RectangleF(cx - radius, cy - radius, radius * 2, radius * 2);

            const float startAngle = 135f;
            const float sweepAngle = 270f;

            // green 0-4000, yellow 4000-5000, red 5000-6000 RPM.
            DrawArcSegment(g, arcRect, startAngle, 180f, Color.LimeGreen, 9f);
            DrawArcSegment(g, arcRect, startAngle + 180f, 45f, Color.Gold, 9f);
            DrawArcSegment(g, arcRect, startAngle + 225f, 45f, Color.Red, 9f);

            // Full numbered scale: 0 to 6000 RPM.
            const int majorStep = 1000;
            const int minorStep = 200;

            for (int rpm = minimum; rpm <= maximum; rpm += minorStep)
            {
                float fraction = (float)(rpm - minimum) / Math.Max(1, maximum - minimum);

                float angleDeg = startAngle + sweepAngle * fraction;

                double radians = angleDeg * Math.PI / 180.0;

                bool major = rpm == minimum || rpm == maximum || rpm % majorStep == 0;

                float outer = radius + 5f;
                float inner = major ? radius - 17f : radius - 10f;

                float x1 = cx + (float)Math.Cos(radians) * inner;
                float y1 = cy + (float)Math.Sin(radians) * inner;
                float x2 = cx + (float)Math.Cos(radians) * outer;
                float y2 = cy + (float)Math.Sin(radians) * outer;

                using (Pen tickPen = new Pen(major ? Color.White : Color.Gray, major ? 2.2f : 1.2f))
                {
                    g.DrawLine(tickPen, x1, y1, x2, y2);
                }

                if (major)
                {
                    float labelRadius = radius - 43f;
                    float lx = cx + (float)Math.Cos(radians) * labelRadius;
                    float ly = cy + (float)Math.Sin(radians) * labelRadius;

                    string label = rpm.ToString();
                    using (Font font = new Font("Arial", 11f, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.White))
                    {
                        SizeF size = g.MeasureString(label, font);
                        g.DrawString(label, font, brush, lx - size.Width / 2f, ly - size.Height / 2f);
                    }
                }
            }

            // Needle follows the ADS actual velocity
            float valueFraction = (float)(Value - minimum) / Math.Max(1, maximum - minimum);
            float needleAngle = (startAngle + sweepAngle * valueFraction) * (float)Math.PI / 180f;
            float needleRadius = radius - 22f;
            float needleX = cx + (float)Math.Cos(needleAngle) * needleRadius;
            float needleY = cy + (float)Math.Sin(needleAngle) * needleRadius;

            using (Pen needlePen = new Pen(Color.Red, 4f))
            {
                needlePen.StartCap = LineCap.Round;
                needlePen.EndCap = LineCap.Round;
                g.DrawLine(needlePen, cx, cy, needleX, needleY);
            }

            using (Brush hubBrush = new SolidBrush(Color.LightGray))
            {
                g.FillEllipse(hubBrush, cx - 9, cy - 9, 18, 18);
            }

            using (Pen hubPen = new Pen(Color.White, 2))
            {
                g.DrawEllipse(hubPen, cx - 9, cy - 9, 18, 18);
            }

            // Digital RPM value
            string digital = Value.ToString();
            using (Font digitalFont = new Font("Arial", 24f, FontStyle.Bold))
            using (Brush digitalBrush = new SolidBrush(Color.White))
            {
                SizeF size = g.MeasureString(digital, digitalFont);
                g.DrawString(digital, digitalFont, digitalBrush, cx - size.Width / 2f, cy + radius * 0.38f);
            }

            using (Font unitFont = new Font("Arial", 10f, FontStyle.Bold))
            {
                using (Brush unitBrush = new SolidBrush(Color.LightGray))
                {
                    const string unit = "r/min";
                    SizeF size = g.MeasureString(unit, unitFont);
                    g.DrawString(unit, unitFont, unitBrush, cx - size.Width / 2f, cy + radius * 0.38f + 31f);
                }
            }
        }

        private void DrawArcSegment(Graphics g, RectangleF rect, float start, float sweep, Color color, float width)
        {
            using (Pen pen = new Pen(color, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, rect, start, sweep);
            }
        }
    }
}