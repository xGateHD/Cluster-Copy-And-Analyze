using ClusterAnalyzer;
using ClustersCopyAndAnalyze.Services.Copy;

namespace ClustersCopyAndAnalyze
{
    internal class ThreadStarter(Progress<AnalysisPhase> analyzeProgress, Progress<double> copyProgress)
    {
        private readonly ClusterAnalyzeService clusterAnalyzerService = new();

        public void TryStartOperation(string sourcePath, string targetPath)
        {
            if (!CopyingService.ValidatePaths(sourcePath, targetPath, out string errorMessage))
            {
                MessageBox.Show(errorMessage);
                return;
            }

            StartOperations(sourcePath, targetPath);
        }

        public async void StartOperations(string sourcePath, string targetPath)
        {
            CancellationTokenSource cts = new();
            var token = cts.Token;
            try{
                var beforeData = await clusterAnalyzerService.AnalyzeFAT32Cluster(sourcePath, token, analyzeProgress);

                _ = new TableView(beforeData, "Таблица до выполенния операций");
            }
            catch(Exception ex){
                MessageBox.Show(ex.Message);
            }
            await CopyingService.CopyDirectoryAsync(sourcePath, targetPath, copyProgress);
            var afterData = await clusterAnalyzerService.AnalyzeFAT32Cluster(targetPath, token, analyzeProgress);
            _ = new TableView(afterData, "Таблица после выполенния операций");
        }
    }
}