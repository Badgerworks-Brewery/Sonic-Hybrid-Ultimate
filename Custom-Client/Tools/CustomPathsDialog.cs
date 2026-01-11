using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using SonicHybridUltimate.Core;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Tools
{
    /// <summary>
    /// Dialog for managing custom game file search paths
    /// </summary>
    public class CustomPathsDialog : Form
    {
        private readonly UserSettings _userSettings;
        private readonly ILogger _logger;
        
        private ListBox _pathsList = null!;
        private Button _addButton = null!;
        private Button _removeButton = null!;
        private Button _browseButton = null!;
        private Button _scanNowButton = null!;
        private Button _closeButton = null!;
        private Label _infoLabel = null!;
        private CheckBox _autoImportCheckBox = null!;

        public CustomPathsDialog(UserSettings userSettings, ILogger logger)
        {
            _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            InitializeComponents();
            LoadPaths();
        }

        private void InitializeComponents()
        {
            Text = "Manage Game File Locations";
            Size = new Size(600, 450);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                Padding = new Padding(10)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));  // Info
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f)); // Buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f)); // Checkbox
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f)); // Close

            // Info label
            _infoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Add custom folders where you store your Sonic game files (.rsdk and .bin files).\n" +
                       "The application will automatically scan these locations for games.",
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft
            };
            mainLayout.Controls.Add(_infoLabel, 0, 0);

            // Paths list
            _pathsList = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.One,
                HorizontalScrollbar = true
            };
            _pathsList.SelectedIndexChanged += PathsList_SelectedIndexChanged;
            mainLayout.Controls.Add(_pathsList, 0, 1);

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 5, 0, 0)
            };

            _browseButton = new Button
            {
                Text = "Browse and Add Folder...",
                AutoSize = true,
                Height = 30
            };
            _browseButton.Click += BrowseButton_Click;

            _addButton = new Button
            {
                Text = "Add Current Folder",
                AutoSize = true,
                Height = 30
            };
            _addButton.Click += AddButton_Click;

            _removeButton = new Button
            {
                Text = "Remove Selected",
                AutoSize = true,
                Height = 30,
                Enabled = false
            };
            _removeButton.Click += RemoveButton_Click;

            _scanNowButton = new Button
            {
                Text = "Scan Now",
                AutoSize = true,
                Height = 30
            };
            _scanNowButton.Click += ScanNowButton_Click;

            buttonPanel.Controls.AddRange(new Control[] 
            { 
                _browseButton, 
                _addButton, 
                _removeButton,
                _scanNowButton 
            });
            mainLayout.Controls.Add(buttonPanel, 0, 2);

            // Auto-import checkbox
            _autoImportCheckBox = new CheckBox
            {
                Dock = DockStyle.Fill,
                Text = "Automatically import detected games to GameData folder",
                Checked = _userSettings.Current.AutoImportDetectedGames
            };
            _autoImportCheckBox.CheckedChanged += AutoImportCheckBox_CheckedChanged;
            mainLayout.Controls.Add(_autoImportCheckBox, 0, 3);

            // Close button
            _closeButton = new Button
            {
                Text = "Close",
                Dock = DockStyle.Right,
                Width = 100,
                Height = 30
            };
            _closeButton.Click += (s, e) => Close();
            mainLayout.Controls.Add(_closeButton, 0, 4);

            Controls.Add(mainLayout);
        }

        private void LoadPaths()
        {
            _pathsList.Items.Clear();
            
            var paths = _userSettings.GetCustomPaths();
            foreach (var path in paths)
            {
                _pathsList.Items.Add(path);
            }

            UpdateButtonStates();
        }

        private void PathsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            _removeButton.Enabled = _pathsList.SelectedIndex >= 0;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder containing Sonic game files (.rsdk or .bin)",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                AddPath(dialog.SelectedPath);
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            var currentDir = Directory.GetCurrentDirectory();
            AddPath(currentDir);
        }

        private void AddPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(
                    "Please provide a valid folder path.",
                    "Invalid Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(path))
            {
                MessageBox.Show(
                    $"The folder does not exist:\n{path}",
                    "Folder Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_userSettings.AddCustomPath(path))
            {
                _logger.LogInformation("Added custom search path: {Path}", path);
                LoadPaths();
                
                MessageBox.Show(
                    $"Added custom search path:\n{path}\n\n" +
                    "The application will now scan this folder for game files.",
                    "Path Added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    "This path is already in the list.",
                    "Path Already Added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            if (_pathsList.SelectedIndex < 0)
                return;

            var path = _pathsList.SelectedItem?.ToString();
            if (path == null)
                return;

            var result = MessageBox.Show(
                $"Remove this search path?\n\n{path}",
                "Confirm Removal",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (_userSettings.RemoveCustomPath(path))
                {
                    _logger.LogInformation("Removed custom search path: {Path}", path);
                    LoadPaths();
                }
            }
        }

        private void ScanNowButton_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Starting manual game file scan...");
                
                var detector = new GameFileAutoDetector(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<GameFileAutoDetector>.Instance,
                    _userSettings
                );
                
                var detectedGames = detector.DetectAllGames();
                
                if (detectedGames.Any())
                {
                    var message = $"Found {detectedGames.Count} game file(s):\n\n";
                    foreach (var game in detectedGames)
                    {
                        message += $"• {game.DisplayName}\n  {game.FilePath}\n\n";
                    }
                    
                    if (_userSettings.Current.AutoImportDetectedGames)
                    {
                        message += "These files will be automatically imported to the GameData folder.";
                    }
                    else
                    {
                        message += "Enable auto-import to copy these files to the GameData folder.";
                    }
                    
                    MessageBox.Show(
                        message,
                        "Game Files Detected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                        
                    // If auto-import is enabled, import them now
                    if (_userSettings.Current.AutoImportDetectedGames)
                    {
                        var gameDataDir = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "GameData"
                        );
                        
                        int importedCount = 0;
                        foreach (var game in detectedGames)
                        {
                            if (detector.ImportDetectedGame(game, gameDataDir))
                            {
                                importedCount++;
                            }
                        }
                        
                        MessageBox.Show(
                            $"Successfully imported {importedCount} of {detectedGames.Count} game files to:\n{gameDataDir}",
                            "Import Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "No game files were found in the configured search paths.\n\n" +
                        "Make sure you have added folders that contain:\n" +
                        "• sonic1.rsdk, sonic2.rsdk, soniccd.rsdk\n" +
                        "• sonic3.bin (Sonic 3 & Knuckles ROM)\n\n" +
                        "Use 'Browse and Add Folder' to add more locations.",
                        "No Games Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game file scan");
                MessageBox.Show(
                    $"Error scanning for game files:\n{ex.Message}",
                    "Scan Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void AutoImportCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var settings = _userSettings.Current;
            settings.AutoImportDetectedGames = _autoImportCheckBox.Checked;
            _userSettings.Save(settings);
            
            _logger.LogInformation("Auto-import setting changed to: {AutoImport}", _autoImportCheckBox.Checked);
        }
    }
}
