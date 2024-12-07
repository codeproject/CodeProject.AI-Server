
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using CodeProject.AI.SDK.Common;

// A demonstration of sending a request to CodeProject.AI Server as JSON instead
// of FormData.

string filename = "./images/study-group.jpg";
using var httpClient = new HttpClient()
{
    BaseAddress = new Uri("http://localhost:32168/v1/"),
    Timeout     = TimeSpan.FromSeconds(30),
};
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


string result;

// Image detection
Console.WriteLine($"Sending {filename} to 'vision/detection'");
var detectPayload = new RequestPayload();
detectPayload.AddFile(filename);
result = await SendPayload("vision/detection", detectPayload);
Console.WriteLine(result);

// Sentiment Analysis
Console.WriteLine($"Sending {filename} to 'text/sentiment'");
var sentimentPayload = new RequestPayload();
string textValue = "This movie was the worst thing since 'Green Lantern'.";
Console.WriteLine($"\nSending '{textValue}' to 'text/sentiment'.");
sentimentPayload.SetValue("text", textValue);
result = await SendPayload("text/sentiment", sentimentPayload);
Console.WriteLine(result);

Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();

/// <summary>
/// Sends a JSON payload to CodeProject.AI Server and returns the result as a
/// JSON string.
/// </summary>
/// <param name="url">The URL of the AI server endpoint</param>
/// <param name="payload">The request payload</param>
/// <returns>The result as a JSON string</returns>
async Task<string> SendPayload( string url, RequestPayload payload)
{
    string json = System.Text.Json.JsonSerializer.Serialize(payload);

    // Create JsonContent with specified media type
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    using var response = await httpClient.PostAsync(url, content);
    var result = await response.Content.ReadAsStringAsync();
    return result;
}