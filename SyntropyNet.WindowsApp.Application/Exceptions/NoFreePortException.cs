using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Exceptions
{
    public class NoFreePortException: Exception
    {
        public NoFreePortException()
        {

        }

        public NoFreePortException(string error)
            :base(error)
        {

        }
    }
}
