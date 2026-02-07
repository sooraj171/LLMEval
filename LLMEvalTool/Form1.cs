using LLMEval;

namespace LLMEvalTool
{
    public partial class Form1 : Form
    {
        private readonly IEvaluationService _evalService;

        public Form1()
        {
            InitializeComponent();
            IAiProviderFactory providerFactory = new AiProviderFactory();
            _evalService = new AdvancedEvaluationService(providerFactory);
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            try
            {
                // Use environment variables for API keys - set LLMEVAL_GEMINI_API_KEY, LLMEVAL_OPENAI_API_KEY as needed
                string geminiApiKey = Environment.GetEnvironmentVariable("LLMEVAL_GEMINI_API_KEY") ?? "";
                string openAiApiKey = Environment.GetEnvironmentVariable("LLMEVAL_OPENAI_API_KEY") ?? "";

                var configGemini = new Dictionary<string, string>
                {
                    ["ApiKey"] = geminiApiKey,
                    ["Model"] = "gemini-2.0-flash"
                };

                var configOllama = new Dictionary<string, string>
                {
                    ["Model"] = "mistral"
                };

                var configOpenAi = new Dictionary<string, string>
                {
                    ["ApiKey"] = openAiApiKey,
                    ["Model"] = "gpt-3.5-turbo"
                };

                EvaluationRequest requestGemini = new EvaluationRequest
                {
                    Question = "Capital of France",
                    AiResponse = "Paris, France",
                    GoldenOutput = "Paris",
                    ProviderType = ProviderType.Gemini,
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
                    Configuration = configGemini,
                    MatchingType = "semantic",
                    PassThreshold = 0.9,
                    EvaluationType = EvaluationType.LLMAsJudge,
                    IsReferenceDoc = false
                };

                EvaluationRequest requestOllama = new EvaluationRequest
                {
                    Question = "Capital of France",
                    AiResponse = "Paris, France",
                    GoldenOutput = "Paris",
                    ProviderType = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434/",
                    Configuration = configOllama,
                    MatchingType = "semantic",
                    PassThreshold = 0.9,
                    EvaluationType = EvaluationType.LLMAsJudge,
                    ModelName = "mistral",
                    IsReferenceDoc = false
                };

                EvaluationRequest requestOpenAi = new EvaluationRequest
                {
                    Question = "Capital of France",
                    AiResponse = "Paris, France",
                    GoldenOutput = "Paris",
                    ProviderType = ProviderType.OpenAI,
                    Endpoint = "https://api.openai.com/v1/chat/completions",
                    Configuration = configOpenAi,
                    MatchingType = "semantic",
                    PassThreshold = 0.9,
                    EvaluationType = EvaluationType.LLMAsJudge,
                    ModelName = "gpt-3.5-turbo",
                    IsReferenceDoc = false
                };

                // Run DirectEvaluation (no API key needed) - uses TF-IDF semantic similarity
                EvaluationRequest requestDirect = new EvaluationRequest
                {
                    Question = "Capital of France",
                    AiResponse = "Paris, France",
                    GoldenOutput = "Paris",
                    ProviderType = ProviderType.Gemini,
                    Endpoint = "",
                    Configuration = new Dictionary<string, string>(),
                    MatchingType = "semantic",
                    PassThreshold = 0.5,
                    EvaluationType = EvaluationType.DirectEvaluation,
                    IsReferenceDoc = false
                };

                var directResult = await _evalService.EvaluateAsync(requestDirect);
                MessageBox.Show($"Direct Evaluation (TF-IDF): Score={directResult.Score:F2}, Passed={directResult.IsPassed}\nDetails: {directResult.Details}",
                    "Evaluation Result", MessageBoxButtons.OK);

                // Try Gemini if API key is configured
                if (!string.IsNullOrEmpty(geminiApiKey))
                {
                    var resultGemini = await _evalService.EvaluateAsync(requestGemini);
                    MessageBox.Show($"Gemini LLM-as-Judge: Score={resultGemini.Score:F2}, Passed={resultGemini.IsPassed}\nDetails: {resultGemini.Details}",
                        "Gemini Result", MessageBoxButtons.OK);
                }
                else
                {
                    MessageBox.Show("Set LLMEVAL_GEMINI_API_KEY environment variable to test Gemini evaluation.",
                        "Configuration", MessageBoxButtons.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Evaluation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
