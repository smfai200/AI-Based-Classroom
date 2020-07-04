using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

namespace WiimoteWhiteboard
{
    public partial class CalibrationForm : Form
    {
        Bitmap bCalibration;
        Graphics gCalibration;

        CalibrationForm()
        {
            Rectangle rect = Screen.GetBounds(this);

            InitializeComponent();
            
            this.FormBorderStyle = FormBorderStyle.None;
            this.Left = 0;
            this.Top = 0;
            this.Size = new Size(rect.Width, rect.Height);
            this.Text = "Calibration - Working area:" + Screen.GetWorkingArea(this).ToString() + " || Real area: " + Screen.GetBounds(this).ToString();

            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnKeyPress);

            bCalibration = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
            gCalibration = Graphics.FromImage(bCalibration);
            pbCalibrate.Left = 0;
            pbCalibrate.Top = 0;
            pbCalibrate.Size = new Size(rect.Width, rect.Height);
            
            gCalibration.Clear(Color.White);

            BeginInvoke((MethodInvoker)delegate() { pbCalibrate.Image = bCalibration; });
        }

        private void OnKeyPress(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if ((int)(byte)e.KeyCode == (int)Keys.Escape)
            {
                if (CalibrationCancelled != null)
                    CalibrationCancelled(this, EventArgs.Empty);
            }
        }

        void DrawCrosshair(Point point, int size, Pen p, Graphics g){
            g.DrawEllipse(p, point.X - size / 2, point.Y - size / 2, size, size);
            g.DrawLine(p, point.X - size, point.Y, point.X + size, point.Y);
            g.DrawLine(p, point.X, point.Y - size, point.X, point.Y + size);
        }

        void ShowCalibration(Point point, int size, Pen p){
            gCalibration.Clear(Color.White);
            DrawCrosshair(point, size, p, gCalibration);
            pbCalibrate.Image = bCalibration;
        }

        static CalibrationForm form = null;
        static Pen p = new Pen(Color.Red);
        const int CROSSHAIR_SIZE = 25;

        public static EventHandler CalibrationCancelled;

        public static void ShowCalibration(Point point)
        {
            if (form == null)
            {
                form = new CalibrationForm();
                form.TopMost = true;
            }

            form.ShowCalibration(point, CROSSHAIR_SIZE, p);
            form.Show();
        }

        public static void HideCalibration()
        {
            form.Hide();
            form.Dispose();
        }

    }
}