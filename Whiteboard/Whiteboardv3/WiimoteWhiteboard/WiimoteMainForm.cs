/*
 * Original Code by Johnny Chung Lee: (http://johnnylee.net/projects/wii/)
 * Patches included from: (http://sourceforge.net/projects/wiiwhiteboard/)
 * Uses Wiimote Library by Brian Peek (http://wiimotelib.codeplex.com/)
 * Icon by Zhebrakov Andrew (http://www.icojoy.com/articles/49/)
 * x64 Comptibility fix and code refactoring by Rambler (http://www.hiddenramblings.com)
 * Released under MIT License (http://www.opensource.org/licenses/mit-license.php)
 */

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using WiimoteLib;
using PointF = System.Drawing.PointF;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Runtime.InteropServices;

namespace WiimoteWhiteboard
{
	public partial class WiimoteMainForm : Form
	{
        Controller controller;
        Graphics TrackingLayout;

        public WiimoteMainForm()
		{
            InitializeComponent();

            pbTrackingLayout.Image = new Bitmap(pbTrackingLayout.Width, pbTrackingLayout.Height);
            TrackingLayout = Graphics.FromImage(pbTrackingLayout.Image);
            TrackingLayout.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            CalibrationForm.CalibrationCancelled += new EventHandler(CalibrationForm_Cancelled);
            controller = new Controller(Screen.PrimaryScreen);
            cbCursorControl.Checked = controller.CursorEnabled;
            if (controller.CursorEnabled)
                UpdateTrackingUtilization(controller.SourceLayout);
            controller.CalibrationStateChanged += controller_CalibrationStateChanged;
            controller.StatusInfoChanged += controller_StatusInfoChanged;

            ConnectionControl.StateChanged += new EventHandler<EventArgs<ConnectionController.ConnectState>>(ConnectionControl_StateChanged);

            if (!ConnectionManager.IsStackCompatible())
            {
                ConnectionControl.Visible = false;
                try
                {
                    controller.Enable();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }
            else
            {
                try
                {
                    controller.Enable();
                }
                catch (Exception)
                {
                    ConnectionControl.Connect();
                }
            }
        }

        void ConnectionControl_StateChanged(object sender, EventArgs<ConnectionController.ConnectState> state)
        {
            switch (state.Value)
            {
                case ConnectionController.ConnectState.Connected:
                    controller.Enable();
                    pnlMainControls.Enabled = true;
                    break;
                case ConnectionController.ConnectState.NotFound:
                    controller.Disable();
                    pnlMainControls.Enabled = false;
                    break;
                case ConnectionController.ConnectState.Connecting:
                    controller.Disable();
                    pnlMainControls.Enabled = false;
                    break;
            }
        }

        void controller_StatusInfoChanged(object sender, EventArgs<WiimoteState> e)
        {
            if (Visible)
                BeginInvoke((MethodInvoker)delegate() {
                    WiimoteState state = e.Value;
                    if (pbBattery.Value != (int)state.Battery)
                    {
                        pbBattery.Value = (int)state.Battery;
                        lblBattery.Text = state.Battery.ToString("0\\%");
                    }
                    lblPoint1.Visible = state.IRState.IRSensors[0].Found;
                    lblPoint2.Visible = state.IRState.IRSensors[1].Found;
                    lblPoint3.Visible = state.IRState.IRSensors[2].Found;
                    lblPoint4.Visible = state.IRState.IRSensors[3].Found;
                });
        }

        void CalibrationForm_Cancelled(object sender, EventArgs e)
        {
            controller.CalibrationCancel();
        }

        void controller_CalibrationStateChanged(object sender, EventArgs<Controller.CalibrationStateInfo> e)
        {
            if (e.Value.State > 0)
                BeginInvoke((MethodInvoker)delegate() { TopMost = false; CalibrationForm.ShowCalibration(e.Value.TargetPoint); });
            else
                BeginInvoke((MethodInvoker)delegate() { CalibrationForm.HideCalibration(); UpdateTrackingUtilization(controller.SourceLayout); TopMost = cbAlwaysOnTop.Checked; });
        }

		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
            controller.CalibrationStateChanged -= controller_CalibrationStateChanged;
            controller.StatusInfoChanged -= controller_StatusInfoChanged;
            controller.Disable();
		}

        private void btnCalibrate_Click(object sender, EventArgs e)
        {
            controller.Calibrate();
        }

        private void cbCursorControl_CheckedChanged(object sender, EventArgs e)
        {
            controller.CursorEnabled = cbCursorControl.Checked;
        }

        void UpdateTrackingUtilization(PointF[] src)
        {
            PointF[] srcscaled = new PointF[4];
            for (int i = 0; i < srcscaled.Length; i++)
            {
                srcscaled[i] = new PointF();
                srcscaled[i].X = src[i].X * pbTrackingLayout.Width / 1024;
                srcscaled[i].Y = src[i].Y * pbTrackingLayout.Height / 768;
            }
            //swap the last two points to get a proper rectangle
            PointF tmp = srcscaled[3];
            srcscaled[3] = srcscaled[2];
            srcscaled[2] = tmp;
            TrackingLayout.Clear(Color.Gray);
            TrackingLayout.DrawPolygon(Pens.Black, srcscaled);
            TrackingLayout.FillPolygon(Brushes.White, srcscaled);

            //area of ideal calibration coordinates (to match the screen)
            float idealArea = (1 - 2 * Controller.CALIBRATION_MARGIN) * 1024 * (1 - 2 * Controller.CALIBRATION_MARGIN) * 768;

            //area of quadrliatera
            float actualArea = 0.5f * Math.Abs((src[1].X - src[2].X) * (src[0].Y - src[3].Y) - (src[0].X - src[3].X) * (src[1].Y - src[2].Y));
            float util = (actualArea / idealArea) * 100;


            string value = util.ToString("0\\%");

            GraphicsPath pth = new GraphicsPath();
            pth.AddString(value, new FontFamily("Times New Roman"), 0, 20,
                new PointF(pbTrackingLayout.Width / 2 - 10, pbTrackingLayout.Height / 2 - 10), StringFormat.GenericTypographic);

            Pen p = new Pen(Color.White, 2.0f);
            TrackingLayout.FillPath(Brushes.Black, pth);
            p.Dispose();
            pth.Dispose();
            pbTrackingLayout.Invalidate();
        }

        private void tbSmoothingLevel_ValueChanged(object sender, EventArgs e)
        {
            controller.SmoothingAmount = tbSmoothingLevel.Value;
        }

        private void cbAlwaysOnTop_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = cbAlwaysOnTop.Checked;
        }

	}
}
