using System;
using System.IO;
using System.IO.Compression;
using System.Windows.Input;
using MacroUI.Commands;
using Microsoft.Win32;

namespace MacroUI.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private AppSettings _settings;
        public AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public string CenterTitle
        {
            get => _settings.CenterTitle;
            set { _settings.CenterTitle = value; OnPropertyChanged(); }
        }

        public string CenterImagePath
        {
            get => _settings.CenterImagePath;
            set { _settings.CenterImagePath = value; OnPropertyChanged(); }
        }

        public string Theme
        {
            get => _settings.Theme;
            set { _settings.Theme = value; OnPropertyChanged(); }
        }

        public string AnimationEasing
        {
            get => _settings.AnimationEasing;
            set { _settings.AnimationEasing = value; OnPropertyChanged(); }
        }

        public string SelectKey
        {
            get => _settings.SelectKey;
            set { _settings.SelectKey = value; OnPropertyChanged(); }
        }

        public string BackKey
        {
            get => _settings.BackKey;
            set { _settings.BackKey = value; OnPropertyChanged(); }
        }

        public bool EnableGameMode
        {
            get => _settings.EnableGameMode;
            set { _settings.EnableGameMode = value; OnPropertyChanged(); }
        }

        public bool EnableAutoSelect
        {
            get => _settings.EnableAutoSelect;
            set { _settings.EnableAutoSelect = value; OnPropertyChanged(); }
        }

        public bool EnableAudio
        {
            get => _settings.EnableAudio;
            set { _settings.EnableAudio = value; OnPropertyChanged(); }
        }

        public string TickSoundPath
        {
            get => _settings.TickSoundPath;
            set { _settings.TickSoundPath = value; OnPropertyChanged(); }
        }

        public double TickSoundVolume
        {
            get => _settings.TickSoundVolume;
            set { _settings.TickSoundVolume = value; OnPropertyChanged(); }
        }


        public ICommand ExportBackupCommand { get; }
        public ICommand ImportBackupCommand { get; }
        public ICommand SelectCenterImageCommand { get; }
        public ICommand ClearCenterImageCommand { get; }
        public ICommand BrowseTickSoundCommand { get; }
        public ICommand ClearTickSoundCommand { get; }

        public event EventHandler ImportCompleted;

        public SettingsViewModel(AppSettings initialSettings)
        {
            _settings = initialSettings ?? new AppSettings();
            
            ExportBackupCommand = new RelayCommand(_ => ExportBackup());
            ImportBackupCommand = new RelayCommand(_ => ImportBackup());
            SelectCenterImageCommand = new RelayCommand(_ => SelectCenterImage());
            ClearCenterImageCommand = new RelayCommand(_ => ClearCenterImage());
            BrowseTickSoundCommand = new RelayCommand(_ => BrowseTickSound());
            ClearTickSoundCommand = new RelayCommand(_ => TickSoundPath = "");
        }



        private void SelectCenterImage()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Select Center Image"
            };
            if (dlg.ShowDialog() == true)
            {
                string basePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                string filePath = dlg.FileName;
                
                if (filePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    CenterImagePath = filePath.Substring(basePath.Length).TrimStart('\\', '/');
                }
                else
                {
                    CenterImagePath = filePath;
                }
            }
        }

        private void ClearCenterImage()
        {
            CenterImagePath = null;
        }

        private void BrowseTickSound()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3;*.wma;*.aac;*.m4a)|*.wav;*.mp3;*.wma;*.aac;*.m4a|All Files (*.*)|*.*",
                Title = "Select Menu Tick Sound"
            };
            if (dlg.ShowDialog() == true)
            {
                TickSoundPath = dlg.FileName;
            }
        }

        private void ExportBackup()
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip",
                Title = "Export Library Backup",
                FileName = "AulaMacroBackup.zip"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string basePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                    string tempDir = Path.Combine(Path.GetTempPath(), "AulaBackup_" + Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);

                    string macrosFile = Path.Combine(basePath, "macros.json");
                    string hotstringsFile = Path.Combine(basePath, "hotstrings.json");
                    string settingsFile = Path.Combine(basePath, "settings.json");

                    if (File.Exists(macrosFile)) File.Copy(macrosFile, Path.Combine(tempDir, "macros.json"));
                    if (File.Exists(hotstringsFile)) File.Copy(hotstringsFile, Path.Combine(tempDir, "hotstrings.json"));
                    if (File.Exists(settingsFile)) File.Copy(settingsFile, Path.Combine(tempDir, "settings.json"));

                    if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);

                    ZipFile.CreateFromDirectory(tempDir, dlg.FileName);
                    Directory.Delete(tempDir, true);

                    System.Windows.MessageBox.Show("Backup exported successfully!", "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Export failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ImportBackup()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip",
                Title = "Import Library Backup"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string basePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                    using (ZipArchive archive = ZipFile.OpenRead(dlg.FileName))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (entry.Name == "macros.json" || entry.Name == "hotstrings.json" || entry.Name == "settings.json")
                            {
                                string destPath = Path.Combine(basePath, entry.Name);
                                entry.ExtractToFile(destPath, true);
                            }
                        }
                    }
                    
                    System.Windows.MessageBox.Show("Backup imported successfully! Configuration has been reloaded.", "Import Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    ImportCompleted?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Import failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
