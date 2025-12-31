using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Windows;
using DataSpecter.Core.Interfaces;
using DataSpecter.UI.Models;
using System.IO;
using System;
using System.Collections.ObjectModel;

namespace DataSpecter.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IFileService _fileService;
        private readonly ISuffixArrayService _suffixArrayService;
        private readonly IEntropyService _entropyService;
        private readonly IFuzzyHashService _fuzzyHashService;
        private readonly ObservableCollection<FileModel> _files = new();

        [ObservableProperty]
        private string _windowTitle = "Data Specter";

        [ObservableProperty]
        private string _statusMessage = "Ready";
        
        // Navigation
        [ObservableProperty]
        private object _currentViewModel;

        public DashboardViewModel DashboardVM { get; }
        public AnalysisWorkspaceViewModel AnalysisVM { get; }
        public FileComparisonViewModel ComparisonVM { get; }
        public FileManagementViewModel ManagementVM { get; }
        public AnomalyDetailsViewModel AnomalyVM { get; } = new();

        public MainViewModel(
            IFileService fileService, 
            ISuffixArrayService suffixArrayService, 
            IEntropyService entropyService, 
            ILcsService lcsService, 
            IFuzzyHashService fuzzyHashService)
        {
            _fileService = fileService;
            _suffixArrayService = suffixArrayService;
            _entropyService = entropyService;
            _fuzzyHashService = fuzzyHashService;

            // Initialize child VMs with shared state and delegates
            DashboardVM = new DashboardViewModel(_files, _suffixArrayService, _fileService, OpenFile, LoadFiles);
            AnalysisVM = new AnalysisWorkspaceViewModel(_files, _entropyService, _suffixArrayService, NavigateToAnomaly);
            ComparisonVM = new FileComparisonViewModel(_files, lcsService, _fuzzyHashService);
            ManagementVM = new FileManagementViewModel(_files);

            CurrentViewModel = DashboardVM;
        }

        [RelayCommand]
        private void NavigateToDashboard() => CurrentViewModel = DashboardVM;

        [RelayCommand]
        private void NavigateToAnalysis() => CurrentViewModel = AnalysisVM;

        [RelayCommand]
        private void NavigateToComparison() => CurrentViewModel = ComparisonVM;
        
        [RelayCommand]
        private void NavigateToFiles() => CurrentViewModel = ManagementVM;

        public void NavigateToAnomaly(FileModel file, long offset, long length)
        {
            AnomalyVM.Initialize(file, offset, length);
            CurrentViewModel = AnomalyVM;
        }

        private void LoadFiles(string[] paths)
        {
             StatusMessage = "Loading files...";
             foreach (var path in paths)
             {
                try
                {
                    if (!File.Exists(path)) continue;

                    var dataSource = _fileService.OpenFile(path);
                    
                    var fileModel = new FileModel
                    {
                        Id = _files.Count + 1,
                        Name = Path.GetFileName(path),
                        Path = path,
                        Size = dataSource.Length,
                        Status = "Ready",
                        DataSource = dataSource
                    };

                    _files.Add(fileModel);
                    StatusMessage = $"Loaded: {fileModel.Name}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading {Path.GetFileName(path)}";
                    MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
             }
        }

        [RelayCommand]
        private void OpenFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Forensic Target",
                Filter = "Forensic Targets (*.txt;*.log;*.dll;*.pdf;*.bin;*.exe)|*.txt;*.log;*.dll;*.pdf;*.bin;*.exe|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = true 
            };

            if (dialog.ShowDialog() == true)
            {
                LoadFiles(dialog.FileNames);
            }
        }

        [RelayCommand]
        private void Exit()
        {
            // Cleanup
            foreach(var file in _files)
            {
                file.DataSource?.Dispose();
            }
            Application.Current.Shutdown();
        }
    }
}