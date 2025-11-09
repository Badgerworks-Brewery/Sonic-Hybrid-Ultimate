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
    public partial class MainForm : Form
    {
        private readonly ILogger<MainForm> _logger;
        private readonly IServiceProvider _services;
        private readonly RSDKEngine _rsdkEngine;
        private readonly OxygenEngine _oxygenEngine;
        private readonly RSDKAnalyzer _rsdkAnalyzer;

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

        public MainForm(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = _services.GetRequiredService<ILogger<MainForm>>();
            _rsdkEngine = _services.GetRequiredService<RSDKEngine>();
            _oxygenEngine = _services.GetRequiredService<OxygenEngine>();
            _rsdkAnalyzer = _services.GetRequiredService<RSDKAnalyzer>();

            InitializeComponents();
            InitializeTimer();

            _logger.LogInformation("MainForm initialized");
        }

        private void InitializeComponents()
        {
            Text = "Sonic Hybrid Ultimate";
            Size = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;

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
                Enabled = false  // Disabled until Sonic 2 is completed
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

            // Set up logging
            var loggerProvider = new LoggerProvider(Log);
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddProvider(loggerProvider);
                builder.AddDebug();
                builder.AddConsole();
            });
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

                var defaultPath = Path.Combine("Hybrid-RSDK-Main", "Data", "sonic1.rsdk");
                string gamePath = defaultPath;

                if (!File.Exists(gamePath))
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
                    MessageBox.Show("Failed to load Sonic 1. Please check the log for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                var defaultPath = Path.Combine("Hybrid-RSDK-Main", "Data", "soniccd.rsdk");
                string gamePath = defaultPath;

                if (!File.Exists(gamePath))
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
                    MessageBox.Show("Failed to load Sonic CD. Please check the log for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                var defaultPath = Path.Combine("Hybrid-RSDK-Main", "Data", "sonic2.rsdk");
                string gamePath = defaultPath;

                if (!File.Exists(gamePath))
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
                    MessageBox.Show("Failed to load Sonic 2. Please check the log for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                var defaultDll = Path.Combine("Sonic 3 AIR Main", "Oxygen", "sonic3air.dll");
                string oxygenDll = defaultDll;

                if (!File.Exists(oxygenDll))
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Select Sonic 3 AIR oxygen DLL (sonic3air.dll)",
                        Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        oxygenDll = ofd.FileName;
                    }
                    else
                    {
                        _logger.LogWarning("Oxygen DLL not provided by user");
                        MessageBox.Show("Please provide the Sonic 3 AIR 'sonic3air.dll' to continue.", "Oxygen DLL Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                if (_oxygenEngine.Initialize(oxygenDll))
                {
                    _currentGame = "sonic3";
                    _statusLabel.Text = "Running: Sonic 3 & Knuckles";
                    _loadSonic3Button.Enabled = false;
                    _logger.LogInformation("Sonic 3 & Knuckles loaded successfully");
                }
                else
                {
                    _logger.LogError("Failed to load Sonic 3 & Knuckles");
                    MessageBox.Show("Failed to load Sonic 3 & Knuckles. Please check the log for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    case "soniccd":
                    case "sonic2":
                        _rsdkEngine.Update();
                        if (_currentGame == "sonic2")
                        {
                            CheckSonic2Completion();
                        }
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
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var services = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.AddDebug();
                    builder.AddConsole();
                })
                .AddSingleton<RSDKEngine>()
                .AddSingleton<OxygenEngine>()
                .AddSingleton<RSDKAnalyzer>()
                .AddSingleton<MainForm>()
                .BuildServiceProvider();

            Application.Run(services.GetRequiredService<MainForm>());
        }
    }
}
