using System;
using System.Collections.Generic;
using System.Text;
using WiimoteLib;
using System.Windows.Forms;
using System.Drawing;
using PointF = System.Drawing.PointF;
using ButtonState = WiimoteLib.ButtonState;
using System.Runtime.InteropServices;
using System.IO;

namespace WiimoteWhiteboard
{
    public class Controller
    {
        public const float CALIBRATION_MARGIN = .1f;
        const int SMOOTHING_BUFFER_SIZE = 50;

        bool PrevCursorEnabled;
        public bool CursorEnabled
        {
            get;
            set;
        }

        public struct CalibrationStateInfo
        {
            public int State;
            public System.Drawing.Point TargetPoint;
        }

        public event EventHandler<EventArgs<CalibrationStateInfo>> CalibrationStateChanged;
        public event EventHandler<EventArgs<WiimoteState>> StatusInfoChanged;

        CalibrationStateInfo CalibrationState = new CalibrationStateInfo() { State = 0, TargetPoint = new System.Drawing.Point() };

        Warper warper = new Warper();
        PointF[] src = new PointF[4];
        PointF[] dst = new PointF[4];

        public PointF[] SourceLayout
        {
            get { return src; }
        }

        public PointF[] DestinationLayout
        {
            get { return dst; }
        }

        IRSensor lastWiiState = new IRSensor(); //helps with event firing
        ButtonState lastButtonState = new ButtonState();

        Object lockObj = new Object();

        PointF[] SmoothingBuffer = new PointF[SMOOTHING_BUFFER_SIZE];
        int SmoothingBufferIndex = 0;

        public int SmoothingAmount
        {
            get;
            set;
        }

        public bool SmoothingEnabled
        {
            get { return SmoothingAmount > 0; }
        }

        Rectangle ScreenSize;

        Wiimote wm = new Wiimote();
        InputController InputCont;

        public Controller(Screen screen)
        {
            SmoothingAmount = 8;
            ScreenSize = screen.Bounds;
            InputCont = new InputController(ScreenSize);

            for (int i = 0; i < SMOOTHING_BUFFER_SIZE; i++)
                SmoothingBuffer[i] = new PointF();

            wm.WiimoteChanged += new EventHandler<WiimoteChangedEventArgs>(wm_WiimoteChanged);
            wm.WiimoteExtensionChanged += new EventHandler<WiimoteExtensionChangedEventArgs>(wm_WiimoteExtensionChanged);

            for (int i = 0; i < src.Length; i++)
            {
                src[i] = new PointF();
                dst[i] = new PointF();
            }

            CursorEnabled = LoadConfig();
            PrevCursorEnabled = CursorEnabled;
        }

        public void Enable()
        {
            wm.Connect();
            wm.SetReportType(InputReport.IRAccel, true);
            wm.GetStatus();
            wm.SetLEDs(true, false, false, false);
        }

        public void Disable()
        {
            wm.Disconnect();
        }
        
        PointF GetSmoothedCursor(int amount)
        {
            int start = SmoothingBufferIndex - amount;
            if (start < 0)
                start = 0;
            PointF smoothed = new PointF(0, 0);
            int count = SmoothingBufferIndex - start;
            for (int i = start; i < SmoothingBufferIndex; i++)
            {
                smoothed.X += SmoothingBuffer[i % SMOOTHING_BUFFER_SIZE].X;
                smoothed.Y += SmoothingBuffer[i % SMOOTHING_BUFFER_SIZE].Y;
            }
            smoothed.X /= count;
            smoothed.Y /= count;
            return smoothed;
        }

        void wm_WiimoteExtensionChanged(object sender, WiimoteExtensionChangedEventArgs args)
        {
			if(!args.Inserted)
                wm.SetReportType(InputReport.IRAccel, true);
        }

        void wm_WiimoteChanged(object sender, WiimoteChangedEventArgs args)
        {
            lock (lockObj)
            {
                WiimoteLib.ButtonState bs = args.WiimoteState.ButtonState;

                //extract the wiimote state
                IRSensor ws = args.WiimoteState.IRState.IRSensors[0];

                if (ws.Found) //move cursor
                {
                    PointF position = new PointF(ws.RawPosition.X, ws.RawPosition.Y);
                    PointF warpedPos = position;
                    warper.warp(position, ref warpedPos);

                    SmoothingBuffer[SmoothingBufferIndex % SMOOTHING_BUFFER_SIZE] = warpedPos;
                    SmoothingBufferIndex++;

                    if (!lastWiiState.Found) //mouse down
                    {
                        SmoothingBufferIndex = 0; //resets the count

                        if (CursorEnabled)
                        {
                            InputCont.MoveMouse(warpedPos); //move the mouse to x,y
                            InputCont.LeftMouseDown(); //left click down
                        } else if (CalibrationState.State > 0)
                            ContinueCalibration(position);
                    }
                    else if (CursorEnabled) //dragging
                    {
                        //if (((x != lastWiiState.RawPosition.X) || (y != lastWiiState.RawPosition.Y)) && cursorControl)
                        if (SmoothingEnabled)
                        {
                            PointF s = GetSmoothedCursor(SmoothingAmount);
                            InputCont.MoveMouse(s); //move the mouse to x,y
                        }
                        else
                            InputCont.MoveMouse(warpedPos); //move the mouse to x,y
                    }
                }
                else if (lastWiiState.Found && CursorEnabled)
                {//mouse up
                    InputCont.LeftMouseUp();
                    InputCont.MoveMouse(new PointF(0,0)); //move the mouse to x,y
                }

                if (!lastButtonState.A && bs.A)
                    Calibrate();

                lastButtonState = bs;
                lastWiiState = ws;

                if (StatusInfoChanged != null)
                    StatusInfoChanged(this, new EventArgs<WiimoteState>(args.WiimoteState));
            }
        }


        void ContinueCalibration(PointF position)
        {
            switch (CalibrationState.State)
            {
                case 1:
                    src[CalibrationState.State - 1] = position;
                    CalibrationState.TargetPoint.X = ScreenSize.Width - (int)(ScreenSize.Width * CALIBRATION_MARGIN);
                    CalibrationState.TargetPoint.Y = (int)(ScreenSize.Height * CALIBRATION_MARGIN);
                    dst[CalibrationState.State] = CalibrationState.TargetPoint;
                    CalibrationState.State++;
                    if (CalibrationStateChanged != null)
                        CalibrationStateChanged(this, new EventArgs<CalibrationStateInfo>(CalibrationState));
                    break;
                case 2:
                    src[CalibrationState.State - 1] = position;
                    CalibrationState.TargetPoint.X = (int)(ScreenSize.Width * CALIBRATION_MARGIN);
                    CalibrationState.TargetPoint.Y = ScreenSize.Height - (int)(ScreenSize.Height * CALIBRATION_MARGIN);
                    dst[CalibrationState.State] = CalibrationState.TargetPoint;
                    CalibrationState.State++;
                    if (CalibrationStateChanged != null)
                        CalibrationStateChanged(this, new EventArgs<CalibrationStateInfo>(CalibrationState));
                    break;
                case 3:
                    src[CalibrationState.State - 1] = position;
                    CalibrationState.TargetPoint.X = ScreenSize.Width - (int)(ScreenSize.Width * CALIBRATION_MARGIN);
                    CalibrationState.TargetPoint.Y = ScreenSize.Height - (int)(ScreenSize.Height * CALIBRATION_MARGIN);
                    dst[CalibrationState.State] = CalibrationState.TargetPoint;
                    CalibrationState.State++;
                    if (CalibrationStateChanged != null)
                        CalibrationStateChanged(this, new EventArgs<CalibrationStateInfo>(CalibrationState));
                    break;
                case 4:
                    src[CalibrationState.State - 1] = position;
                    warper.setDestination(dst[0], dst[1], dst[2], dst[3]);
                    warper.setSource(src[0], src[1], src[2], src[3]);
                    warper.computeWarp();
                    SaveConfig();
                    CalibrationState.State = 0;
                    if (CalibrationStateChanged != null)
                        CalibrationStateChanged(this, new EventArgs<CalibrationStateInfo>(CalibrationState));
                    CursorEnabled = PrevCursorEnabled;
                    break;
                default:
                    break;
            }
        }

        public void Calibrate()
        {
            PrevCursorEnabled = CursorEnabled;
            CursorEnabled = false;
            CalibrationState.TargetPoint.X = (int)(ScreenSize.Width * CALIBRATION_MARGIN);
            CalibrationState.TargetPoint.Y = (int)(ScreenSize.Height * CALIBRATION_MARGIN);
            dst[0] = CalibrationState.TargetPoint;

            //TODO: rather than src, dst being used directly while calibrating two temp arrays
            //should be used. so we still have the working values. (usefull to display a correct 
            //utilisation layout after a cancelling of a calibration

            CalibrationState.State = 1;
            if (CalibrationStateChanged != null)
                CalibrationStateChanged(this, new EventArgs<CalibrationStateInfo>(CalibrationState));
        }

        public void CalibrationCancel()
        {
            CalibrationState.State = 0;
            if (CalibrationStateChanged != null)
                CalibrationStateChanged(this, new EventArgs<CalibrationStateInfo>(CalibrationState));
            CursorEnabled = PrevCursorEnabled;
        }

        public void SaveConfig()
        {
            TextWriter tw = new StreamWriter("calibration.dat");
            for (int i = 0; i < 4; i++)
            {
                tw.WriteLine(src[i].X);
                tw.WriteLine(src[i].Y);
            }
            tw.Close();
        }

        public bool LoadConfig()
        {
            try
            {
                TextReader tr = new StreamReader("calibration.dat");
                for (int i = 0; i < 4; i++)
                {
                    src[i].X = float.Parse(tr.ReadLine());
                    src[i].Y = float.Parse(tr.ReadLine());
                }

                // close the stream
                tr.Close();

                warper.setDestination(new PointF(ScreenSize.Width * CALIBRATION_MARGIN,
                                        ScreenSize.Height * CALIBRATION_MARGIN),
                                      new PointF(ScreenSize.Width * (1.0f - CALIBRATION_MARGIN),
                                        ScreenSize.Height * CALIBRATION_MARGIN),
                                      new PointF(ScreenSize.Width * CALIBRATION_MARGIN,
                                        ScreenSize.Height * (1.0f - CALIBRATION_MARGIN)),
                                      new PointF(ScreenSize.Width * (1.0f - CALIBRATION_MARGIN),
                                        ScreenSize.Height * (1.0f - CALIBRATION_MARGIN)));
                warper.setSource(src[0], src[1], src[2], src[3]);

                warper.computeWarp(); 
                
                return true;
            }
            catch (System.IO.FileNotFoundException)
            {
                //no prexsting calibration
                return false;
            }
        }

    }
}
