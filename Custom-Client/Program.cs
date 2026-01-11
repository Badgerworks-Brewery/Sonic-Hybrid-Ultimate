using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SonicHybridUltimate.Engines;
using SonicHybridUltimate.Tools;
using SonicHybridUltimate.Core;

namespace SonicHybridUltimate
{
    /// <summary>
    /// Common search paths for game data files
    /// </summary>
    internal static class GamePaths
    {
        // Get the directory where the executable is located
        private static string ExeDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        
        // Primary location: GameData folder next to executable
        private static string GameDataDir => Path.Combine(ExeDirectory, "GameData");
        
        // Priority 1: Unified hybrid data (all games in one)
        public static readonly string[] HybridSearchPaths = new[]
        {
            Path.Combine("Hybrid-RSDK-Main", "sonic-hybrid", "Data.rsdk"),
            Path.Combine("..", "Hybrid-RSDK-Main", "sonic-hybrid", "Data.rsdk"),
            Path.Combine("sonic-hybrid", "Data.rsdk"),
            Path.Combine("..", "sonic-hybrid", "Data.rsdk"),
        };
        
        // Priority 2: Individual game files (fallback)
        public static readonly string[] Sonic1SearchPaths = new[]
        {
            // Primary: GameData folder (for packaged distribution)
            Path.Combine(GameDataDir, "sonic1.rsdk"),
            // Fallback: Development paths
            Path.Combine("Hybrid-RSDK-Main", "Data", "sonic1.rsdk"),
            Path.Combine("Hybrid-RSDK-Main", "rsdk-source-data", "sonic1.rsdk"),
            Path.Combine("rsdk-source-data", "sonic1.rsdk"),
            Path.Combine("..", "rsdk-source-data", "sonic1.rsdk"),
            Path.Combine("GameData", "sonic1.rsdk"),
            "sonic1.rsdk",
            "Data.rsdk"
        };
        
        public static readonly string[] SonicCDSearchPaths = new[]
        {
            // Primary: GameData folder (for packaged distribution)
            Path.Combine(GameDataDir, "soniccd.rsdk"),
            // Fallback: Development paths
            Path.Combine("Hybrid-RSDK-Main", "Data", "soniccd.rsdk"),
            Path.Combine("Hybrid-RSDK-Main", "rsdk-source-data", "soniccd.rsdk"),
            Path.Combine("rsdk-source-data", "soniccd.rsdk"),
            Path.Combine("..", "rsdk-source-data", "soniccd.rsdk"),
            Path.Combine("GameData", "soniccd.rsdk"),
            "soniccd.rsdk",
            "Data.rsdk"
        };
        
        public static readonly string[] Sonic2SearchPaths = new[]
        {
            // Primary: GameData folder (for packaged distribution)
            Path.Combine(GameDataDir, "sonic2.rsdk"),
            // Fallback: Development paths
            Path.Combine("Hybrid-RSDK-Main", "Data", "sonic2.rsdk"),
            Path.Combine("Hybrid-RSDK-Main", "rsdk-source-data", "sonic2.rsdk"),
            Path.Combine("rsdk-source-data", "sonic2.rsdk"),
            Path.Combine("..", "rsdk-source-data", "sonic2.rsdk"),
            Path.Combine("GameData", "sonic2.rsdk"),
            "sonic2.rsdk",
            "Data.rsdk"
        };
        
        public static readonly string[] Sonic3SearchPaths = new[]
        {
            // Primary: GameData folder (for packaged distribution)
            Path.Combine(GameDataDir, "sonic3.bin"),
            // Fallback: Development paths
            Path.Combine("Sonic 3 AIR Main", "sonic3.bin"),
            Path.Combine("Hybrid-RSDK-Main", "rsdk-source-data", "sonic3.bin"),
            Path.Combine("rsdk-source-data", "sonic3.bin"),
            Path.Combine("..", "rsdk-source-data", "sonic3.bin"),
            Path.Combine("GameData", "sonic3.bin"),
            "sonic3.bin"
        };
    }
    
    public partial class MainForm : Form
    {
        private readonly ILogger<MainForm> _logger;
        private readonly IServiceProvider _services;
        private readonly RSDKEngine _rsdkEngine;
        private readonly OxygenEngine _oxygenEngine;
        private readonly RSDKAnalyzer _rsdkAnalyzer;
        private readonly UILoggerProvider _uiLoggerProvider;
        private readonly UserSettings _userSettings;
        private readonly GameFileAutoDetector _autoDetector;

        private RichTextBox _logBox = null!;
        private Label _statusLabel = null!;
        private Button _loadSonic1Button = null!;
        private Button _loadSonicCDButton = null!;
        private Button _loadSonic2Button = null!;
        private Button _loadSonic3Button = null!;
        private System.Windows.Forms.Timer _updateTimer = null!;

        private string _currentGame = string.Empty;
        private bool _isTransitioning;
        private bool _hasEncounteredFatalError = false;

        public MainForm(IServiceProvider services, UILoggerProvider uiLoggerProvider)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _uiLoggerProvider = uiLoggerProvider ?? throw new ArgumentNullException(nameof(uiLoggerProvider));
            _logger = _services.GetRequiredService<ILogger<MainForm>>();
            _rsdkEngine = _services.GetRequiredService<RSDKEngine>();
            _oxygenEngine = _services.GetRequiredService<OxygenEngine>();
            _rsdkAnalyzer = _services.GetRequiredService<RSDKAnalyzer>();
            _userSettings = _services.GetRequiredService<UserSettings>();
            _autoDetector = _services.GetRequiredService<GameFileAutoDetector>();

            InitializeComponents();
            
            // Configure UI logging now that the log box is created
            _uiLoggerProvider.SetLogAction(Log);
            
            InitializeTimer();

            // Auto-detect and import game files
            AutoDetectAndImportGames();

            // Check for available games and native libraries
            CheckGameAvailability();

            _logger.LogInformation("MainForm initialized");
        }

        private void AutoDetectAndImportGames()
        {
            try
            {
                _logger.LogInformation("╔══════════════════════════════════════════╗");
                _logger.LogInformation("║     AUTO-DETECTING GAME FILES...         ║");
                _logger.LogInformation("╚══════════════════════════════════════════╝");
                _logger.LogInformation("");
                
                var detectedGames = _autoDetector.DetectAllGames();
                
                if (detectedGames.Any())
                {
                    _logger.LogInformation("✓ Found {Count} game file(s):", detectedGames.Count);
                    foreach (var game in detectedGames)
                    {
                        _logger.LogInformation("  • {DisplayName}", game.DisplayName);
                        _logger.LogInformation("    {FilePath}", game.FilePath);
                    }
                    _logger.LogInformation("");
                    
                    // Auto-import if enabled
                    if (_userSettings.Current.AutoImportDetectedGames)
                    {
                        _logger.LogInformation("Auto-importing detected games to GameData folder...");
                        var gameDataDir = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "GameData"
                        );
                        
                        int importedCount = 0;
                        foreach (var game in detectedGames)
                        {
                            if (_autoDetector.ImportDetectedGame(game, gameDataDir))
                            {
                                importedCount++;
                                _logger.LogInformation("  ✓ Imported {GameType}", game.GameType);
                            }
                        }
                        
                        _logger.LogInformation("✓ Imported {Count} of {Total} game files", importedCount, detectedGames.Count);
                    }
                }
                else
                {
                    _logger.LogInformation("No game files found in configured search paths.");
                    _logger.LogInformation("Use Tools > Manage Game Locations to add custom folders.");
                }
                
                _logger.LogInformation("");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-detection");
            }
        }

        private void CheckGameAvailability()
        {
            _logger.LogInformation("=== Sonic Hybrid Ultimate ===");
            _logger.LogInformation("Checking available games and engines...");
            _logger.LogInformation("");
            
            // Check native libraries
            bool hasRsdkLib = NativeLibraryResolver.IsLibraryAvailable("RSDKv4");
            bool hasOxygenLib = NativeLibraryResolver.IsLibraryAvailable("OxygenEngine");
            
            _logger.LogInformation("Engine Status:");
            _logger.LogInformation("  RSDKv4 Library: {Status}", hasRsdkLib ? "✓ Available" : "✗ Not Found");
            _logger.LogInformation("  OxygenEngine Library: {Status}", hasOxygenLib ? "✓ Available" : "✗ Not Found");
            _logger.LogInformation("");
            
            // Check for game files using shared paths
            var sonic1Path = FindGameFile("sonic1.rsdk", GamePaths.Sonic1SearchPaths);
            var sonicCDPath = FindGameFile("soniccd.rsdk", GamePaths.SonicCDSearchPaths);
            var sonic2Path = FindGameFile("sonic2.rsdk", GamePaths.Sonic2SearchPaths);
            var sonic3Path = FindGameFile("sonic3.bin", GamePaths.Sonic3SearchPaths);
            
            _logger.LogInformation("Game Data Status:");
            _logger.LogInformation("  Sonic 1: {Status}", sonic1Path != null ? $"✓ Found at {sonic1Path}" : "✗ Not Found (select when prompted)");
            _logger.LogInformation("  Sonic CD: {Status}", sonicCDPath != null ? $"✓ Found at {sonicCDPath}" : "✗ Not Found (select when prompted)");
            _logger.LogInformation("  Sonic 2: {Status}", sonic2Path != null ? $"✓ Found at {sonic2Path}" : "✗ Not Found (select when prompted)");
            _logger.LogInformation("  Sonic 3 ROM: {Status}", sonic3Path != null ? $"✓ Found at {sonic3Path}" : "✗ Not Found (select when prompted)");
            _logger.LogInformation("");
            
            if (!hasRsdkLib)
            {
                _logger.LogWarning("Native RSDKv4 library not found. Run build_native_libs.sh to build it.");
            }
            
            if (!hasOxygenLib)
            {
                _logger.LogWarning("Native OxygenEngine library not found. Run build_native_libs.sh to build it.");
            }
            
            _logger.LogInformation("Click a game button above to start playing!");
            _logger.LogInformation("=====================================");
        }

        private void InitializeComponents()
        {
            Text = "Sonic Hybrid Ultimate";
            Size = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;

            // Create menu bar
            var menuStrip = new MenuStrip();
            
            // File menu
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => Close());
            
            // Tools menu
            var toolsMenu = new ToolStripMenuItem("&Tools");
            toolsMenu.DropDownItems.Add("&Manage Game Locations...", null, ManageLocations_Click);
            toolsMenu.DropDownItems.Add("&Scan for Games Now", null, ScanNow_Click);
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            toolsMenu.DropDownItems.Add("&Settings...", null, Settings_Click);
            
            // Help menu
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add("&About", null, About_Click);
            
            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(toolsMenu);
            menuStrip.Items.Add(helpMenu);
            
            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;

            // Create main layout
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));  // Buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Log
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));  // Status

            // Create button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            _loadSonic1Button = new Button
            {
                Text = "Load Sonic 1",
                AutoSize = true
            };
            _loadSonic1Button.Click += LoadSonic1_Click;

            _loadSonicCDButton = new Button
            {
                Text = "Load Sonic CD",
                AutoSize = true
            };
            _loadSonicCDButton.Click += LoadSonicCD_Click;

            _loadSonic2Button = new Button
            {
                Text = "Load Sonic 2",
                AutoSize = true
            };
            _loadSonic2Button.Click += LoadSonic2_Click;

            _loadSonic3Button = new Button
            {
                Text = "Load Sonic 3 & Knuckles",
                AutoSize = true,
                Enabled = true  // Allow users to manually load Sonic 3 AIR
            };
            _loadSonic3Button.Click += LoadSonic3_Click;

            buttonPanel.Controls.AddRange(new Control[] { 
                _loadSonic1Button, 
                _loadSonicCDButton, 
                _loadSonic2Button, 
                _loadSonic3Button 
            });

            // Create log box
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10f)
            };

            // Create status label
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ready",
                TextAlign = ContentAlignment.MiddleLeft,
                BorderStyle = BorderStyle.Fixed3D
            };

            // Add controls to layout
            mainLayout.Controls.Add(buttonPanel, 0, 0);
            mainLayout.Controls.Add(_logBox, 0, 1);
            mainLayout.Controls.Add(_statusLabel, 0, 2);

            Controls.Add(mainLayout);

            // Don't set up logging here - it's done in DI container
        }

        private void InitializeTimer()
        {
            _updateTimer = new System.Windows.Forms.Timer
            {
                Interval = 16  // ~60 FPS
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void LoadSonic1_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Loading Sonic 1...");

                string? gamePath = FindGameFile("sonic1.rsdk", GamePaths.Sonic1SearchPaths);

                if (gamePath == null)
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Select Sonic 1 .rsdk file",
                        Filter = "RSDK files (*.rsdk)|*.rsdk|All files (*.*)|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        gamePath = ofd.FileName;
                    }
                    else
                    {
                        _logger.LogWarning("Sonic 1 .rsdk not provided by user");
                        MessageBox.Show("Please provide a valid Sonic 1 .rsdk file to continue.", "RSDK File Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                if (_rsdkEngine.Initialize(gamePath))
                {
                    _currentGame = "sonic1";
                    _statusLabel.Text = "Running: Sonic 1";
                    _loadSonic1Button.Enabled = false;
                    _logger.LogInformation("Sonic 1 loaded successfully");
                }
                else
                {
                    _logger.LogError("Failed to load Sonic 1");
                    MessageBox.Show(
                        "Failed to load Sonic 1.\n\n" +
                        "This could be because:\n" +
                        "• The native libraries (RSDKv4.dll) are missing\n" +
                        "• The .rsdk file is invalid or corrupted\n" +
                        "• SDL2 or other dependencies are missing\n\n" +
                        "Please check the log for detailed error information.\n\n" +
                        "To fix this:\n" +
                        "1. Run the build_native_libs.sh script to build required libraries\n" +
                        "2. Ensure you have a valid Sonic 1 .rsdk file\n" +
                        "3. Check that all dependencies are installed",
                        "Error Loading Sonic 1",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Sonic 1");
                MessageBox.Show($"Error loading Sonic 1: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSonicCD_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Loading Sonic CD...");

                string? gamePath = FindGameFile("soniccd.rsdk", GamePaths.SonicCDSearchPaths);

                if (gamePath == null)
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Select Sonic CD .rsdk file",
                        Filter = "RSDK files (*.rsdk)|*.rsdk|All files (*.*)|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        gamePath = ofd.FileName;
                    }
                    else
                    {
                        _logger.LogWarning("Sonic CD .rsdk not provided by user");
                        MessageBox.Show("Please provide a valid Sonic CD .rsdk file to continue.", "RSDK File Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                if (_rsdkEngine.Initialize(gamePath))
                {
                    _currentGame = "soniccd";
                    _statusLabel.Text = "Running: Sonic CD";
                    _loadSonicCDButton.Enabled = false;
                    _logger.LogInformation("Sonic CD loaded successfully");
                }
                else
                {
                    _logger.LogError("Failed to load Sonic CD");
                    MessageBox.Show(
                        "Failed to load Sonic CD.\n\n" +
                        "This could be because:\n" +
                        "• The native libraries (RSDKv4.dll) are missing\n" +
                        "• The .rsdk file is invalid or corrupted\n" +
                        "• SDL2 or other dependencies are missing\n\n" +
                        "Please check the log for detailed error information.\n\n" +
                        "To fix this:\n" +
                        "1. Run the build_native_libs.sh script to build required libraries\n" +
                        "2. Ensure you have a valid Sonic CD .rsdk file\n" +
                        "3. Check that all dependencies are installed",
                        "Error Loading Sonic CD",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Sonic CD");
                MessageBox.Show($"Error loading Sonic CD: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSonic2_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Loading Sonic 2...");

                string? gamePath = FindGameFile("sonic2.rsdk", GamePaths.Sonic2SearchPaths);

                if (gamePath == null)
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Select Sonic 2 .rsdk file",
                        Filter = "RSDK files (*.rsdk)|*.rsdk|All files (*.*)|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        gamePath = ofd.FileName;
                    }
                    else
                    {
                        _logger.LogWarning("Sonic 2 .rsdk not provided by user");
                        MessageBox.Show("Please provide a valid Sonic 2 .rsdk file to continue.", "RSDK File Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                if (_rsdkEngine.Initialize(gamePath))
                {
                    _currentGame = "sonic2";
                    _statusLabel.Text = "Running: Sonic 2";
                    _loadSonic2Button.Enabled = false;
                    _logger.LogInformation("Sonic 2 loaded successfully");
                }
                else
                {
                    _logger.LogError("Failed to load Sonic 2");
                    MessageBox.Show(
                        "Failed to load Sonic 2.\n\n" +
                        "This could be because:\n" +
                        "• The native libraries (RSDKv4.dll) are missing\n" +
                        "• The .rsdk file is invalid or corrupted\n" +
                        "• SDL2 or other dependencies are missing\n\n" +
                        "Please check the log for detailed error information.\n\n" +
                        "To fix this:\n" +
                        "1. Run the build_native_libs.sh script to build required libraries\n" +
                        "2. Ensure you have a valid Sonic 2 .rsdk file\n" +
                        "3. Check that all dependencies are installed",
                        "Error Loading Sonic 2",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Sonic 2");
                MessageBox.Show($"Error loading Sonic 2: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSonic3_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Loading Sonic 3 & Knuckles...");

                string? romFile = FindGameFile("sonic3.bin", GamePaths.Sonic3SearchPaths);

                if (romFile == null)
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Select Sonic 3 & Knuckles ROM (sonic3.bin)",
                        Filter = "ROM files (*.bin)|*.bin|All files (*.*)|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        romFile = ofd.FileName;
                    }
                    else
                    {
                        _logger.LogWarning("Sonic 3 & Knuckles ROM not provided by user");
                        MessageBox.Show("Please provide the Sonic 3 & Knuckles ROM file to continue.", "ROM Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                if (_oxygenEngine.Initialize(romFile))
                {
                    _currentGame = "sonic3";
                    
                    // Check if we're in stub mode
                    if (_oxygenEngine.IsStubMode)
                    {
                        _statusLabel.Text = "Sonic 3 & Knuckles (Setup Required)";
                        _logger.LogWarning("Sonic 3 & Knuckles initialized in stub mode - Sonic 3 AIR not found");
                        MessageBox.Show(
                            "Sonic 3 & Knuckles ROM validated!\n\n" +
                            "However, Sonic 3 AIR is not installed. To play:\n\n" +
                            "1. Download Sonic 3 AIR from: https://sonic3air.org/\n" +
                            "2. Extract to 'Sonic 3 AIR Main' folder\n" +
                            "3. Restart the application\n\n" +
                            "Your ROM file is ready and will be used when Sonic 3 AIR is installed.",
                            "Sonic 3 AIR Setup Required",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        _statusLabel.Text = "Running: Sonic 3 & Knuckles";
                        _loadSonic3Button.Enabled = false;
                        _logger.LogInformation("Sonic 3 & Knuckles loaded successfully");
                    }
                }
                else
                {
                    _logger.LogError("Failed to load Sonic 3 & Knuckles");
                    MessageBox.Show(
                        "Failed to load Sonic 3 & Knuckles.\n\n" +
                        "Please check that you have a valid ROM file.\n\n" +
                        "Check the log for detailed error information.",
                        "Error Loading Game",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Sonic 3 & Knuckles");
                MessageBox.Show($"Error loading Sonic 3 & Knuckles: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isTransitioning)
                {
                    // Handle transition between games
                    return;
                }

                switch (_currentGame)
                {
                    case "sonic1":
                        _rsdkEngine.Update();
                        CheckSonic1Completion();
                        break;
                    case "soniccd":
                        _rsdkEngine.Update();
                        CheckSonicCDCompletion();
                        break;
                    case "sonic2":
                        _rsdkEngine.Update();
                        CheckSonic2Completion();
                        break;
                    case "sonic3":
                        _oxygenEngine.Update();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in update loop");
            }
        }

        private async void CheckSonic1Completion()
        {
            // Check if Sonic 1 has been completed and transition to Sonic CD
            if (_rsdkEngine.IsGameComplete() && !_isTransitioning)
            {
                _logger.LogInformation("Sonic 1 completed! Automatically transitioning to Sonic CD...");
                _isTransitioning = true;
                _statusLabel.Text = "Transitioning to Sonic CD...";
                
                try
                {
                    _rsdkEngine.Cleanup();
                    
                    string sonicCDPath = Path.Combine(
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                        "..", "rsdk-source-data", "soniccd.rsdk"
                    );
                    
                    if (_rsdkEngine.Initialize(sonicCDPath))
                    {
                        _currentGame = "soniccd";
                        _statusLabel.Text = "Running: Sonic CD (Palmtree Panic Zone)";
                        _loadSonic1Button.Enabled = false;
                        _logger.LogInformation("Successfully transitioned to Sonic CD");
                    }
                    else
                    {
                        throw new Exception("Failed to initialize Sonic CD");
                    }
                    
                    _isTransitioning = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during automatic transition to Sonic CD");
                    MessageBox.Show($"Error transitioning to Sonic CD: {ex.Message}", "Transition Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _isTransitioning = false;
                    _statusLabel.Text = "Running: Sonic 1 (Transition Failed)";
                }
            }
        }

        private async void CheckSonicCDCompletion()
        {
            // Check if Sonic CD has been completed and transition to Sonic 2
            if (_rsdkEngine.IsGameComplete() && !_isTransitioning)
            {
                _logger.LogInformation("Sonic CD completed! Automatically transitioning to Sonic 2...");
                _isTransitioning = true;
                _statusLabel.Text = "Transitioning to Sonic 2...";
                
                try
                {
                    _rsdkEngine.Cleanup();
                    
                    string sonic2Path = Path.Combine(
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                        "..", "rsdk-source-data", "sonic2.rsdk"
                    );
                    
                    if (_rsdkEngine.Initialize(sonic2Path))
                    {
                        _currentGame = "sonic2";
                        _statusLabel.Text = "Running: Sonic 2 (Emerald Hill Zone)";
                        _loadSonicCDButton.Enabled = false;
                        _logger.LogInformation("Successfully transitioned to Sonic 2");
                    }
                    else
                    {
                        throw new Exception("Failed to initialize Sonic 2");
                    }
                    
                    _isTransitioning = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during automatic transition to Sonic 2");
                    MessageBox.Show($"Error transitioning to Sonic 2: {ex.Message}", "Transition Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _isTransitioning = false;
                    _statusLabel.Text = "Running: Sonic CD (Transition Failed)";
                }
            }
        }

        private async void CheckSonic2Completion()
        {
            // Check if Death Egg has been defeated and we should transition to Sonic 3
            if (_rsdkEngine.IsDeathEggDefeated() && !_isTransitioning)
            {
                _logger.LogInformation("Death Egg defeated! Automatically transitioning to Sonic 3 & Knuckles...");
                _isTransitioning = true;
                _statusLabel.Text = "Transitioning to Sonic 3 & Knuckles...";
                
                try
                {
                    // Create transition manager
                    var transitionManager = new GameTransitionManager(
                        _rsdkEngine,
                        _oxygenEngine,
                        _logger
                    );
                    
                    // Transition to Sonic 3
                    await transitionManager.TransitionToSonic3();
                    
                    // Update state
                    _currentGame = "sonic3";
                    _statusLabel.Text = "Running: Sonic 3 & Knuckles (Angel Island Zone)";
                    _loadSonic2Button.Enabled = false;
                    _loadSonic3Button.Enabled = false;
                    
                    _logger.LogInformation("Successfully transitioned to Sonic 3 & Knuckles");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during automatic transition to Sonic 3");
                    MessageBox.Show($"Error transitioning to Sonic 3 & Knuckles: {ex.Message}", "Transition Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _isTransitioning = false;
                    _statusLabel.Text = "Running: Sonic 2 (Transition Failed)";
                }
            }
        }

        /// <summary>
        /// Searches for a game file in multiple locations.
        /// </summary>
        /// <param name="fileName">The file name to search for</param>
        /// <param name="searchPaths">Paths to search in order</param>
        /// <returns>The full path to the file if found, null otherwise</returns>
        private string? FindGameFile(string fileName, string[] searchPaths)
        {
            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation("Found {FileName} at: {Path}", fileName, path);
                    return Path.GetFullPath(path);
                }
            }
            
            _logger.LogWarning("Could not find {FileName} in any search path", fileName);
            return null;
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(Log), message);
                return;
            }

            _logBox.AppendText(message + Environment.NewLine);
            _logBox.ScrollToCaret();
        }

        private void ManageLocations_Click(object sender, EventArgs e)
        {
            using var dialog = new Tools.CustomPathsDialog(_userSettings, _logger);
            dialog.ShowDialog(this);
            
            // Re-scan after dialog closes
            AutoDetectAndImportGames();
            CheckGameAvailability();
        }

        private void ScanNow_Click(object sender, EventArgs e)
        {
            _logger.LogInformation("Manual scan requested...");
            AutoDetectAndImportGames();
            CheckGameAvailability();
        }

        private void Settings_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Settings dialog coming soon!\n\n" +
                $"Current settings location:\n{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SonicHybridUltimate")}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void About_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Sonic Hybrid Ultimate\n\n" +
                "A unified Sonic experience combining:\n" +
                "  • Sonic the Hedgehog 1\n" +
                "  • Sonic CD\n" +
                "  • Sonic the Hedgehog 2\n" +
                "  • Sonic 3 & Knuckles\n\n" +
                "Built on RSDK and Sonic 3 AIR engines.\n\n" +
                "Visit: https://github.com/Badgerworks-Brewery/Sonic-Hybrid-Ultimate",
                "About Sonic Hybrid Ultimate",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer.Stop();
            _rsdkEngine.Cleanup();
            _oxygenEngine.Cleanup();
            base.OnFormClosing(e);
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // Set up global exception handlers to catch crashes
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Create UILoggerProvider that will be configured later
            var uiLoggerProvider = new UILoggerProvider();
            
            // Load user settings
            var userSettings = new UserSettings();
            userSettings.Load();

            var services = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.AddDebug();
                    builder.AddConsole();
                    builder.AddProvider(uiLoggerProvider);
                })
                .AddSingleton<RSDKEngine>()
                .AddSingleton<OxygenEngine>()
                .AddSingleton<RSDKAnalyzer>()
                .AddSingleton(uiLoggerProvider) // Register so MainForm can access it
                .AddSingleton(userSettings) // Register UserSettings
                .AddSingleton<GameFileAutoDetector>() // Register auto-detector
                .AddSingleton<MainForm>()
                .BuildServiceProvider();

            var mainForm = services.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
        
        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Console.WriteLine($"[CRASH] Unhandled thread exception: {e.Exception}");
            MessageBox.Show(
                $"Application Error:\n\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
                "Application Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"[CRASH] Unhandled domain exception: {e.ExceptionObject}");
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"Fatal Error:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
