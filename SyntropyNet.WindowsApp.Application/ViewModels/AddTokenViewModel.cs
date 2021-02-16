using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.ViewModels
{
    public class AddTokenViewModel : BindableBase, IDialogAware
    {
        public string Title => "Add Agent Token";

        public event Action<IDialogResult> RequestClose;

        protected virtual void CloseDialog(string parameter)
        {

            //RaiseRequestClose(new DialogResult(result, new Prism.Services.Dialogs.DialogParameters(){
            //                    })
            //);
        }

        public virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }

        public virtual bool CanCloseDialog()
        {
            return true;
        }

        public virtual void OnDialogClosed()
        {

        }

        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
        }
    }
}
