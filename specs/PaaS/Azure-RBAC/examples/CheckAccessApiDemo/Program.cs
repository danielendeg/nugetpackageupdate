using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace CheckAccessApiDemo
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static void Main(
            string tenant,
            string clientId,
            string clientSecret,
            string oid,
            string resource,
            string action)
        {
            AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{tenant}");
            ClientCredential clientCredential = new ClientCredential(clientId, clientSecret);
            AuthenticationResult authResult;

            authResult = authContext.AcquireTokenAsync("https://management.azure.com", clientCredential).Result;

            var message = new HttpRequestMessage(HttpMethod.Post, $"https://management.azure.com{resource}/providers/Microsoft.Authorization/checkaccess?api-version=2018-09-01-preview");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

            var payload = JObject.Parse(String.Format(
                @"{{
                    ""Subject"": {{
                        ""Attributes"": {{
                            ""ObjectId"": ""{0}"",
                            ""xms-pasrp-retrievegroupmemberships"" :  true
                        }}
                    }},
                    ""Actions"": [ 
                        {{ 
                            ""Id"": ""{1}"",
                            ""IsDataAction"": true,
                            ""Attributes"": {{ }}
                        }} 
                    ],
                    ""Resource"": {{ 
                        ""Id"": ""{2}"", 
                        ""Attributes"": {{ }} 
                    }},
                    ""Environment"": null
                }}",
                oid,
                action,
                resource
            )).ToString();

            message.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            var checkAccessResult = httpClient.SendAsync(message).Result.Content.ReadAsStringAsync().Result;
            JArray result = JArray.Parse(checkAccessResult);
        
            Console.WriteLine($"Access Check: {result[0]["accessDecision"].ToString()}");
        }
    }
}
