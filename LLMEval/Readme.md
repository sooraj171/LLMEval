# AI Response Evaluation Library Help

This document provides a guide on how to use the AI Response Evaluation Library. This library allows you to evaluate the responses of AI applications against expected golden outputs and, optionally, reference documents, using various AI models as judges (currently supporting Ollama, OpenAI, and Gemini).

## Getting Started

### Installation

1.  **Add NuGet Package:** In your C# project, add a reference to the `STAF.LLMEval` NuGet package. You can do this using the NuGet Package Manager in Visual Studio or by using the .NET CLI:

    ```bash
    dotnet add package STAF.LLMEval
    ```

### Configuration

The library relies on configuration for API endpoints and, in some cases, API keys.

* **API Endpoints:** You'll need to provide the specific API endpoints for the AI models you intend to use (Ollama, OpenAI, Gemini). These can typically be set within your application's configuration (e.g., `appsettings.json`) or directly in your code when creating the `EvaluationRequest`.
* **API Keys:** For OpenAI and Gemini, you will need to provide API keys. It is **strongly recommended** to handle these securely using environment variables or user secrets (for development) instead of hardcoding them or including them directly in the `EvaluationRequest` for production use.

## Core Concepts

### `EvaluationRequest`

This class represents the input for evaluating an AI response. It contains the following properties:

* `Question` (string): The original question asked to the AI application.
* `AiResponse` (string): The response received from the AI application.
* `GoldenOutput` (string): The expected, correct response or the reference document.
* `ProviderType` (`enum`): Specifies the AI provider (`Ollama`, `OpenAI`, `Gemini`).
* `Endpoint` (string): The API endpoint for the selected `ProviderType`.
* `Configuration` (`Dictionary<string, string>`): A dictionary to hold provider-specific configurations, such as API keys (use with caution in production).
* `PassThreshold` (double): A numerical threshold (between 0 and 1) that the evaluation score must meet or exceed for the evaluation to be considered a pass.
* `EvaluationType` (`enum`): Specifies the type of evaluation:
    * `DirectComparison`: Evaluates the `AiResponse` against the `GoldenOutput` using internal logic (e.g., exact match, keyword, semantic).
    * `LLMAsJudge`: Uses the specified AI model to evaluate the `AiResponse` based on the `Question` and `GoldenOutput` (and optionally `IsReferenceDocument`).
* `IsReferenceDocument` (bool): A flag indicating whether the `GoldenOutput` should be treated as a reference document for the `AiResponse`.

### `EvaluationResult`

This class represents the output of the evaluation. It contains the following properties:

* `Score` (double): A numerical score (typically between 0 and 1) indicating the quality or correctness of the `AiResponse`.
* `IsPassed` (bool): A boolean indicating whether the `Score` meets or exceeds the `PassThreshold`.
* `Details` (string): Additional information or reasoning for the evaluation, often provided by the LLM judge.

### `IEvaluationService` and `AdvancedEvaluationService`

The `IEvaluationService` interface defines the contract for performing evaluations. `AdvancedEvaluationService` is the concrete implementation that handles both direct comparisons and using LLMs as judges.

### `IAiProvider` and Implementations (`OllamaProvider`, `OpenAIProvider`, `GeminiProvider`)

The `IAiProvider` interface defines how to interact with different AI models. The concrete implementations handle the specific API calls for each provider.

### `LLMResponseParser`

This class contains static methods to parse the responses from the LLM judges (Ollama and Gemini) to extract the evaluation score and reasoning.

## Usage

1.  **Create an `EvaluationRequest` object:** Populate the properties of this object with the necessary information, including the question, AI response, golden output (or reference document), provider type, endpoint, and your desired evaluation settings.

    ```csharp
    var request = new EvaluationRequest
    {
        Question = "What is the capital of France?",
        AiResponse = "Paris, France",
        GoldenOutput = "Paris",
        ProviderType = ProviderType.Gemini,
        Endpoint = "your_gemini_endpoint",
        Configuration = new Dictionary<string, string> { { "ApiKey", "YOUR_API_KEY" } }, // Secure this!
        PassThreshold = 0.8,
        EvaluationType = EvaluationType.LLMAsJudge,
        IsReferenceDocument = false
    };
    ```

2.  **Instantiate `AdvancedEvaluationService`:** Create an instance of the evaluation service, providing instances of the provider implementations. It's recommended to use Dependency Injection for managing these dependencies in larger applications.

    ```csharp
    var ollamaProvider = new OllamaProvider(new HttpClient());
    var openAiProvider = new OpenAIProvider(new HttpClient());
    var geminiProvider = new GeminiProvider(new HttpClient());
    var evaluationService = new AdvancedEvaluationService(ollamaProvider, openAiProvider, geminiProvider);
    ```

3.  **Call `EvaluateAsync`:** Call the `EvaluateAsync` method of the `AdvancedEvaluationService` with your `EvaluationRequest` object. This will return an `EvaluationResult`.

    ```csharp
    EvaluationResult result = await evaluationService.EvaluateAsync(request);

    Console.WriteLine($"Score: {result.Score}");
    Console.WriteLine($"Passed: {result.IsPassed}");
    Console.WriteLine($"Details: {result.Details}");
    ```

## Security Considerations

* **API Keys:** Handle API keys with extreme care. Avoid hardcoding them in your application. Use environment variables, user secrets (for development), or dedicated secret management services (for production).
* **Endpoint Security:** Ensure the API endpoints you are using are secure (HTTPS).
* **Input Validation:** Sanitize and validate all input data to prevent potential injection vulnerabilities.

## Error Handling

The library includes basic error handling within the provider implementations and the evaluation service. Be prepared to catch exceptions and handle potential issues such as network errors, invalid API responses, or missing configuration. The `EvaluationResult.Details` property often provides more specific error information.

## Contributing

Send me a note in the community if you want to contribute to this library. I am open to suggestions, improvements, and bug fixes.

## License

MIT License

Copyright (c) 2025 Sooraj Ramachandran

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.