using LLMEval;
using System.Data.Common;

namespace LLMEvalTool
{
    public partial class Form1 : Form
    {
        private readonly IEvaluationService _evalService;
        public Form1()
        {
            InitializeComponent();
           // 1.Create an instance of the AiProviderFactory
            IAiProviderFactory providerFactory = new AiProviderFactory();

            // 2. Create an instance of AdvancedEvaluationService, passing the factory and HttpClient
             _evalService = new AdvancedEvaluationService(providerFactory);

        }

        private async void button1_Click(object sender, EventArgs e)
        {
            Dictionary<string, string> config = new Dictionary<string, string>();
            config.Add("ApiKey", "AIzaSyCtZw_toeN4wcjp_FKzZ85vpwF98z_REtM");
            config.Add("Model", "mistral");

            EvaluationRequest requestGemini = new EvaluationRequest
            {
                Question = "Capital of France",
                AiResponse = "Paris, France",
                GoldenOutput = "Paris",
                ProviderType = ProviderType.Gemini,
                Endpoint = @"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
                Configuration = config,
                MatchingType = "Semantic",
                PassThreshold = .9,
                EvaluationType = EvaluationType.LLMAsJudge,
                IsReferenceDoc = false
            };

            EvaluationRequest requestOllama = new EvaluationRequest
            {
                Question = "Capital of France",
                AiResponse = "Paris, France",
                GoldenOutput = "Paris",
                ProviderType = ProviderType.Ollama,
                Endpoint = @"http://localhost:11434/",
                Configuration = config,
                MatchingType = "Semantic",
                PassThreshold = .9,
                EvaluationType = EvaluationType.LLMAsJudge,
                ModelName = "mistral",
                IsReferenceDoc = false
            };

            var result = await _evalService.EvaluateAsync(requestGemini);


        }
    }
}
