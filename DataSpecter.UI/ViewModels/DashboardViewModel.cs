using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSpecter.UI.Models;
using DataSpecter.Core.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace DataSpecter.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<FileModel> _files;

        [ObservableProperty]
        private int _totalEvidence;

        [ObservableProperty]
        private int _fullyIndexed;
        
        [ObservableProperty]
        private string _indexingStrategy = "Optimized";

                private readonly Action _browseFilesAction;
                private readonly Action<string[]> _filesDroppedAction;
                private readonly ISuffixArrayService _suffixArrayService;
                private readonly IFileService _fileService;
        
                public DashboardViewModel(ObservableCollection<FileModel> files, ISuffixArrayService suffixArrayService, IFileService fileService, Action browseFilesAction, Action<string[]> filesDroppedAction)
                {
                    _files = files;
                    _suffixArrayService = suffixArrayService;
                    _fileService = fileService;
                    _browseFilesAction = browseFilesAction;
                    _filesDroppedAction = filesDroppedAction;
                    _files.CollectionChanged += (s, e) => UpdateStats();
                    UpdateStats();
                }
        
                private void UpdateStats()
                {
                    TotalEvidence = Files.Count;
                    FullyIndexed = Files.Count(f => f.Status == "indexed");
                }
        
                [RelayCommand]
                private void UploadFiles() 
                {
                     _browseFilesAction?.Invoke();
                }
        
                [RelayCommand]
                private void DropFiles(string[]? files)
                {
                    if (files != null && files.Length > 0)
                    {
                        _filesDroppedAction?.Invoke(files);
                    }
                }
                
                [RelayCommand]
                private async Task IndexFile(FileModel? file)
                {
                    if(file == null || file.Status == "indexed" || file.DataSource == null) return;
                    
                    try
                    {
                        file.Status = "indexing";
                        file.IndexingProgress = 0;
                        file.IndexingStage = "Initializing";
                        file.IndexingDetails = "";
                        
                        // 1. Try Load Persistence
                        var existing = await _fileService.LoadIndexAsync(file.Path);
                        
                        if (existing != null)
                        {
                             file.SuffixArray = existing.Value.sa;
                             file.LcpArray = existing.Value.lcp;
                             file.SuffixArrayCount = existing.Value.sa.Length;
                             file.LcpArrayCount = existing.Value.lcp.Length;
                             // Times are unknown if loaded, set to zero or small val
                             file.SuffixArrayTime = TimeSpan.Zero;
                             file.LcpArrayTime = TimeSpan.Zero;
                        }
                        else
                        {
                            // 2. Generate with progress reporting
                            var progress = new Progress<(string stage, int current, int total, double percentage)>(report =>
                            {
                                file.IndexingStage = report.stage;
                                file.IndexingProgress = (int)report.percentage;
                                file.IndexingDetails = $"{report.current:N0} / {report.total:N0} ({report.percentage:F1}%)";
                            });

                            var result = await _suffixArrayService.GenerateAsync(file.DataSource, progress);
                            
                            file.SuffixArrayCount = result.saCount;
                            file.SuffixArrayTime = result.saTime;
                            file.SuffixArray = result.suffixArray;
                            
                            file.LcpArrayCount = result.lcpCount;
                            file.LcpArrayTime = result.lcpTime;
                            file.LcpArray = result.lcpArray;
                            
                            // 3. Save Persistence
                            await _fileService.SaveIndexAsync(file.Path, file.SuffixArray, file.LcpArray);
                        }
        
                        file.Status = "indexed";
                        file.IndexingProgress = 100;
                        file.IndexingStage = "Complete";
                        UpdateStats();
                    }
                    catch (Exception ex)
                    {
                        file.Status = "error";
                        file.IndexingStage = "Error";
                        file.IndexingDetails = ex.Message;
                        // In a real app we'd log this or show a message
                        System.Diagnostics.Debug.WriteLine($"Indexing failed: {ex.Message}");
                    }
                }
    }
}
