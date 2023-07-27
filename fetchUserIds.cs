using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public static class FetchUserIds
{
    [FunctionName("FetchUserIds")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "FetchUserIds/{product}")] HttpRequest req,
        string product,
        ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        if (string.IsNullOrEmpty(product))
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            product = data?.product;
        }

        if (!string.IsNullOrEmpty(product))
        {
            var azureToken = await GetAzureAccessTokenAsync();
            if (string.IsNullOrEmpty(azureToken))
            {
                return new BadRequestObjectResult("Failed to acquire Azure access token.");
            }

            var subscriptionId = Environment.GetEnvironmentVariable("SUB_ID");
            var resourceGroup = Environment.GetEnvironmentVariable("RG");
            var apimName = Environment.GetEnvironmentVariable("APIM_NAME");
            var ownerId = await GetProductSubscriptionOwnerIdAsync(azureToken, subscriptionId, resourceGroup, apimName, product);
            if (string.IsNullOrEmpty(ownerId))
            {
                return new NotFoundObjectResult($"Product '{product}' not found ");
            }

            return new OkObjectResult(ownerId);
        }
        else
        {
            return null;
        }
    }
    private static async Task<string> GetAzureAccessTokenAsync()
    {
        string clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
        string clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
        string tenantId = Environment.GetEnvironmentVariable("TENANT_ID");

        var httpClient = new HttpClient();

        var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/token";
        var tokenRequestBody = $"grant_type=client_credentials&client_id={clientId}&client_secret={clientSecret}&resource=https://management.azure.com/";

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new StringContent(tokenRequestBody, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        var tokenResponse = await httpClient.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var responseBody = await tokenResponse.Content.ReadAsStringAsync();
        dynamic tokenData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
        string accessToken = tokenData?.access_token;

        return accessToken;
    }


    private static async Task<string> GetProductSubscriptionOwnerIdAsync(string azureToken, string subscriptionId, string resourceGroup, string apimName, string product)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", azureToken);

            var apiUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.ApiManagement/service/{apimName}/subscriptions?api-version=2020-06-01-preview";
            var response = await client.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                var subscriptions = JsonConvert.DeserializeObject<SubscriptionListResult>(responseBody);
                List<String> returnStr = new List<string>();
                var subProducts = subscriptions?.Value?.FindAll(s => s.properties.scope.Contains(product));
                if (subProducts != null)
                {
                    foreach (Subscription sub in subProducts) 
                    {
                        int i = sub.properties.OwnerId.LastIndexOf('/');
                        if(i >= 0 && i < sub.properties.OwnerId.Length) {
                            string id = sub.properties.OwnerId.Substring(i + 1);
                            returnStr.Add(id);
                        }
                    }
                    return JsonConvert.SerializeObject(returnStr);

                }
            }

            return null;
        }
    }
}
    public class Subscription
    {   
        public string type {get; set;}
        public string name {get; set;}

        public string id {get; set;}
        public Properties properties { get; set; }
    }

    public class Properties 
    {
     public string OwnerId {get; set;}
     public string scope {get; set;}
     public string displayName {get; set;}
     public string state {get; set;}
     public string createdDate {get; set;}
     public string startDate {get; set;}
     public string expirationDate {get; set;}
     public string endDate {get; set;}
     public string notificationDate {get; set;}
     public string stateComment {get; set;}
     public string allowTracing {get; set;}
     public string allowTracingTill {get; set;}
    }

    public class SubscriptionListResult
    {
        public List<Subscription> Value { get; set; }
    }
