using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSpecter.Core.Interfaces;
using DataSpecter.UI.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DataSpecter.UI.ViewModels
{
    public partial class FileSimilarityViewModel : ObservableObject
    {
        private readonly IFuzzyHashService _fuzzyHashService;
        private readonly Action<FileModel, long, long>? _navigateToOffsetAction;

        [ObservableProperty]
        private ObservableCollection<FileModel> _availableFiles;

        [ObservableProperty]
        private FileModel? _file1;

        [ObservableProperty]
        private FileModel? _file2;

        [ObservableProperty]
        private string _file1Size = string.Empty;

        [ObservableProperty]
        private string _file2Size = string.Empty;

        [ObservableProperty]
        private bool _isComparing = false;

        [ObservableProperty]
        private int _comparisonProgress = 0;

        [ObservableProperty]
        private string _comparisonStatus = string.Empty;

        [ObservableProperty]
        private bool _hasResults = false;

        [ObservableProperty]
        private int _ssdeepScore = 0;

        [ObservableProperty]
        private double _byteLevelScore = 0.0;

        [ObservableProperty]
        private string _ssdeepScoreDescription = string.Empty;

        [ObservableProperty]
        private string _byteLevelScoreDescription = string.Empty;

        [ObservableProperty]
        private string _fuzzyHash1 = string.Empty;

        [ObservableProperty]
        private string _fuzzyHash2 = string.Empty;

        [ObservableProperty]
        private int _commonChunksCount = 0;

        [ObservableProperty]
        private string _bytesAnalyzed = string.Empty;

        [ObservableProperty]
        private string _longestMatchLength = string.Empty;

        [ObservableProperty]
        private string _matchOffset1 = string.Empty;

        [ObservableProperty]
        private string _matchOffset2 = string.Empty;

        [ObservableProperty]
        private string _analysisTime = string.Empty;

        [ObservableProperty]
        private string _similarityInsight = string.Empty;

        [ObservableProperty]
        private string _fuzzyHashInsight = string.Empty;

        [ObservableProperty]
        private string _byteLevelInsight = string.Empty;

        [ObservableProperty]
        private bool _hasLongestMatch = false;

        private long _longestMatchOffset1;
        private long _longestMatchOffset2;
        private int _longestMatchLen;

        public FileSimilarityViewModel(
            ObservableCollection<FileModel> files, 
            IFuzzyHashService fuzzyHashService,
            Action<FileModel, long, long>? navigateToOffsetAction = null)
        {
            _availableFiles = files;
            _fuzzyHashService = fuzzyHashService;
            _navigateToOffsetAction = navigateToOffsetAction;

            // Set default selections if we have files
            if (files.Count >= 2)
            {
                File1 = files[0];
                File2 = files[1];
            }
            else if (files.Count == 1)
            {
                File1 = files[0];
            }
        }

        partial void OnFile1Changed(FileModel? value)
        {
            File1Size = value?.DataSource != null ? FormatFileSize(value.DataSource.Length) : string.Empty;
        }

        partial void OnFile2Changed(FileModel? value)
        {
            File2Size = value?.DataSource != null ? FormatFileSize(value.DataSource.Length) : string.Empty;
        }

        [RelayCommand]
        private async Task Compare()
        {
            if (File1?.DataSource == null || File2?.DataSource == null)
            {
                MessageBox.Show("Please select two files to compare.", "Selection Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (File1 == File2)
            {
                MessageBox.Show("Please select two different files.", "Invalid Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsComparing = true;
                HasResults = false;
                ComparisonProgress = 0;
                ComparisonStatus = "Initializing comparison...";

                // Step 1: Compute fuzzy hashes
                ComparisonStatus = "Computing fuzzy hash for File 1...";
                ComparisonProgress = 10;
                FuzzyHash1 = await _fuzzyHashService.ComputeFuzzyHashAsync(File1.DataSource);

                ComparisonStatus = "Computing fuzzy hash for File 2...";
                ComparisonProgress = 30;
                FuzzyHash2 = await _fuzzyHashService.ComputeFuzzyHashAsync(File2.DataSource);

                // Step 2: Compare fuzzy hashes
                ComparisonStatus = "Comparing fuzzy hashes...";
                ComparisonProgress = 50;
                SsdeepScore = _fuzzyHashService.CompareFuzzyHashes(FuzzyHash1, FuzzyHash2);

                // Step 3: Byte-level similarity
                ComparisonStatus = "Analyzing byte-level similarity...";
                ComparisonProgress = 60;
                var similarity = await _fuzzyHashService.ComputeByteLevelSimilarityAsync(
                    File1.DataSource, 
                    File2.DataSource,
                    File1.SuffixArray,
                    File1.LcpArray);

                ComparisonProgress = 90;
                ComparisonStatus = "Generating insights...";

                // Populate results
                ByteLevelScore = similarity.SimilarityPercentage;
                CommonChunksCount = similarity.CommonChunksCount;
                BytesAnalyzed = FormatFileSize(similarity.TotalBytesAnalyzed);
                
                _longestMatchLen = similarity.LongestCommonSubstringLength;
                _longestMatchOffset1 = similarity.LongestCommonSubstringOffset1;
                _longestMatchOffset2 = similarity.LongestCommonSubstringOffset2;
                
                LongestMatchLength = _longestMatchLen > 0 
                    ? $"{_longestMatchLen:N0} bytes" 
                    : "No significant matches";
                MatchOffset1 = _longestMatchLen > 0 
                    ? $"0x{_longestMatchOffset1:X8}" 
                    : "N/A";
                MatchOffset2 = _longestMatchLen > 0 
                    ? $"0x{_longestMatchOffset2:X8}" 
                    : "N/A";
                HasLongestMatch = _longestMatchLen > 0;
                
                AnalysisTime = $"{similarity.CalculationTimeMs:N0} ms";

                // Generate descriptions
                GenerateDescriptions();
                GenerateInsights();

                ComparisonProgress = 100;
                ComparisonStatus = "Analysis complete!";

                await Task.Delay(500);
                IsComparing = false;
                HasResults = true;
            }
            catch (Exception ex)
            {
                ComparisonStatus = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to compare files: {ex.Message}", "Comparison Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                IsComparing = false;
            }
        }

        private void GenerateDescriptions()
        {
            // SSDEEP score description
            if (SsdeepScore >= 90)
                SsdeepScoreDescription = "Nearly Identical";
            else if (SsdeepScore >= 70)
                SsdeepScoreDescription = "Highly Similar";
            else if (SsdeepScore >= 50)
                SsdeepScoreDescription = "Moderately Similar";
            else if (SsdeepScore >= 30)
                SsdeepScoreDescription = "Somewhat Similar";
            else if (SsdeepScore >= 10)
                SsdeepScoreDescription = "Slightly Similar";
            else
                SsdeepScoreDescription = "Very Different";

            // Byte-level score description
            if (ByteLevelScore >= 90)
                ByteLevelScoreDescription = "Near-Perfect Match";
            else if (ByteLevelScore >= 70)
                ByteLevelScoreDescription = "Strong Correlation";
            else if (ByteLevelScore >= 50)
                ByteLevelScoreDescription = "Moderate Correlation";
            else if (ByteLevelScore >= 30)
                ByteLevelScoreDescription = "Some Correlation";
            else if (ByteLevelScore >= 10)
                ByteLevelScoreDescription = "Weak Correlation";
            else
                ByteLevelScoreDescription = "Minimal Correlation";
        }

        private void GenerateInsights()
        {
            // Overall similarity insight
            double avgScore = (SsdeepScore + ByteLevelScore) / 2.0;
            
            if (avgScore >= 80)
            {
                SimilarityInsight = "⚠️ HIGH SIMILARITY: These files are substantially similar. " +
                    "If comparing against a known threat, this could indicate a variant or modified version. " +
                    "Recommend thorough investigation of differences.";
            }
            else if (avgScore >= 50)
            {
                SimilarityInsight = "🔍 MODERATE SIMILARITY: Files share significant common elements. " +
                    "Could be related versions, similar file types, or contain common libraries/resources. " +
                    "Further analysis of unique sections recommended.";
            }
            else if (avgScore >= 20)
            {
                SimilarityInsight = "📊 LOW SIMILARITY: Some common patterns detected but files are largely different. " +
                    "May share common file format structures or small code sections.";
            }
            else
            {
                SimilarityInsight = "✓ MINIMAL SIMILARITY: Files appear to be unrelated. " +
                    "No significant matching patterns found beyond random chance.";
            }

            // Fuzzy hash insight
            if (SsdeepScore >= 70)
            {
                FuzzyHashInsight = $"🔐 SSDEEP Analysis: The fuzzy hash match of {SsdeepScore}% indicates files share similar " +
                    "structural patterns and chunk sequences. This is useful for detecting file modifications, " +
                    "where a small portion was changed but overall structure remains similar.";
            }
            else if (SsdeepScore >= 30)
            {
                FuzzyHashInsight = $"🔐 SSDEEP Analysis: Moderate fuzzy hash score ({SsdeepScore}%) suggests some structural " +
                    "similarities but significant differences exist. Files may be loosely related or share common components.";
            }
            else
            {
                FuzzyHashInsight = $"🔐 SSDEEP Analysis: Low fuzzy hash score ({SsdeepScore}%) indicates files have " +
                    "different structural patterns. They are likely unrelated or have been substantially modified.";
            }

            // Byte-level insight
            if (HasLongestMatch && _longestMatchLen >= 1024)
            {
                ByteLevelInsight = $"🎯 Byte-Level Analysis: Found significant matching sequence of {_longestMatchLen:N0} bytes. " +
                    "This could indicate copied code, embedded resources, or common libraries. " +
                    $"The match occurs at offset 0x{_longestMatchOffset1:X8} in File 1 and 0x{_longestMatchOffset2:X8} in File 2. " +
                    "Click 'VIEW LONGEST MATCH' to examine this section.";
            }
            else if (ByteLevelScore >= 50)
            {
                ByteLevelInsight = $"🎯 Byte-Level Analysis: {ByteLevelScore:F1}% chunk similarity detected across {CommonChunksCount:N0} matching chunks. " +
                    "Files share substantial binary content, suggesting they may be versions of the same file or contain significant common data.";
            }
            else if (ByteLevelScore >= 20)
            {
                ByteLevelInsight = $"🎯 Byte-Level Analysis: {ByteLevelScore:F1}% chunk similarity with {CommonChunksCount:N0} matching chunks. " +
                    "Some common binary patterns detected, but files have distinct content.";
            }
            else
            {
                ByteLevelInsight = $"🎯 Byte-Level Analysis: Minimal chunk overlap ({ByteLevelScore:F1}%). " +
                    "Files have very different binary content with no significant matching sequences.";
            }
        }

        [RelayCommand]
        private async Task ExportReport()
        {
            if (!HasResults) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"similarity_report_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".txt",
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine("          FILE SIMILARITY ANALYSIS REPORT");
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine();
                    sb.AppendLine($"Report Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine();
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine("FILES COMPARED");
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine($"File 1: {File1?.Name}");
                    sb.AppendLine($"  Size: {File1Size}");
                    sb.AppendLine();
                    sb.AppendLine($"File 2: {File2?.Name}");
                    sb.AppendLine($"  Size: {File2Size}");
                    sb.AppendLine();
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine("SIMILARITY SCORES");
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine($"Fuzzy Hash (SSDEEP):    {SsdeepScore}% ({SsdeepScoreDescription})");
                    sb.AppendLine($"Byte-Level Analysis:    {ByteLevelScore:F2}% ({ByteLevelScoreDescription})");
                    sb.AppendLine($"Overall Assessment:     {((SsdeepScore + ByteLevelScore) / 2):F2}%");
                    sb.AppendLine();
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine("FUZZY HASHES (SSDEEP)");
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine($"File 1: {FuzzyHash1}");
                    sb.AppendLine($"File 2: {FuzzyHash2}");
                    sb.AppendLine();
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine("BYTE-LEVEL STATISTICS");
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine($"Common Chunks:          {CommonChunksCount:N0}");
                    sb.AppendLine($"Bytes Analyzed:         {BytesAnalyzed}");
                    sb.AppendLine($"Longest Match:          {LongestMatchLength}");
                    sb.AppendLine($"Match Offset (File 1):  {MatchOffset1}");
                    sb.AppendLine($"Match Offset (File 2):  {MatchOffset2}");
                    sb.AppendLine($"Analysis Time:          {AnalysisTime}");
                    sb.AppendLine();
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine("ANALYSIS INSIGHTS");
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine();
                    sb.AppendLine(WrapText(SimilarityInsight, 60));
                    sb.AppendLine();
                    sb.AppendLine(WrapText(FuzzyHashInsight, 60));
                    sb.AppendLine();
                    sb.AppendLine(WrapText(ByteLevelInsight, 60));
                    sb.AppendLine();
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine("        End of Report - DataSpecter Analysis Tool");
                    sb.AppendLine("═══════════════════════════════════════════════════════════");

                    await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
                    MessageBox.Show($"Report exported to:\n{dialog.FileName}", "Export Successful", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export report: {ex.Message}", "Export Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void NavigateToMatch()
        {
            if (!HasLongestMatch || File1 == null) return;

            _navigateToOffsetAction?.Invoke(File1, _longestMatchOffset1, _longestMatchLen);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int order = 0;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F2} {sizes[order]}";
        }

        private string WrapText(string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxWidth)
                return text;

            var sb = new StringBuilder();
            int currentPos = 0;

            while (currentPos < text.Length)
            {
                int remaining = text.Length - currentPos;
                int chunkSize = Math.Min(maxWidth, remaining);
                
                if (remaining > maxWidth)
                {
                    // Try to break at a space
                    int lastSpace = text.LastIndexOf(' ', currentPos + chunkSize, chunkSize);
                    if (lastSpace > currentPos)
                        chunkSize = lastSpace - currentPos;
                }

                sb.AppendLine(text.Substring(currentPos, chunkSize).Trim());
                currentPos += chunkSize;
            }

            return sb.ToString().TrimEnd();
        }
    }
}
