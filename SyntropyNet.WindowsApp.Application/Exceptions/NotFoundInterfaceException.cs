using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Exceptions
{
    class NotFoundInterfaceException : Exception
    {
        public NotFoundInterfaceException()
        {

        }

        public NotFoundInterfaceException(string error)
            : base(error)
        {

        }
    }
}
