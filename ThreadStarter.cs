using ClustersCopyAndAnalyze.Services.Clusters;
using ClustersCopyAndAnalyze.Services.Copy;

namespace ClustersCopyAndAnalyze
{
    internal class ThreadStarter
    {
        private Progress<ТипПрогрессаАнализатора> analyzeProgress;
        private Progress<double> copyProgress;
        private ClusterAnalyzeService clusterAnalyzerService = new();

        public ThreadStarter(Progress<ТипПрогрессаАнализатора> analyzeProgress, Progress<double> copyProgress)
        {
            this.analyzeProgress = analyzeProgress;
            this.copyProgress = copyProgress;
        }

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
                var beforeData = await clusterAnalyzerService.AnalyzeClusterAsync(sourcePath, token, analyzeProgress);
                
                new TableView(beforeData, "Таблица до выполенния операций");
            }
            catch(Exception ex){
                MessageBox.Show(ex.Message);
            }
            await CopyingService.CopyDirectoryAsync(sourcePath, targetPath, copyProgress);
            var afterData = await clusterAnalyzerService.AnalyzeClusterAsync(targetPath, token, analyzeProgress);
            new TableView(afterData, "Таблица после выполенния операций");
        }
    }
}