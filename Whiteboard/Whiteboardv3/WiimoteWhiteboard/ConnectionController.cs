using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace WiimoteWhiteboard
{
    public partial class ConnectionController : UserControl
    {
        public enum ConnectState { Connected, Connecting, NotFound }

        ConnectionManager ConnectionManager;
        public ConnectionController()
        {
            InitializeComponent();

            ConnectionManager = new ConnectionManager();

            ConnectionManager.Connected += new EventHandler(ConnectionManager_Connected);
            ConnectionManager.Error += new EventHandler<EventArgs<string>>(ConnectionManager_Error);
            ConnectionManager.ConnectionFailed += new EventHandler(ConnectionManager_ConnectionFailed);

            State = ConnectState.Connected;
        }

        public EventHandler<EventArgs<ConnectState>> StateChanged;

        ConnectState _State = ConnectState.NotFound;
        public ConnectState State
        {
            get
            {
                return _State;
            }
            private set
            {
                if (_State == value)
                    return;

                _State = value;
                switch (value)
                {
                    case ConnectState.Connected:
                        pbConnecting.Visible = false;
                        lblStatus.BackColor = Color.Green;
                        lblStatus.Text = "Connected";
                        break;
                    case ConnectState.Connecting:
                        lblStatus.BackColor = Color.DarkOrange;
                        pbConnecting.Visible = false;
                        lblStatus.Text = "Searching for Wiimotes.";
                        break;
                    case ConnectState.NotFound:
                        lblStatus.BackColor = Color.OrangeRed;
                        pbConnecting.Visible = false;
                        lblStatus.Text = "Wiimote search failed!";
                        break;
                }

                if (StateChanged != null)
                    StateChanged(this, new EventArgs<ConnectState>(value));
            }
        }

        public void Connect()
        {
            State = ConnectState.Connecting;
            ConnectionManager.ConnectWiiMotes(true);
        }

        void ConnectionManager_ConnectionFailed(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate()
            {
                if (cbAutoConnect.Checked)
                    Connect();
                else
                    State = ConnectState.NotFound;
            });
        }

        void ConnectionManager_Error(object sender, EventArgs<string> e)
        {
            BeginInvoke((MethodInvoker)delegate()
            {
                State = ConnectState.NotFound;
                MessageBox.Show(e.Value);
            });
        }

        void ConnectionManager_Connected(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate()
            {
                State = ConnectState.Connected;
            });
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Connect();
        }


    }
}
