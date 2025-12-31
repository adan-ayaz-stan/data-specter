using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DataSpecter.UI.ViewModels
{
    public partial class EntropyAnalysisViewModel : ObservableObject
    {
        private readonly BinaryDataSource _dataSource;
        private readonly IEntropyService _entropyService;

        [ObservableProperty]
        private bool _isCalculating = true;

        [ObservableProperty]
        private int _calculationProgress = 0;

        [ObservableProperty]
        private string _calculationStatus = "Initializing...";

        [ObservableProperty]
        private bool _hasResults = false;

        [ObservableProperty]
        private double[]? _entropyData;

        [ObservableProperty]
        private double[]? _rawEntropyData; // Keep raw data for export

        [ObservableProperty]
        private double[]? _byteFrequencies;

        [ObservableProperty]
        private double _averageEntropy;

        [ObservableProperty]
        private double _minEntropy;

        [ObservableProperty]
        private double _maxEntropy;

        [ObservableProperty]
        private double _stdDevEntropy;

        [ObservableProperty]
        private int _chunkCount;

        [ObservableProperty]
        private string _chunkSize = string.Empty;

        [ObservableProperty]
        private string _fileName;

        [ObservableProperty]
        private string _fileSize = string.Empty;

        [ObservableProperty]
        private string _compressionHint = string.Empty;

        [ObservableProperty]
        private string _encryptionHint = string.Empty;

        [ObservableProperty]
        private string _dataTypeHint = string.Empty;

        public EntropyAnalysisViewModel(BinaryDataSource dataSource, IEntropyService entropyService, string fileName)
        {
            _dataSource = dataSource;
            _entropyService = entropyService;
            FileName = fileName;
            FileSize = FormatFileSize(dataSource.Length);
        }

        public async Task StartAnalysisAsync()
        {
            try
            {
                IsCalculating = true;
                HasResults = false;
                CalculationProgress = 0;
                CalculationStatus = "Calculating entropy...";

                // Create progress reporter
                var progress = new Progress<(int current, int total)>(report =>
                {
                    CalculationProgress = (int)((report.current * 50.0) / report.total); // First 50%
                    CalculationStatus = $"Analyzing entropy: {report.current:N0} / {report.total:N0} chunks";
                });

                // Calculate entropy with dynamic chunk sizing
                var entropyResult = await CalculateEntropyWithProgressAsync(progress);
                RawEntropyData = entropyResult.data;
                ChunkSize = FormatFileSize(entropyResult.chunkSize);
                ChunkCount = entropyResult.data.Length;

                CalculationProgress = 50;
                CalculationStatus = "Calculating byte frequencies...";

                // Calculate byte frequency distribution
                var frequencies = await CalculateByteFrequenciesAsync();
                
                CalculationProgress = 75;
                CalculationStatus = "Analyzing data patterns...";

                // Calculate statistics
                CalculateStatistics(RawEntropyData);

                // Downsample for display (limit to 500 bars)
                EntropyData = DownsampleData(RawEntropyData, 500);

                // Normalize byte frequencies for display (0-8 scale like entropy)
                ByteFrequencies = NormalizeFrequencies(frequencies);

                CalculationProgress = 90;
                CalculationStatus = "Generating insights...";

                // Generate file insights
                GenerateInsights();

                CalculationProgress = 100;
                CalculationStatus = "Analysis complete!";
                HasResults = true;

                await Task.Delay(500);
                IsCalculating = false;
            }
            catch (Exception ex)
            {
                CalculationStatus = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to analyze file: {ex.Message}", "Analysis Error", MessageBoxButton.OK, MessageBoxImage.Error);
                IsCalculating = false;
            }
        }

        private async Task<(double[] data, int chunkSize)> CalculateEntropyWithProgressAsync(IProgress<(int current, int total)> progress)
        {
            return await Task.Run(() =>
            {
                long length = _dataSource.Length;

                // Calculate appropriate chunk size to limit memory usage
                const int maxInitialChunks = 10000;
                const int minChunkSize = 1024;
                const int maxChunkSize = 1024 * 1024;

                int chunkSize = (int)Math.Min(maxChunkSize, Math.Max(minChunkSize, length / maxInitialChunks));
                int numChunks = (int)((length + chunkSize - 1) / chunkSize);

                double[] entropyValues = new double[numChunks];
                byte[] buffer = new byte[chunkSize];

                int reportInterval = Math.Max(1, numChunks / 100);

                for (int i = 0; i < numChunks; i++)
                {
                    long offset = (long)i * chunkSize;
                    int read = _dataSource.ReadRange(offset, buffer, 0, chunkSize);

                    if (read > 0)
                    {
                        entropyValues[i] = CalculateShannonEntropy(buffer, read);
                    }

                    if (i % reportInterval == 0 || i == numChunks - 1)
                    {
                        progress?.Report((i + 1, numChunks));
                    }
                }

                return (entropyValues, chunkSize);
            });
        }

        private async Task<long[]> CalculateByteFrequenciesAsync()
        {
            return await Task.Run(() =>
            {
                long[] frequencies = new long[256];
                byte[] buffer = new byte[65536]; // 64KB buffer for fast reading
                long offset = 0;
                long length = _dataSource.Length;

                // Sample up to 10MB for byte frequency (faster for large files)
                long maxBytesToSample = Math.Min(length, 10 * 1024 * 1024);

                while (offset < maxBytesToSample)
                {
                    int toRead = (int)Math.Min(buffer.Length, maxBytesToSample - offset);
                    int read = _dataSource.ReadRange(offset, buffer, 0, toRead);
                    
                    if (read == 0) break;

                    for (int i = 0; i < read; i++)
                    {
                        frequencies[buffer[i]]++;
                    }

                    offset += read;
                }

                return frequencies;
            });
        }

        private double CalculateShannonEntropy(byte[] buffer, int count)
        {
            if (count == 0) return 0.0;

            int[] frequencies = new int[256];
            for (int i = 0; i < count; i++)
            {
                frequencies[buffer[i]]++;
            }

            double entropy = 0.0;
            double log2 = Math.Log(2);

            for (int i = 0; i < 256; i++)
            {
                if (frequencies[i] > 0)
                {
                    double p = (double)frequencies[i] / count;
                    entropy -= p * (Math.Log(p) / log2);
                }
            }

            return entropy;
        }

        private void CalculateStatistics(double[] data)
        {
            if (data == null || data.Length == 0)
            {
                AverageEntropy = MinEntropy = MaxEntropy = StdDevEntropy = 0;
                return;
            }

            AverageEntropy = data.Average();
            MinEntropy = data.Min();
            MaxEntropy = data.Max();

            // Calculate standard deviation
            double sumOfSquares = data.Sum(val => Math.Pow(val - AverageEntropy, 2));
            StdDevEntropy = Math.Sqrt(sumOfSquares / data.Length);
        }

        private double[] DownsampleData(double[] data, int maxBars)
        {
            if (data.Length <= maxBars) return data;

            double ratio = (double)data.Length / maxBars;
            double[] result = new double[maxBars];

            for (int i = 0; i < maxBars; i++)
            {
                int startIdx = (int)(i * ratio);
                int endIdx = (int)((i + 1) * ratio);
                
                double sum = 0;
                int count = 0;
                for (int j = startIdx; j < endIdx && j < data.Length; j++)
                {
                    sum += data[j];
                    count++;
                }
                
                result[i] = count > 0 ? sum / count : 0;
            }

            return result;
        }

        private double[] NormalizeFrequencies(long[] frequencies)
        {
            if (frequencies.Length == 0) return Array.Empty<double>();

            long max = frequencies.Max();
            if (max == 0) return frequencies.Select(_ => 0.0).ToArray();

            // Normalize to 0-8 scale (matching entropy scale)
            return frequencies.Select(f => (f / (double)max) * 8.0).ToArray();
        }

        private void GenerateInsights()
        {
            // Analyze compression potential
            if (AverageEntropy < 3.0)
            {
                CompressionHint = "🔹 Low entropy detected. File contains repetitive patterns and could compress significantly.";
            }
            else if (AverageEntropy < 6.0)
            {
                CompressionHint = "🔹 Moderate entropy. File may benefit from compression.";
            }
            else
            {
                CompressionHint = "🔹 High entropy. File is likely already compressed or encrypted, or contains random data.";
            }

            // Analyze encryption/randomness
            if (AverageEntropy > 7.5 && StdDevEntropy < 0.3)
            {
                EncryptionHint = "🔐 Very high uniform entropy suggests encrypted or highly compressed data.";
            }
            else if (AverageEntropy > 7.0)
            {
                EncryptionHint = "🔐 High entropy with variation suggests mixed content or partial encryption.";
            }
            else
            {
                EncryptionHint = "🔓 Entropy patterns suggest unencrypted data.";
            }

            // Analyze data type
            if (ByteFrequencies != null)
            {
                double nullByteFreq = ByteFrequencies[0];
                double printableFreq = 0;
                
                // Sum frequencies of printable ASCII range (0x20-0x7E)
                for (int i = 0x20; i <= 0x7E; i++)
                {
                    printableFreq += ByteFrequencies[i];
                }

                if (printableFreq > 5.0) // Significant printable characters
                {
                    DataTypeHint = "📝 High concentration of printable characters suggests text-based content.";
                }
                else if (nullByteFreq > 6.0) // Many null bytes
                {
                    DataTypeHint = "💾 High null byte frequency suggests binary data with padding or sparse structures.";
                }
                else
                {
                    DataTypeHint = "📦 Byte distribution suggests binary or mixed content.";
                }
            }
        }

        [RelayCommand]
        private async Task ExportCsv()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{Path.GetFileNameWithoutExtension(FileName)}_entropy",
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Chunk Index,Offset,Entropy");

                    if (RawEntropyData != null)
                    {
                        int chunkSizeBytes = int.Parse(ChunkSize.Split(' ')[0]);
                        for (int i = 0; i < RawEntropyData.Length; i++)
                        {
                            long offset = (long)i * chunkSizeBytes;
                            sb.AppendLine($"{i},0x{offset:X8},{RawEntropyData[i]:F6}");
                        }
                    }

                    await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
                    MessageBox.Show($"Entropy data exported to:\n{dialog.FileName}", "Export Successful", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV: {ex.Message}", "Export Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExportReport()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{Path.GetFileNameWithoutExtension(FileName)}_analysis_report",
                    DefaultExt = ".txt",
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("═══════════════════════════════════════════════════════");
                    sb.AppendLine("           ENTROPY ANALYSIS REPORT");
                    sb.AppendLine("═══════════════════════════════════════════════════════");
                    sb.AppendLine();
                    sb.AppendLine($"File: {FileName}");
                    sb.AppendLine($"Size: {FileSize}");
                    sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine();
                    sb.AppendLine("───────────────────────────────────────────────────────");
                    sb.AppendLine("ENTROPY STATISTICS");
                    sb.AppendLine("───────────────────────────────────────────────────────");
                    sb.AppendLine($"Average Entropy:      {AverageEntropy:F4}");
                    sb.AppendLine($"Minimum Entropy:      {MinEntropy:F4}");
                    sb.AppendLine($"Maximum Entropy:      {MaxEntropy:F4}");
                    sb.AppendLine($"Standard Deviation:   {StdDevEntropy:F4}");
                    sb.AppendLine($"Total Chunks:         {ChunkCount:N0}");
                    sb.AppendLine($"Chunk Size:           {ChunkSize}");
                    sb.AppendLine();
                    sb.AppendLine("───────────────────────────────────────────────────────");
                    sb.AppendLine("FILE INSIGHTS");
                    sb.AppendLine("───────────────────────────────────────────────────────");
                    sb.AppendLine(CompressionHint);
                    sb.AppendLine(EncryptionHint);
                    sb.AppendLine(DataTypeHint);
                    sb.AppendLine();
                    
                    if (ByteFrequencies != null)
                    {
                        sb.AppendLine("───────────────────────────────────────────────────────");
                        sb.AppendLine("TOP 10 MOST FREQUENT BYTES");
                        sb.AppendLine("───────────────────────────────────────────────────────");
                        
                        var topBytes = ByteFrequencies
                            .Select((freq, idx) => new { Byte = idx, Frequency = freq })
                            .OrderByDescending(x => x.Frequency)
                            .Take(10);

                        foreach (var item in topBytes)
                        {
                            string byteChar = item.Byte >= 0x20 && item.Byte <= 0x7E 
                                ? $"'{(char)item.Byte}'" 
                                : "   ";
                            sb.AppendLine($"  0x{item.Byte:X2} {byteChar}  -  {item.Frequency:F2} (relative frequency)");
                        }
                    }

                    await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
                    MessageBox.Show($"Analysis report exported to:\n{dialog.FileName}", "Export Successful", 
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
        private async Task Recalculate()
        {
            await StartAnalysisAsync();
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
    }
}
