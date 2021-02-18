using SyntropyNet.WindowsApp.Application.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services.HttpRequest
{
    public class HttpRequestService : IHttpRequestService
    {
        public string GetResponse(string url)
        {
            var request = WebRequest.CreateHttp(url);
            var response = request.GetResponseAsync().GetAwaiter().GetResult();

            string data = "";
            using (Stream stream = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    data = reader.ReadLine();
                }
            }

            char[] charsToTrim = { ' ', '\"' };
            return data.Trim(charsToTrim);
        }
    }
}
