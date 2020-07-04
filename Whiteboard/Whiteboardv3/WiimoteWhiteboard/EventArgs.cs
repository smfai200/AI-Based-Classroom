using System;
using System.Collections.Generic;
using System.Text;

namespace WiimoteWhiteboard
{

    public class EventArgs<T> : EventArgs
    {
        public T Value
        {
            get;
            set;
        }

        public EventArgs(T value)
            : base()
        {
            Value = value;
        }
    }

}
