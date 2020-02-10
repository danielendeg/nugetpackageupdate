using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Authorization;
using Microsoft.IdentityModel.Authorization.Azure;
// using Microsoft.IdentityModel.Authorization.Azure.AzureAuthorizationEngine;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace AzureRBACSDK
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
            string pattern = "/subscriptions/(?<subscriptionId>[^/]*)";
            var match = Regex.Match(resource, pattern);

            if (!match.Success)
            {
                Console.WriteLine("Unable to find subscription in resource id");
            }

            string subscription = match.Groups["subscriptionId"].Value;

            AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{tenant}");
            ClientCredential clientCredential = new ClientCredential(clientId, clientSecret);
            AuthenticationResult authResult;

            authResult = authContext.AcquireTokenAsync("https://graph.microsoft.com", clientCredential).Result;

            var message = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{oid}/getMemberGroups");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
            message.Content = new StringContent("{\"securityEnabledOnly\": true}", Encoding.UTF8, "application/json");

            var groups = httpClient.SendAsync(message).Result.Content.ReadAsStringAsync().Result;
            var jGroups = JObject.Parse(groups);
            var groupStrings = jGroups.SelectTokens(".value[*]").Select( t => { return t.ToString(); });

            authResult = authContext.AcquireTokenAsync("https://management.azure.com", clientCredential).Result;

            var builtInRoles = GetUrl(@"https://management.azure.com/providers/Microsoft.Authorization/roleDefinitions?api-version=2018-01-01-preview&$filter=builtinRolesIncludingServiceRoles()", authResult.AccessToken);
            var customRoles = GetUrl(@"https://management.azure.com/subscriptions/" + subscription + @"/providers/Microsoft.Authorization/roleDefinitions?api-version=2018-01-01-preview&$filter=Type+eq+'CustomRole' and rewriteManagementGroupScopes()", authResult.AccessToken);
            var roleAssignments = GetUrl(@"https://management.azure.com/subscriptions/" + subscription + @"/providers/Microsoft.Authorization/roleAssignments?api-version=2018-01-01-preview&$filter=includeAssignmentsToServiceRolesAlso() and rewriteManagementGroupScopes()", authResult.AccessToken);
            var denyAssignments = GetUrl(@"https://management.azure.com/subscriptions/" + subscription + @"/providers/Microsoft.Authorization/denyAssignments?api-version=2018-07-01-preview&$filter=rewriteManagementGroupScopes()", authResult.AccessToken);

            var azureJsonParser = new AzureJsonParser();
            var parsedBuiltInRoleDefinitions = azureJsonParser.ParseRoleDefinitions(builtInRoles);
            var parsedCustomRoleDefinitions = azureJsonParser.ParseRoleDefinitions(customRoles);
            var parsedRoleAssignments = azureJsonParser.ParseRoleAssignments(roleAssignments);
            var parsedDenyAssignments = azureJsonParser.ParseDenyAssignments(denyAssignments);

            var azureAuthorizationEngine = new AzureAuthorizationEngine(
                parsedBuiltInRoleDefinitions.Union(parsedCustomRoleDefinitions),
                parsedRoleAssignments,
                parsedDenyAssignments);

            var subjectInfo = new SubjectInfo();
            subjectInfo.AddAttribute(SubjectAttribute.ObjectId, oid);
            subjectInfo.AddAttribute(SubjectAttribute.Groups, groupStrings);

            var resourceInfo = new ResourceInfo(resource);
            var resourceAction = new ActionInfo(action, true);
            var check = azureAuthorizationEngine.CheckAccess(subjectInfo, resourceInfo, resourceAction);

            Console.WriteLine($"Access Check: {check.IsAccessGranted}");
        }

        private static string GetUrl(string url, string token)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, url);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return httpClient.SendAsync(message).Result.Content.ReadAsStringAsync().Result;
        }
    }
}
