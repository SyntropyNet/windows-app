using SyntropyNet.WindowsApp.Application.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IContext
    {
        bool IsSynchronized { get; }
        void Invoke(Action action);
        void BeginInvoke(Action action);
        void BeginInvoke<T>(Action<T> action, T e);
        void BeginInvoke(Delegate action, params object[] args);
        void ShowBalloonTip(string text);
        void UpdateIcon(AppStatus status);
    }
}
