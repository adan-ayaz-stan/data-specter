using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSpecter.UI.Models;
using DataSpecter.Core.Interfaces;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;

namespace DataSpecter.UI.ViewModels
{
    public partial class FileComparisonViewModel : ObservableObject
    {
        private readonly ILcsService? _lcsService;
        private readonly IFuzzyHashService? _fuzzyHashService;

        [ObservableProperty]
        private ObservableCollection<FileModel> _files;
        
        [ObservableProperty]
        private FileModel? _selectedFile1;
        
        [ObservableProperty]
        private FileModel? _selectedFile2;

        [ObservableProperty]
        private ObservableCollection<HexRow>? _hexData1;
        
        [ObservableProperty]
        private ObservableCollection<HexRow>? _hexData2;

        [ObservableProperty]
        private bool _isCalculating;
        
        [ObservableProperty]
        private bool _isResultReady;

        [ObservableProperty]
        private long _lcsLength;
        
        [ObservableProperty]
        private long _lcsOffset1;
        
        [ObservableProperty]
        private long _lcsOffset2;
        
        [ObservableProperty]
        private string _lcsText = string.Empty;

        [ObservableProperty]
        private TimeSpan _comparisonDuration;
        
        [ObservableProperty]
        private long _file1Length;
        
        [ObservableProperty]
        private long _file2Length;

        // Fuzzy Hash Properties
        [ObservableProperty]
        private bool _isFuzzyHashCalculated;

        [ObservableProperty]
        private string _fuzzyHash1 = string.Empty;

        [ObservableProperty]
        private string _fuzzyHash2 = string.Empty;

        [ObservableProperty]
        private int _ssdeepScore = 0;

        [ObservableProperty]
        private double _byteLevelScore = 0.0;

        [ObservableProperty]
        private string _ssdeepScoreDescription = string.Empty;

        [ObservableProperty]
        private string _byteLevelScoreDescription = string.Empty;

        [ObservableProperty]
        private int _commonChunksCount = 0;

        [ObservableProperty]
        private string _similarityInsight = string.Empty;

        // Scroll properties for HexViewers
        [ObservableProperty]
        private long _scrollOffset1;

        [ObservableProperty]
        private long _scrollOffset2;

        public FileComparisonViewModel(ObservableCollection<FileModel> files, ILcsService? lcsService = null, IFuzzyHashService? fuzzyHashService = null)
        {
             _files = files;
             _lcsService = lcsService;
             _fuzzyHashService = fuzzyHashService;
        }

        [RelayCommand]
        private async Task Compare()
        {
            if (SelectedFile1?.DataSource == null || SelectedFile2?.DataSource == null || _lcsService == null) return;
            
            IsCalculating = true;
            IsResultReady = false;
            
            // Store file lengths for visualization
            File1Length = SelectedFile1.DataSource.Length;
            File2Length = SelectedFile2.DataSource.Length;
            
            // Allow UI to update and show spinner
            await Task.Delay(50);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _lcsService.FindLcsAsync(SelectedFile1.DataSource, SelectedFile2.DataSource);
            stopwatch.Stop();
            
            ComparisonDuration = stopwatch.Elapsed;
            LcsLength = result.length;
            LcsOffset1 = result.offset1;
            LcsOffset2 = result.offset2;

            // Load highlighted previews
            // Offload to background thread to prevent UI freeze during collection population
            var list1 = await Task.Run(() => LoadPreviewRows(SelectedFile1.DataSource, result.offset1, result.length));
            var list2 = await Task.Run(() => LoadPreviewRows(SelectedFile2.DataSource, result.offset2, result.length));

            HexData1 = new ObservableCollection<HexRow>(list1);
            HexData2 = new ObservableCollection<HexRow>(list2);
            
            // Extract text
            if (result.length > 0)
            {
                byte[] textBuf = new byte[Math.Min(result.length, 100)]; // Limit text preview
                SelectedFile1.DataSource.ReadRange(result.offset1, textBuf, 0, textBuf.Length);
                
                // Simple ASCII filtering
                char[] chars = new char[textBuf.Length];
                for(int i=0; i<textBuf.Length; i++)
                {
                    byte b = textBuf[i];
                    chars[i] = (b >= 32 && b <= 126) ? (char)b : '.';
                }
                LcsText = new string(chars);
                if (result.length > 100) LcsText += "...";
            }
            else
            {
                LcsText = "(No Match)";
            }
            
            IsCalculating = false;
            IsResultReady = true;

            // Calculate fuzzy hashes if service is available
            if (_fuzzyHashService != null)
            {
                await CalculateFuzzyHashesAsync();
            }
        }

        private async Task CalculateFuzzyHashesAsync()
        {
            if (SelectedFile1?.DataSource == null || SelectedFile2?.DataSource == null || _fuzzyHashService == null)
                return;

            try
            {
                // Compute fuzzy hashes
                FuzzyHash1 = await _fuzzyHashService.ComputeFuzzyHashAsync(SelectedFile1.DataSource);
                FuzzyHash2 = await _fuzzyHashService.ComputeFuzzyHashAsync(SelectedFile2.DataSource);

                // Compare fuzzy hashes
                SsdeepScore = _fuzzyHashService.CompareFuzzyHashes(FuzzyHash1, FuzzyHash2);

                // Byte-level similarity
                var similarity = await _fuzzyHashService.ComputeByteLevelSimilarityAsync(
                    SelectedFile1.DataSource,
                    SelectedFile2.DataSource,
                    SelectedFile1.SuffixArray,
                    SelectedFile1.LcpArray);

                ByteLevelScore = similarity.SimilarityPercentage;
                CommonChunksCount = similarity.CommonChunksCount;

                // Generate descriptions
                GenerateFuzzyHashDescriptions();
                GenerateSimilarityInsight();

                IsFuzzyHashCalculated = true;
            }
            catch (Exception)
            {
                // Silently fail fuzzy hash calculation - LCS still works
                IsFuzzyHashCalculated = false;
            }
        }

        private void GenerateFuzzyHashDescriptions()
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

        private void GenerateSimilarityInsight()
        {
            double avgScore = (SsdeepScore + ByteLevelScore) / 2.0;

            if (avgScore >= 80)
            {
                SimilarityInsight = "⚠️ HIGH SIMILARITY: Files are substantially similar. If comparing against known threats, this could indicate a variant or modified version.";
            }
            else if (avgScore >= 50)
            {
                SimilarityInsight = "🔍 MODERATE SIMILARITY: Files share significant common elements. May be related versions or contain common libraries.";
            }
            else if (avgScore >= 20)
            {
                SimilarityInsight = "📊 LOW SIMILARITY: Some common patterns detected but files are largely different.";
            }
            else
            {
                SimilarityInsight = "✓ MINIMAL SIMILARITY: Files appear unrelated with no significant matching patterns.";
            }
        }

        private System.Collections.Generic.List<HexRow> LoadPreviewRows(DataSpecter.Core.Models.BinaryDataSource source, long matchOffset, long matchLength)
        {
            var rows = new System.Collections.Generic.List<HexRow>();
            int previewSize = 4096;
            byte[] buf = new byte[previewSize];
            int read = source.ReadRange(0, buf, 0, previewSize);
            
            int bytesPerRow = 16;
            for (int i = 0; i < read; i += bytesPerRow)
            {
                var rowItems = new System.Collections.Generic.List<ByteItem>();
                for (int j = 0; j < bytesPerRow && (i + j) < read; j++)
                {
                    int index = i + j;
                    bool isHigh = (index >= matchOffset && index < matchOffset + matchLength);
                    rowItems.Add(new ByteItem(buf[index], index, isHigh));
                }
                rows.Add(new HexRow(i, rowItems));
            }
            return rows;
        }

        [RelayCommand]
        private void ScrollToMatch()
        {
            // Update scroll offsets to trigger scrolling in HexViewers
            ScrollOffset1 = LcsOffset1;
            ScrollOffset2 = LcsOffset2;
        }
    }
}
