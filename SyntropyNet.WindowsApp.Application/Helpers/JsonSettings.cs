using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Helpers
{
    public static class JsonSettings
    {
        public static JsonSerializerSettings GetSnakeCaseNamingStrategy()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy { ProcessDictionaryKeys = true }
                },
                Formatting = Formatting.Indented
            };
        }
    }
}
