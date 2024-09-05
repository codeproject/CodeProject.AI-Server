using CodeProject.AI.SDK;

using System.Net.Http.Headers;
using System.Text;

var filename = ".\\images\\study-group.jpg";
using var httpClient = new HttpClient()
{
    BaseAddress = new Uri("http://localhost:32168/v1/"),
    Timeout = TimeSpan.FromMinutes(5),
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
Console.WriteLine($"Sending {filename} to 'vision/detection'");
var sentimentPayload = new RequestPayload();
string textValue = "This movie was the worst thing since 'Green Lantern'.";
Console.WriteLine($"\nSending '{textValue}' to 'text/sentiment'.");
sentimentPayload.SetValue("text", textValue);
result = await SendPayload("text/sentiment", sentimentPayload);
Console.WriteLine(result);

Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();

async Task<string> SendPayload( string url, RequestPayload payload)
{
    string json = System.Text.Json.JsonSerializer.Serialize(payload);

    // Create JsonContent with specified media type
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    using var response = await httpClient.PostAsync(url, content);
    var result = await response.Content.ReadAsStringAsync();
    return result;
}

