using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace SonicHybridUltimate
{
    public class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SonicHybridClient());
        }
    }

    public class SonicHybridClient : Form
    {
        // Native methods for RSDK and Oxygen Engine
        [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
        private static extern int InitRSDKv4(string dataPath);

        [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UpdateRSDKv4();

        [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CleanupRSDKv4();

        [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
        private static extern int InitOxygenEngine(string scriptPath);

        [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UpdateOxygenEngine();

        [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CleanupOxygenEngine();

        private string currentGame = "None";
        private readonly string configPath = "config.json";
        private readonly string logPath = "hybrid.log";
        private RichTextBox logBox;
        private Label statusLabel;
        private Dictionary<string, string> gameState;
        private bool engineRunning = false;

        // Core paths
        private readonly string baseRSDKPath;
        private readonly string sonic3Path;
        private readonly string dataPath;
        private readonly string scriptPath;

        // Game-specific paths
        private readonly string sonic1DataPath;
        private readonly string sonic2DataPath;
        private readonly string sonicCDDataPath;

        public SonicHybridClient()
        {
            // Initialize base paths
            baseRSDKPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Hybrid-RSDK Main");
            sonic3Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sonic 3 AIR Main", "Oxygen", "sonic3air");
            dataPath = Path.Combine(baseRSDKPath, "Data");
            scriptPath = Path.Combine(sonic3Path, "scripts");

            // Initialize game-specific paths
            sonic1DataPath = Path.Combine(dataPath, "Sonic1");
            sonic2DataPath = Path.Combine(dataPath, "Sonic2");
            sonicCDDataPath = Path.Combine(dataPath, "SonicCD");

            // Load or create game state
            gameState = LoadGameState();

            InitializeUI();
            ValidateEnvironment();
        }

        private Dictionary<string, string> LoadGameState()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading game state: {ex.Message}");
            }
            return new Dictionary<string, string>();
        }

        private void SaveGameState()
        {
            try
            {
                var json = JsonSerializer.Serialize(gameState);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                LogMessage($"Error saving game state: {ex.Message}");
            }
        }

        private void InitializeUI()
        {
            this.Text = "Sonic Hybrid Ultimate";
            this.Size = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            statusLabel = new Label
            {
                Text = "Ready to Start",
                Location = new Point(20, 20),
                AutoSize = true
            };

            var startButton = new Button
            {
                Text = "Start Game",
                Location = new Point(350, 20),
                Size = new Size(100, 30)
            };
            startButton.Click += StartGame;

            logBox = new RichTextBox
            {
                Location = new Point(20, 60),
                Size = new Size(740, 480),
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10)
            };

            this.Controls.AddRange(new Control[] { statusLabel, startButton, logBox });

            // Set up game loop timer
            var gameTimer = new Timer { Interval = 16 }; // ~60 FPS
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void ValidateEnvironment()
        {
            LogMessage("Validating environment...");

            // Check RSDK environment
            if (!Directory.Exists(baseRSDKPath))
            {
                LogMessage("ERROR: RSDK directory not found!");
                return;
            }

            // Check Sonic 3 AIR environment
            if (!Directory.Exists(sonic3Path))
            {
                LogMessage("ERROR: Sonic 3 AIR source directory not found!");
                return;
            }

            // Check game data directories
            var requiredPaths = new Dictionary<string, string>
            {
                { "Sonic 1", sonic1DataPath },
                { "Sonic 2", sonic2DataPath },
                { "Sonic CD", sonicCDDataPath }
            };

            foreach (var path in requiredPaths)
            {
                if (!Directory.Exists(path.Value))
                {
                    LogMessage($"ERROR: {path.Key} data directory not found at {path.Value}");
                    return;
                }
            }

            // Check script files
            var mainLemonPath = Path.Combine(scriptPath, "main.lemon");
            if (!File.Exists(mainLemonPath))
            {
                LogMessage("ERROR: Sonic 3 AIR main script not found!");
                return;
            }

            LogMessage("Environment validation complete");
        }

        private void StartGame(object? sender, EventArgs e)
        {
            if (engineRunning)
            {
                LogMessage("Game is already running!");
                return;
            }

            try
            {
                StartRSDK();
            }
            catch (Exception ex)
            {
                LogMessage($"Error starting game: {ex.Message}");
                MessageBox.Show($"Failed to start game: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartRSDK()
        {
            LogMessage("Starting RSDK...");
            currentGame = "RSDK";
            statusLabel.Text = "Running: RSDK";

            // Initialize RSDK
            var result = InitRSDKv4(dataPath);
            if (result != 0)
            {
                throw new Exception("Failed to initialize RSDK");
            }

            engineRunning = true;
        }

        private void StartSonic3()
        {
            LogMessage("Starting Sonic 3 AIR...");
            currentGame = "SONIC3";
            statusLabel.Text = "Running: Sonic 3 AIR";

            // Clean up RSDK first
            CleanupRSDKv4();

            // Initialize Oxygen Engine
            var result = InitOxygenEngine(scriptPath);
            if (result != 0)
            {
                throw new Exception("Failed to initialize Oxygen Engine");
            }

            // Set initial state
            gameState["LAST_GAME"] = "SONIC2";
            SaveGameState();

            engineRunning = true;
        }

        private bool IsDeathEggDefeated()
        {
            // Check all required conditions for Death Egg completion
            return IsInDeathEggZone() && 
                   GetDeathEggRobotState() == 1 && 
                   GetDeathEggExplosionTimer() > 0;
        }

        private bool IsInDeathEggZone()
        {
            // Memory address for current zone ID
            const int ADDR_CURRENT_ZONE = 0x203A40;
            byte zoneId = Marshal.ReadByte(new IntPtr(ADDR_CURRENT_ZONE));
            return zoneId == 0x0B; // Death Egg Zone ID
        }

        private int GetDeathEggRobotState()
        {
            // Memory address for Death Egg Robot state
            const int ADDR_ROBOT_STATE = 0x203A44;
            return Marshal.ReadByte(new IntPtr(ADDR_ROBOT_STATE));
        }

        private int GetDeathEggExplosionTimer()
        {
            // Memory address for explosion timer
            const int ADDR_EXPLOSION_TIMER = 0x203A48;
            return Marshal.ReadByte(new IntPtr(ADDR_EXPLOSION_TIMER));
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            if (!engineRunning) return;

            try
            {
                if (currentGame == "RSDK")
                {
                    UpdateRSDKv4();
                    
                    // Check for Sonic 2 completion
                    if (IsDeathEggDefeated())
                    {
                        LogMessage("Death Egg Zone completed! Transitioning to Sonic 3...");
                        SaveGameState();
                        CleanupRSDKv4();
                        StartSonic3();
                    }
                }
                else if (currentGame == "Sonic3")
                {
                    UpdateOxygenEngine();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error in game loop: {ex.Message}");
                engineRunning = false;
            }
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";

            try
            {
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
                
                this.Invoke(() =>
                {
                    logBox.AppendText(logMessage + Environment.NewLine);
                    logBox.ScrollToCaret();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logging error: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (engineRunning)
            {
                LogMessage("Shutting down game engine...");
                if (currentGame == "RSDK")
                    CleanupRSDKv4();
                else if (currentGame == "SONIC3")
                    CleanupOxygenEngine();
            }
            SaveGameState();
            base.OnFormClosing(e);
        }
    }
}
