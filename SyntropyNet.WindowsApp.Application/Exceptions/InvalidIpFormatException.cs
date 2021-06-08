using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Exceptions
{
    class InvalidIpFormatException : Exception
    {
        public InvalidIpFormatException()
        {

        }

        public InvalidIpFormatException(string error)
            : base(error)
        {

        }
    }
}
