using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;

namespace WiimoteWhiteboard
{
    public class InputController
    {
        Rectangle ScreenSize;

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public short wVk;
            public short wScan;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        struct INPUT32
        {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(4)]
            public MOUSEINPUT mi;
            [FieldOffset(4)]
            public KEYBDINPUT ki;
            [FieldOffset(4)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        struct INPUT64
        {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(8)]
            public MOUSEINPUT mi;
            [FieldOffset(8)]
            public KEYBDINPUT ki;
            [FieldOffset(8)]
            public HARDWAREINPUT hi;
        }

        [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT32[] pInputs, int cbSize);

        [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT64[] pInputs, int cbSize);

        const int INPUTTYPE_MOUSE = 0;
        const int INPUTTYPE_KEYBOARD = 1;
        const int INPUTTYPE_HARDWARE = 2;

        //declare consts for mouse messages
        const int MOUSEEVENTF_MOVE = 0x01;
        const int MOUSEEVENTF_LEFTDOWN = 0x02;
        const int MOUSEEVENTF_LEFTUP = 0x04;
        const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        const int MOUSEEVENTF_RIGHTUP = 0x10;
        const int MOUSEEVENTF_MIDDLEDOWN = 0x20;
        const int MOUSEEVENTF_MIDDLEUP = 0x40;
        const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        //declare consts for key scan codes
        const byte VK_TAB = 0x09;
        const byte VK_MENU = 0x12; 

        const byte VK_LEFT = 0x25;
        const byte VK_UP = 0x26;
        const byte VK_RIGHT = 0x27;
        const byte VK_DOWN = 0x28;
        const int KEYEVENTF_EXTENDEDKEY = 0x01;
        const int KEYEVENTF_KEYUP = 0x02;

        //defining click inputs for easy access
        
        INPUT32[] left_click_down32 = new INPUT32[1];
        INPUT32[] left_click_up32 = new INPUT32[1];
        INPUT64[] left_click_down64 = new INPUT64[1];
        INPUT64[] left_click_up64 = new INPUT64[1];

        private void defineINPUTS()
        {
            left_click_down32[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            left_click_up32[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;
            left_click_down64[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            left_click_up64[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;
        }

        public void MoveMouse(PointF p)
        {
            //TODO: move mouse should use a Point rather than a PointF and should get directly usable coordinates
            if (IntPtr.Size == 8)
            {
                var move = new INPUT64[1];
                move[0] = new INPUT64();
                move[0].mi.dx = (int)(p.X * 65535 / ScreenSize.Width);
                move[0].mi.dy = (int)(p.Y * 65535 / ScreenSize.Height);
                move[0].mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
                SendInput(1, move, Marshal.SizeOf(move[0]));
            }
            else
            {
                var move = new INPUT32[1];
                move[0] = new INPUT32();
                move[0].mi.dx = (int)(p.X * 65535 / ScreenSize.Width);
                move[0].mi.dy = (int)(p.Y * 65535 / ScreenSize.Height);
                move[0].mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
                SendInput(1, move, Marshal.SizeOf(move[0]));
            }
        }

        public void LeftMouseDown()
        {
            if (IntPtr.Size == 8)
                SendInput(1, left_click_down64, Marshal.SizeOf(left_click_down64[0]));
            else
                SendInput(1, left_click_down32, Marshal.SizeOf(left_click_down32[0]));
        }

        public void LeftMouseUp()
        {
            if (IntPtr.Size == 8)
                SendInput(1, left_click_up64, Marshal.SizeOf(left_click_up64[0]));
            else
                SendInput(1, left_click_up32, Marshal.SizeOf(left_click_up32[0]));
        }

        public InputController(Rectangle ScreenSize)
        {
            this.ScreenSize = ScreenSize;

            defineINPUTS();
        }

    }
}
