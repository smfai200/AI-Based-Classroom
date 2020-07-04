using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace WiimoteWhiteboard
{
    public class ConnectionManager
    {
        #region pinvoke defs
        [StructLayout(LayoutKind.Sequential)]
        struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public int dwSize;                 //  IN  sizeof this structure

            public bool fReturnAuthenticated;   //  IN  return authenticated devices
            public bool fReturnRemembered;      //  IN  return remembered devices
            public bool fReturnUnknown;         //  IN  return unknown devices
            public bool fReturnConnected;       //  IN  return connected devices

            public bool fIssueInquiry;          //  IN  issue a new inquiry
            public byte cTimeoutMultiplier;     //  IN  timeout for the inquiry

            public IntPtr hRadio;                 //  IN  handle to radio to enumerate - NULL == all radios will be searched
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BLUETOOTH_ADDRESS
        {
            public byte byte1;
            public byte byte2;
            public byte byte3;
            public byte byte4;
            public byte byte5;
            public byte byte6;
            public byte bytex1;
            public byte bytex2;
        }

        const int BLUETOOTH_MAX_NAME_SIZE = 248;

        const int BLUETOOTH_SERVICE_DISABLE = 0;
        const int BLUETOOTH_SERVICE_ENABLE = 1;

        Guid HumanInterfaceDeviceServiceClass_UUID = new Guid(0x00001124, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5F, 0x9B, 0x34, 0xFB);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        unsafe struct BLUETOOTH_DEVICE_INFO
        {
            public int dwSize;                             //  size, in bytes, of this structure - must be the sizeof(BLUETOOTH_DEVICE_INFO)
            public int padding; //why is this padding needed?
            public BLUETOOTH_ADDRESS Address;                  //  Bluetooth address
            public uint ulClassofDevice;                    //  Bluetooth "Class of Device"
            public bool fConnected;                         //  Device connected/in use
            public bool fRemembered;                        //  Device remembered
            public bool fAuthenticated;                     //  Device authenticated/paired/bonded
            public SYSTEMTIME stLastSeen;                     //  Last time the device was seen
            public SYSTEMTIME stLastUsed;                     //  Last time the device was used for other than RNR, inquiry, or SDP

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = BLUETOOTH_MAX_NAME_SIZE)]
            public string szName; //  Name of the device
        }

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS SearchParams, ref BLUETOOTH_DEVICE_INFO DeviceInfo);

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO DeviceInfo);

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint BluetoothSetServiceState(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO DeviceInfo, ref Guid guid, int ServiceFlags);

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint BluetoothRemoveDevice(ref BLUETOOTH_ADDRESS Address);
        #endregion

        public event EventHandler Connected;
        public event EventHandler<EventArgs<string>> Error;
        public event EventHandler ConnectionFailed;

        public Thread Worker;

        Object lockobj = new Object();

        public void ConnectWiiMotes(bool DisconnectOld)
        {
            lock(lockobj)
            {
                if (Worker != null && Worker.IsAlive)
                    return;

                Worker = new Thread(new ThreadStart(
                    delegate() { this.Connect(DisconnectOld); }));
                Cancel = false;
                Worker.Start();
            }
        }

        public bool Cancel { get; set; }

        private void LogError(string error)
        {
            if (Error != null)
                Error(this, new EventArgs<string>(error));
        }

        private void Connect(bool DisconnectOld)
        {
            //TODO: honour disconnectold parameter
            BLUETOOTH_DEVICE_INFO device = new BLUETOOTH_DEVICE_INFO();
            device.dwSize = Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO));
            device.szName = "";

            BLUETOOTH_DEVICE_SEARCH_PARAMS searchparams = new BLUETOOTH_DEVICE_SEARCH_PARAMS();
            searchparams.dwSize = Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_SEARCH_PARAMS));
            searchparams.fIssueInquiry = true;
            searchparams.fReturnAuthenticated = true;
            searchparams.fReturnConnected = true;
            searchparams.fReturnRemembered = true;
            searchparams.fReturnUnknown = true;
            searchparams.hRadio = IntPtr.Zero;
            searchparams.cTimeoutMultiplier = 1;


            bool connected = false;
            IntPtr handle = BluetoothFindFirstDevice(ref searchparams, ref device);
            if (handle == IntPtr.Zero)
            {
                int lasterror = Marshal.GetLastWin32Error();
                if (lasterror != 0)
                    LogError("Bluetooth API returned: " + lasterror.ToString());
            }
            else
            {
                while (true)
                {
                    if (Cancel)
                        break;

                    if (device.szName.StartsWith("Nintendo RVL"))
                    {
                        if (device.fRemembered)
                        {
                            BluetoothRemoveDevice(ref device.Address);
                        }
                        else
                        {
                            if (BluetoothSetServiceState(IntPtr.Zero, ref device, ref HumanInterfaceDeviceServiceClass_UUID, BLUETOOTH_SERVICE_ENABLE) != 0)
                                LogError("Failed to connect to wiimote controller");
                            else
                                connected = true;
                        }
                        break;
                    }

                    device.szName = "";
                    if (!BluetoothFindNextDevice(handle, ref device))
                        break;
                }
            }
            BluetoothFindDeviceClose(handle);
            if (connected && Connected != null)
                Connected(this, EventArgs.Empty);
            else if (ConnectionFailed != null)
                ConnectionFailed(this, EventArgs.Empty);
        }

        public static bool IsStackCompatible()
        {
            BLUETOOTH_DEVICE_INFO device = new BLUETOOTH_DEVICE_INFO();
            device.dwSize = Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO));
            device.szName = "";

            BLUETOOTH_DEVICE_SEARCH_PARAMS searchparams = new BLUETOOTH_DEVICE_SEARCH_PARAMS();
            searchparams.dwSize = Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_SEARCH_PARAMS));
            searchparams.fIssueInquiry = true;
            searchparams.fReturnAuthenticated = true;
            searchparams.fReturnConnected = true;
            searchparams.fReturnRemembered = true;
            searchparams.fReturnUnknown = false;
            searchparams.hRadio = IntPtr.Zero;
            searchparams.cTimeoutMultiplier = 1;


            IntPtr handle = BluetoothFindFirstDevice(ref searchparams, ref device);
            if (handle != IntPtr.Zero)
                BluetoothFindDeviceClose(handle);

            return handle != IntPtr.Zero;
        }
    }
}
