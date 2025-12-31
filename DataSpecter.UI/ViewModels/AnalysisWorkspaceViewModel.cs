using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSpecter.UI.Models;
using DataSpecter.Core.Interfaces;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataSpecter.UI.ViewModels
{
    public partial class AnalysisWorkspaceViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<FileModel> _files;

        [ObservableProperty]
        private FileModel? _selectedFile;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isHex = false;

        [ObservableProperty]
        private ObservableCollection<HexRow>? _hexData;

        [ObservableProperty]
        private string _textData = string.Empty;

        [ObservableProperty]
        private System.Windows.Documents.FlowDocument? _textDocument;

        [ObservableProperty]
        private long _viewOffset = 0;

        [ObservableProperty]
        private long _pageSize = 16384; // 16KB default page size for smooth rendering

        [ObservableProperty]
        private long _totalSize = 0;

        [ObservableProperty]
        private string _filePositionInfo = string.Empty;

        [ObservableProperty]
        private bool _canNavigateNext = false;

        [ObservableProperty]
        private bool _canNavigatePrevious = false;

        [ObservableProperty]
        private ObservableCollection<long> _searchResults = new();

        [ObservableProperty]
        private int _searchCount;

        [ObservableProperty]
        private bool _isSearching = false;

        [ObservableProperty]
        private string _searchStatus = string.Empty;

        [ObservableProperty]
        private System.Collections.Generic.List<DataSpecter.Core.Models.StructureItem>? _structureRoot;

        [ObservableProperty]
        private bool _hasStructure;

        [ObservableProperty]
        private long _highlightOffset = -1;

        [ObservableProperty]
        private int _highlightLength = 0;

        private readonly Action<FileModel, long, long>? _navigateToAnomalyAction;
        private readonly IEntropyService? _entropyService;
        private readonly ISuffixArrayService? _suffixArrayService;
        private readonly System.Collections.Generic.List<DataSpecter.Core.Interfaces.IStructureParser> _parsers;

        public AnalysisWorkspaceViewModel(ObservableCollection<FileModel> files, IEntropyService entropyService, ISuffixArrayService? suffixArrayService = null, Action<FileModel, long, long>? navigateToAnomalyAction = null)
        {
            _files = files;
            _entropyService = entropyService;
            _suffixArrayService = suffixArrayService;
            _navigateToAnomalyAction = navigateToAnomalyAction;
            
            _parsers = new System.Collections.Generic.List<DataSpecter.Core.Interfaces.IStructureParser>
            {
                new DataSpecter.Infrastructure.Parsers.PeParser(),
                new DataSpecter.Infrastructure.Parsers.PdfParser()
            };
        }

        [RelayCommand]
        private void CalculateEntropy()
        {
            if (SelectedFile?.DataSource == null || _entropyService == null) return;

            // Create and show the entropy analysis window
            var viewModel = new EntropyAnalysisViewModel(
                SelectedFile.DataSource, 
                _entropyService, 
                SelectedFile.Name);

            var window = new DataSpecter.UI.Views.EntropyAnalysisWindow
            {
                DataContext = viewModel,
                Owner = System.Windows.Application.Current.MainWindow
            };

            // Start analysis asynchronously after window is shown
            window.Loaded += async (s, e) =>
            {
                await viewModel.StartAnalysisAsync();
            };

            window.ShowDialog();
        }

        [RelayCommand]
        private void IsolateAnomaly()
        {
            if (SelectedFile == null || SelectedFile.LcpArray == null || SelectedFile.SuffixArray == null || SelectedFile.DataSource == null)
            {
                // Can't analyze if not indexed
                return;
            }

            // Find Longest Repeated Substring (LRS)
            // LRS is the max value in the LCP array
            // The position in LCP array (say 'i') corresponds to SuffixArray[i] and SuffixArray[i-1] sharing that prefix.
            
            int maxLcp = 0;
            int maxIndex = 0;

            // Naive scan (fast enough for 100MB arrays usually)
            var lcp = SelectedFile.LcpArray;
            for (int i = 1; i < lcp.Length; i++)
            {
                if (lcp[i] > maxLcp)
                {
                    maxLcp = lcp[i];
                    maxIndex = i;
                }
            }

            if (maxLcp > 0)
            {
                // The substring starts at SuffixArray[maxIndex] with length maxLcp
                long offset = SelectedFile.SuffixArray[maxIndex];
                long length = maxLcp;

                _navigateToAnomalyAction?.Invoke(SelectedFile, offset, length);
            }
        }

        [RelayCommand]
        private async Task Search()
        {
            SearchResults.Clear();
            SearchCount = 0;
            SearchStatus = string.Empty;
            if (string.IsNullOrEmpty(SearchQuery) || SelectedFile?.DataSource == null) 
            {
                SearchStatus = "Enter a search query";
                return;
            }

            IsSearching = true;
            try
            {
                byte[] pattern;
                try 
                {
                    if (IsHex)
                    {
                        // Convert Hex String "0A 1B FF" -> byte[]
                         string cleanHex = SearchQuery.Replace(" ", "").Replace("0x", "");
                         if (cleanHex.Length % 2 != 0) 
                         {
                             SearchStatus = "Invalid hex string (must have even number of characters)";
                             return;
                         }
                         pattern = Convert.FromHexString(cleanHex);
                    }
                    else
                    {
                        pattern = System.Text.Encoding.UTF8.GetBytes(SearchQuery);
                    }
                }
                catch (Exception ex)
                {
                    SearchStatus = $"Error parsing search query: {ex.Message}";
                    return;
                }

                if (pattern.Length == 0) 
                {
                    SearchStatus = "Pattern is empty";
                    return;
                }

                SearchStatus = "Searching...";

                System.Diagnostics.Debug.WriteLine($"[Search] Query: '{SearchQuery}', IsHex: {IsHex}");
                System.Diagnostics.Debug.WriteLine($"[Search] Pattern length: {pattern.Length}, bytes: {string.Join(" ", pattern.Select(b => b.ToString("X2")))}");
                System.Diagnostics.Debug.WriteLine($"[Search] File indexed: {SelectedFile.SuffixArray != null}, SA length: {SelectedFile.SuffixArray?.Length ?? 0}");
                System.Diagnostics.Debug.WriteLine($"[Search] File size: {SelectedFile.DataSource.Length}");

                // Use Suffix Array if available for O(M log N) speed
                if (SelectedFile.SuffixArray != null && _suffixArrayService != null)
                {
                    var offsets = await _suffixArrayService.SearchAsync(SelectedFile.DataSource, SelectedFile.SuffixArray, pattern).ConfigureAwait(false);
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SearchCount = offsets.Length;
                        
                        // If too many results, limit display to prevent UI freeze, but keep count accurate
                        if (offsets.Length > 10000)
                        {
                            var limited = new long[10000];
                            Array.Copy(offsets, limited, 10000);
                            SearchResults = new ObservableCollection<long>(limited);
                            SearchStatus = $"Found {offsets.Length:N0} matches (showing first 10,000)";
                        }
                        else
                        {
                            SearchResults = new ObservableCollection<long>(offsets);
                            SearchStatus = offsets.Length == 0 ? "No matches found" : $"Found {offsets.Length:N0} matches";
                        }
                    });
                }
                else
                {
                     // Fallback to naive search
                     SearchStatus = "Searching (file not indexed - searching first 10MB)...";
                     
                     var results = await Task.Run(() =>
                     {
                         long fileLength = SelectedFile.DataSource.Length;
                         byte[] buffer = new byte[8192];
                         long limit = Math.Min(fileLength, 10 * 1024 * 1024);
                         var foundResults = new System.Collections.Generic.List<long>();

                         for (long i = 0; i < limit; i += (buffer.Length - pattern.Length + 1))
                         {
                             int read = SelectedFile.DataSource.ReadRange(i, buffer, 0, (int)Math.Min(buffer.Length, limit - i));
                             if (read < pattern.Length) break;

                             for (int j = 0; j <= read - pattern.Length; j++)
                             {
                                 bool match = true;
                                 for (int k = 0; k < pattern.Length; k++)
                                 {
                                     if (buffer[j + k] != pattern[k])
                                     {
                                         match = false;
                                         break;
                                     }
                                 }
                                 if (match)
                                 {
                                     foundResults.Add(i + j);
                                     if (foundResults.Count >= 10000) break;
                                 }
                             }
                             if (foundResults.Count >= 10000) break;
                         }
                         return foundResults;
                     }).ConfigureAwait(false);
                     
                     await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                     {
                         SearchCount = results.Count;
                         SearchResults = new ObservableCollection<long>(results);
                         SearchStatus = results.Count == 0 ? "No matches found" : $"Found {results.Count:N0} matches";
                     });
                }
            }
            finally
            {
                IsSearching = false;
            }
        }
        
        [RelayCommand]
        private void NavigateNext()
        {
            if (CanNavigateNext)
            {
                ViewOffset += PageSize;
                LoadViewData();
            }
        }

        [RelayCommand]
        private void NavigatePrevious()
        {
            if (CanNavigatePrevious)
            {
                ViewOffset = Math.Max(0, ViewOffset - PageSize);
                LoadViewData();
            }
        }

        [RelayCommand]
        private void NavigateToStart()
        {
            ViewOffset = 0;
            LoadViewData();
        }

        [RelayCommand]
        private void NavigateToEnd()
        {
            if (SelectedFile?.DataSource != null)
            {
                ViewOffset = Math.Max(0, SelectedFile.DataSource.Length - PageSize);
                LoadViewData();
            }
        }

        [RelayCommand]
        private void NavigateToOffset(long offset)
        {
            if (SelectedFile?.DataSource == null) return;

            // Store the pattern length from the last search
            byte[] pattern;
            try
            {
                if (IsHex)
                {
                    string cleanHex = SearchQuery.Replace(" ", "").Replace("0x", "");
                    if (cleanHex.Length % 2 != 0) return;
                    pattern = Convert.FromHexString(cleanHex);
                }
                else
                {
                    pattern = System.Text.Encoding.UTF8.GetBytes(SearchQuery);
                }
            }
            catch
            {
                pattern = Array.Empty<byte>();
            }

            // Set highlight range
            HighlightOffset = offset;
            HighlightLength = pattern.Length;

            // Calculate which page contains this offset (center it if possible)
            long halfPage = PageSize / 2;
            ViewOffset = Math.Max(0, offset - halfPage);
            
            // Make sure we don't go past the end
            if (ViewOffset + PageSize > SelectedFile.DataSource.Length)
            {
                ViewOffset = Math.Max(0, SelectedFile.DataSource.Length - PageSize);
            }

            LoadViewData();
        }

        [RelayCommand]
        private void ExportResults()
        {
            if (SearchResults.Count == 0 || SelectedFile?.DataSource == null) return;
            
            // Get the search pattern for context extraction
            byte[] pattern;
            try
            {
                if (IsHex)
                {
                    string cleanHex = SearchQuery.Replace(" ", "").Replace("0x", "");
                    if (cleanHex.Length % 2 != 0) return;
                    pattern = Convert.FromHexString(cleanHex);
                }
                else
                {
                    pattern = System.Text.Encoding.UTF8.GetBytes(SearchQuery);
                }
            }
            catch
            {
                pattern = Array.Empty<byte>();
            }

            // Build CSV with context
            var csv = "Offset (Decimal),Offset (Hex),Match Text,Context (350 chars max)\n";
            
            foreach(var offset in SearchResults)
            {
                string matchText = "";
                string context = "";
                
                try
                {
                    // Read the matched pattern
                    byte[] matchBuffer = new byte[pattern.Length];
                    SelectedFile.DataSource.ReadRange(offset, matchBuffer, 0, pattern.Length);
                    matchText = System.Text.Encoding.UTF8.GetString(matchBuffer).Replace("\"", "\"\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                    
                    // Extract context (350 chars centered around the match)
                    int contextLength = 350;
                    int halfContext = contextLength / 2;
                    
                    // Calculate start position (try to center the match)
                    long contextStart = Math.Max(0, offset - halfContext);
                    
                    // Read buffer for context
                    int bufferSize = Math.Min(contextLength * 2, (int)(SelectedFile.DataSource.Length - contextStart));
                    byte[] contextBuffer = new byte[bufferSize];
                    int bytesRead = SelectedFile.DataSource.ReadRange(contextStart, contextBuffer, 0, bufferSize);
                    
                    // Convert to text
                    string fullContext = System.Text.Encoding.UTF8.GetString(contextBuffer, 0, bytesRead);
                    
                    // Find line boundaries around the match position
                    int relativeMatchPos = (int)(offset - contextStart);
                    
                    // Find start of line (search backwards for newline)
                    int lineStart = relativeMatchPos;
                    while (lineStart > 0 && fullContext[lineStart - 1] != '\n' && fullContext[lineStart - 1] != '\r')
                    {
                        lineStart--;
                    }
                    
                    // Find end of line (search forwards for newline)
                    int lineEnd = relativeMatchPos + pattern.Length;
                    while (lineEnd < fullContext.Length && fullContext[lineEnd] != '\n' && fullContext[lineEnd] != '\r')
                    {
                        lineEnd++;
                    }
                    
                    // Extract the line
                    string line = fullContext.Substring(lineStart, lineEnd - lineStart);
                    
                    // If line is too long, truncate to 350 chars centered around match
                    if (line.Length > contextLength)
                    {
                        int matchPosInLine = relativeMatchPos - lineStart;
                        int extractStart = Math.Max(0, matchPosInLine - halfContext);
                        int extractLength = Math.Min(contextLength, line.Length - extractStart);
                        
                        line = line.Substring(extractStart, extractLength);
                        
                        // Add ellipsis if truncated
                        if (extractStart > 0) line = "..." + line;
                        if (extractStart + extractLength < fullContext.Substring(lineStart, lineEnd - lineStart).Length) line = line + "...";
                    }
                    
                    // Clean up for CSV
                    context = line.Replace("\"", "\"\"").Replace("\t", "\\t");
                }
                catch
                {
                    matchText = "[Error reading]";
                    context = "[Error extracting context]";
                }
                
                csv += $"{offset},0x{offset:X},\"{matchText}\",\"{context}\"\n";
            }
            
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DataSpecter_Export_{DateTime.Now.Ticks}.csv");
            System.IO.File.WriteAllText(path, csv);
            
            // Open the CSV file
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        
        partial void OnSelectedFileChanged(FileModel? value)
        {
            LoadViewData();
        }

        partial void OnViewOffsetChanged(long value)
        {
            LoadViewData();
        }

        partial void OnPageSizeChanged(long value)
        {
            LoadViewData();
        }

        private async void LoadViewData()
        {
            if (SelectedFile?.DataSource != null)
            {
                TotalSize = SelectedFile.DataSource.Length;
                
                // Clamp page size to reasonable bounds
                int actualPageSize = (int)Math.Min(PageSize, TotalSize - ViewOffset);
                if (actualPageSize <= 0) actualPageSize = 1024;
                
                byte[] buffer = new byte[actualPageSize];
                int read = SelectedFile.DataSource.ReadRange(ViewOffset, buffer, 0, actualPageSize);
                
                // If read < actualPageSize, resize buffer for display purposes
                if (read < actualPageSize)
                {
                    Array.Resize(ref buffer, read);
                }
                
                // Create virtualized HexRow collection (16 bytes per row)
                var hexRows = new ObservableCollection<HexRow>();
                int bytesPerRow = 16;
                for (int i = 0; i < read; i += bytesPerRow)
                {
                    var rowItems = new System.Collections.Generic.List<ByteItem>();
                    int rowLength = Math.Min(bytesPerRow, read - i);
                    
                    for (int j = 0; j < rowLength; j++)
                    {
                        long currentOffset = ViewOffset + i + j;
                        bool isHighlighted = HighlightOffset >= 0 && 
                                            currentOffset >= HighlightOffset && 
                                            currentOffset < HighlightOffset + HighlightLength;
                        rowItems.Add(new ByteItem(buffer[i + j], currentOffset, isHighlighted));
                    }
                    
                    hexRows.Add(new HexRow(ViewOffset + i, rowItems));
                }
                HexData = hexRows;
                
                // Create text document with highlighting support
                TextData = System.Text.Encoding.UTF8.GetString(buffer);
                
                // Create FlowDocument for highlighted text view
                var flowDoc = new System.Windows.Documents.FlowDocument();
                var paragraph = new System.Windows.Documents.Paragraph();
                
                if (HighlightOffset >= 0 && HighlightLength > 0)
                {
                    // Calculate relative positions within the current view
                    long relativeHighlightStart = HighlightOffset - ViewOffset;
                    
                    if (relativeHighlightStart >= 0 && relativeHighlightStart < read)
                    {
                        // Add text before highlight
                        if (relativeHighlightStart > 0)
                        {
                            string beforeText = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)relativeHighlightStart);
                            paragraph.Inlines.Add(new System.Windows.Documents.Run(beforeText));
                        }
                        
                        // Add highlighted text
                        int highlightStartInBuffer = (int)relativeHighlightStart;
                        int highlightEndInBuffer = Math.Min((int)(relativeHighlightStart + HighlightLength), read);
                        int highlightLengthInBuffer = highlightEndInBuffer - highlightStartInBuffer;
                        
                        if (highlightLengthInBuffer > 0)
                        {
                            string highlightedText = System.Text.Encoding.UTF8.GetString(buffer, highlightStartInBuffer, highlightLengthInBuffer);
                            var highlightRun = new System.Windows.Documents.Run(highlightedText)
                            {
                                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 0)), // Yellow
                                Foreground = System.Windows.Media.Brushes.Black,
                                FontWeight = System.Windows.FontWeights.Bold
                            };
                            paragraph.Inlines.Add(highlightRun);
                        }
                        
                        // Add text after highlight
                        if (highlightEndInBuffer < read)
                        {
                            string afterText = System.Text.Encoding.UTF8.GetString(buffer, highlightEndInBuffer, read - highlightEndInBuffer);
                            paragraph.Inlines.Add(new System.Windows.Documents.Run(afterText));
                        }
                    }
                    else
                    {
                        // Highlight not in current view, just add all text
                        paragraph.Inlines.Add(new System.Windows.Documents.Run(TextData));
                    }
                }
                else
                {
                    // No highlight, add all text
                    paragraph.Inlines.Add(new System.Windows.Documents.Run(TextData));
                }
                
                flowDoc.Blocks.Add(paragraph);
                TextDocument = flowDoc;
                
                // Update position info
                long endOffset = Math.Min(ViewOffset + read, TotalSize);
                FilePositionInfo = $"Showing bytes {ViewOffset:N0} - {endOffset:N0} of {TotalSize:N0} ({(endOffset * 100.0 / TotalSize):F1}%)";
                
                // Update navigation state
                CanNavigatePrevious = ViewOffset > 0;
                CanNavigateNext = ViewOffset + read < TotalSize;

                // Structure Parsing
                StructureRoot = null;
                HasStructure = false;
                
                // Read header for detection (10 bytes)
                byte[] header = new byte[10];
                SelectedFile.DataSource.ReadRange(0, header, 0, 10);
                
                foreach(var parser in _parsers)
                {
                    if (parser.CanParse(SelectedFile.Name, header))
                    {
                        StructureRoot = await parser.ParseAsync(SelectedFile.DataSource);
                        HasStructure = true;
                        break;
                    }
                }
            }
            else
            {
                HexData = null;
                TextData = string.Empty;
                StructureRoot = null;
                HasStructure = false;
            }
        }
    }
}
