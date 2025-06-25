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
            var evaluator = new SemanticSimilarityEvaluator(@"C:\Users\soora\Downloads\glove.6B\glove.6B.100d.txt", 100);
           
            string reference = "The document titled \"sample 2 pager\" consists of two pages. On the first page, it provides a definition of a sample sentence as a sentence that illustrates the use of a word or grammatical structure in a language [sample 2 pager, Page 1]. The second page contains information about the Tennessee Titans, an American football team based in Nashville, Tennessee. The Titans are part of the National Football League (NFL), competing in the AFC South division, and have won two AFC championships and appeared in one Super Bowl [sample 2 pager, Page 2]";
            string output = "The document \"sample 2 pager\" includes information about sample sentences and the Tennessee Titans.";

            var result = evaluator.Evaluate(reference, output);

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

            EvaluationRequest requestOpenAi = new EvaluationRequest
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

            var result1 = await _evalService.EvaluateAsync(requestGemini);


        }
    }
}
