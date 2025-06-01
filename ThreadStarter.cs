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
        private Progress<ClusterAnalyzerService.AnalyzerProgressType> analyzeProgress;
        private Progress<double> copyProgress;

        public ThreadStarter(Progress<ClusterAnalyzerService.AnalyzerProgressType> analyzeProgress, Progress<double> copyProgress)
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
            // Task analyzeOrigin = ClusterAnalyzerService.DirectoryAnalyzeAsync(sourcePath, analyzeProgress);
            // Task copy = CopyingService.CopyDirectoryAsync(sourcePath, targetPath, copyProgress);
            // await Task.WhenAll(analyzeOrigin, copy);
            try{
                var beforeData = await ClusterAnalyzerService.AnalyzeFileClustersAsync(sourcePath);
                var tableBefore = ClusterAnalyzerService.FormatToDT(beforeData);
                new TableView(tableBefore, "Таблица до выполенния операций");
            }
            catch(Exception ex){
                MessageBox.Show(ex.Message);
            }
            await CopyingService.CopyDirectoryAsync(sourcePath, targetPath, copyProgress);
            var afterData = await ClusterAnalyzerService.AnalyzeFileClustersAsync(targetPath);
            var tableAfter = ClusterAnalyzerService.FormatToDT(afterData);
            new TableView(tableAfter, "Таблица после выполенния операций");
        }
    }
}