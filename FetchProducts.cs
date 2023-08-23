using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;


namespace Digitransit.Function
{
    public static class FetchProducts
    {
        [FunctionName("FetchProducts")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "FetchProducts")] HttpRequest req,
            ILogger log)
        {
        log.LogInformation("C# HTTP trigger function processed a request.");

        string resourceGroup =  Environment.GetEnvironmentVariable("RG");
        string apimName = Environment.GetEnvironmentVariable("APIM_NAME");
        string apiVersion = "2022-08-01";
        var subscriptionId = Environment.GetEnvironmentVariable("SUB_ID");
        string apiEndpoint = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.ApiManagement/service/{apimName}/products?api-version={apiVersion}";

        var httpClient = new HttpClient();
        string accessToken = await GetToken(httpClient); 

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.GetAsync(apiEndpoint);

        if (response.IsSuccessStatusCode)
        {   

            var productNames = await response.Content.ReadAsAsync<ProductList>();
            return new OkObjectResult(productNames.Value);
        }
        else
        {
            log.LogError($"API call failed with status code: {response.StatusCode}");
            return new StatusCodeResult((int)response.StatusCode);
        }
    }

        private static async Task<string> GetToken(HttpClient httpClient)
        {
            string clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            string clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
            string tenantId = Environment.GetEnvironmentVariable("TENANT_ID");

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
    }


    public class ProductList
    {
        public Product[] Value { get; set; }
    }

    public class Product
    {
        public string Name { get; set; }
        public string displayName { get; set; }
    }
}