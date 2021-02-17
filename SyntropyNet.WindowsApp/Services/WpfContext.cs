using SyntropyNet.WindowsApp.Application.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SyntropyNet.WindowsApp.Services
{
    public class WpfContext : IContext
    {
        private readonly Dispatcher _dispatcher;

        public bool IsSynchronized
        {
            get
            {
                return this._dispatcher.Thread == Thread.CurrentThread;
            }
        }

        public WpfContext()
        {
            this._dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void Invoke(Action action)
        {
            Debug.Assert(action != null);

            this._dispatcher.Invoke(action);
        }

        public void BeginInvoke(Action action)
        {
            Debug.Assert(action != null);

            this._dispatcher.BeginInvoke(action);
        }
        public void BeginInvoke<T>(Action<T> action, T e)
        {
            Debug.Assert(action != null);

            this._dispatcher.BeginInvoke(action, e);
        }
        public void BeginInvoke(Delegate action, params object[] args)
        {
            Debug.Assert(action != null);

            this._dispatcher.BeginInvoke(action, args);
        }
    }
}
