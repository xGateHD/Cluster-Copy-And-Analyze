using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
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
            // Task analyzeOrigin = ClusterAnalyzerService.DirectoryAnalyzeAsync(sourcePath, analyzeProgress);
            // Task copy = CopyingService.CopyDirectoryAsync(sourcePath, targetPath, copyProgress);
            // await Task.WhenAll(analyzeOrigin, copy);
            try{
                var beforeData = await clusterAnalyzerService.AnalyzeClusterAsync(sourcePath, token);
                var tableBefore = ClusterAnalyzeService.FormatToDT(beforeData);
                new TableView(tableBefore, "Таблица до выполенния операций");
            }
            catch(Exception ex){
                MessageBox.Show(ex.Message);
            }
            await CopyingService.CopyDirectoryAsync(sourcePath, targetPath, copyProgress);
            var afterData = await clusterAnalyzerService.AnalyzeClusterAsync(targetPath, token);
            var tableAfter = ClusterAnalyzeService.FormatToDT(afterData);
            new TableView(tableAfter, "Таблица после выполенния операций");
        }
    }
}