using System.Text;
using System.Text.Json;

// Main entry point: runs the extraction process for each image URL
await RunAsync();

// Asynchronous method to process all image URLs
static async Task RunAsync()
{
    // Azure endpoint and credentials (replace with your actual values)
    var endpoint = "<your-endpoint>/contentunderstanding/analyzers/<your-analyzer-name>:analyze?api-version=2024-12-01-preview";
    var subscriptionKey = "<your-content-understanding-key>";
    var imageUrls = new[] { "<your-image-url_01>", "<your-image-url_02>" };

    // Use a single HttpClient instance for all requests
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

    foreach (var imageUrl in imageUrls)
    {
        // Prepare the request body for the POST request
        var requestBody = $"{{\"url\":\"{imageUrl}\"}}";
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        // Send POST request to start extraction
        var postResponse = await client.PostAsync(endpoint, content);
        postResponse.EnsureSuccessStatusCode();

        // Retrieve the Operation-Location header for polling
        if (!postResponse.Headers.TryGetValues("Operation-Location", out var values))
        {
            Console.WriteLine("Operation-Location header not found. Skipping this image.");
            continue;
        }
        var operationLocation = values.First();
        Console.WriteLine($"Operation-Location: {operationLocation}");

        ExtractionOperation? extractionOperation = null;
        // Poll the operation status until it is 'Succeeded'
        do
        {
            var getRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            getRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var getResponse = await client.SendAsync(getRequest);
            getResponse.EnsureSuccessStatusCode();
            var result = await getResponse.Content.ReadAsStringAsync();

            extractionOperation = JsonSerializer.Deserialize<ExtractionOperation>(result);
            if (extractionOperation == null)
            {
                Console.WriteLine("Failed to parse ExtractionOperation. Skipping this image.");
                break;
            }
            if (extractionOperation.status != "Succeeded")
            {
                Console.WriteLine($"Status: {extractionOperation.status}. Waiting before retry...");
                await Task.Delay(3000); // Wait 3 seconds before retrying
            }
        } while (extractionOperation.status != "Succeeded");

        // Output the extracted content if available
        if (extractionOperation?.result?.contents != null && extractionOperation.result.contents.Count > 0)
        {
            Console.WriteLine($"Parsed ExtractionOperation: status={extractionOperation.status}, contents={extractionOperation.result.contents[0].markdown}");
        }
        else
        {
            Console.WriteLine("No contents found in extraction result.");
        }
    }
}

// Model for the extraction operation response
class ExtractionOperation
{
    public string? id { get; set; }
    public string? status { get; set; }
    public ExtractionResult? result { get; set; }
}

// Model for the result property in the extraction operation
class ExtractionResult
{
    public List<ContentResult>? contents { get; set; }
}

// Model for each content item in the extraction result
class ContentResult
{
    public string? markdown { get; set; }
    public string? kind { get; set; }
}