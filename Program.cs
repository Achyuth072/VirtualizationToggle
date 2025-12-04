using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VirtualizationToggle
{
    public class VirtTrayApp : ApplicationContext
    {
        private NotifyIcon trayIcon = null!;
        private ContextMenuStrip contextMenu = null!;
        private ToolStripMenuItem statusMenuItem = null!;
        private ToolStripMenuItem toggleMenuItem = null!;
        private ToolStripMenuItem startupMenuItem = null!;
        private ToolStripMenuItem exitMenuItem = null!;
        private bool isVirtualizationEnabled;
        private const string APP_NAME = "VirtualizationToggle";

        public VirtTrayApp()
        {
            InitializeComponent();
            CheckVirtualizationStatus();
            UpdateUI();
        }

        private void InitializeComponent()
        {
            // Create context menu
            contextMenu = new ContextMenuStrip();
            
            statusMenuItem = new ToolStripMenuItem("Status: Checking...");
            statusMenuItem.Enabled = false;
            
            toggleMenuItem = new ToolStripMenuItem("Toggle Virtualization", null, OnToggle!);
            
            startupMenuItem = new ToolStripMenuItem("Run at Windows Startup", null, OnStartupToggle!);
            startupMenuItem.CheckOnClick = true;
            startupMenuItem.Checked = IsInStartup();
            
            exitMenuItem = new ToolStripMenuItem("Exit", null, OnExit!);

            contextMenu.Items.Add(statusMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(toggleMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(startupMenuItem);
            contextMenu.Items.Add(exitMenuItem);

            // Create tray icon
            trayIcon = new NotifyIcon()
            {
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            trayIcon.MouseClick += OnTrayIconClick!;
        }

        private void CheckVirtualizationStatus()
        {
            try
            {
                // Check both features
                bool hypervisorEnabled = CheckFeatureStatus("Microsoft-Hyper-V-Hypervisor");
                bool vmPlatformEnabled = CheckFeatureStatus("VirtualMachinePlatform");
                
                // If either is enabled, consider virtualization enabled
                isVirtualizationEnabled = hypervisorEnabled || vmPlatformEnabled;
            }
            catch
            {
                isVirtualizationEnabled = false;
            }
        }

        private bool CheckFeatureStatus(string featureName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -Command \"(Get-WindowsOptionalFeature -Online -FeatureName {featureName}).State\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();
                        return output.Contains("Enabled");
                    }
                }
            }
            catch { }
            
            return false;
        }

        private void UpdateUI()
        {
            if (isVirtualizationEnabled)
            {
                trayIcon.Icon = CreateIcon(Color.Green);
                trayIcon.Text = "Virtualization: Enabled\n(WSL2/WSA available)";
                statusMenuItem.Text = "Status: Enabled ✓";
                toggleMenuItem.Text = "Disable Virtualization";
            }
            else
            {
                trayIcon.Icon = CreateIcon(Color.Red);
                trayIcon.Text = "Virtualization: Disabled\n(ThrottleStop available)";
                statusMenuItem.Text = "Status: Disabled ✗";
                toggleMenuItem.Text = "Enable Virtualization";
            }
        }

        private Icon CreateIcon(Color color)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 2, 2, 12, 12);
                }
                using (Pen pen = new Pen(Color.White, 2))
                {
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void OnTrayIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OnToggle(sender, e);
            }
        }

        private void OnToggle(object sender, EventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("This application requires administrator privileges.\nPlease restart as administrator.",
                    "Administrator Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string action = isVirtualizationEnabled ? "disable" : "enable";
            string message = isVirtualizationEnabled
                ? "This will disable virtualization.\nThrottleStop will be available after restart.\n\nContinue?"
                : "This will enable virtualization.\nWSL2/WSA will be available after restart.\n\nContinue?";

            var result = MessageBox.Show(message, "Toggle Virtualization",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                ToggleVirtualization();
            }
        }

        private async void ToggleVirtualization()
        {
            string action = isVirtualizationEnabled ? "Disabling" : "Enabling";
            
            // Show tray notification
            trayIcon.ShowBalloonTip(2000, "Please Wait", 
                $"{action} virtualization features...", ToolTipIcon.Info);

            // Create and show loading dialog
            Form? loadingDialog = null;
            Label? loadingLabel = null;
            ProgressBar? progressBar = null;

            try
            {
                loadingDialog = new Form
                {
                    Text = "Please Wait",
                    Size = new Size(350, 150),
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    ControlBox = false,
                    TopMost = true
                };

                loadingLabel = new Label
                {
                    Text = $"{action} virtualization features...\nThis may take a moment.",
                    Location = new Point(30, 20),
                    Size = new Size(290, 40),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                progressBar = new ProgressBar
                {
                    Location = new Point(30, 70),
                    Size = new Size(290, 23),
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30
                };

                loadingDialog.Controls.Add(loadingLabel);
                loadingDialog.Controls.Add(progressBar);
                loadingDialog.Show();
                Application.DoEvents();

                string state = isVirtualizationEnabled ? "Disable" : "Enable";
                
                // Toggle both features silently - run asynchronously
                string commands = $@"
                    {state}-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-Hypervisor -NoRestart | Out-Null;
                    {state}-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart | Out-Null
                ";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -Command \"{commands}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };

                var process = new Process { StartInfo = psi };
                process.Start();
                
                // Wait asynchronously so UI can update
                await System.Threading.Tasks.Task.Run(() => process.WaitForExit());

                loadingDialog.Close();

                if (process.ExitCode == 0)
                {
                    ShowRestartDialog();
                }
                else
                {
                    MessageBox.Show("Failed to toggle virtualization features.\nPlease check if you have administrator privileges.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                loadingDialog?.Close();
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowRestartDialog()
        {
            var dialog = new Form
            {
                Text = "Restart Required",
                Size = new Size(400, 220),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var label = new Label
            {
                Text = "A restart is required for changes to take effect.\nWhen would you like to restart?",
                Location = new Point(30, 30),
                Size = new Size(340, 40),
                TextAlign = ContentAlignment.TopCenter
            };

            var restartNowBtn = new Button
            {
                Text = "Restart Now",
                Location = new Point(50, 90),
                Size = new Size(130, 35),
                TabIndex = 0
            };
            restartNowBtn.Click += (s, e) =>
            {
                Process.Start("shutdown", "/r /t 0");
                dialog.Close();
            };

            var restartLaterBtn = new Button
            {
                Text = "Schedule Restart",
                Location = new Point(220, 90),
                Size = new Size(130, 35),
                TabIndex = 1
            };
            restartLaterBtn.Click += (s, e) =>
            {
                dialog.Close();
                ShowScheduleDialog();
            };

            var cancelBtn = new Button
            {
                Text = "Restart Later Manually",
                Location = new Point(100, 140),
                Size = new Size(200, 35),
                TabIndex = 2
            };
            cancelBtn.Click += (s, e) =>
            {
                trayIcon.ShowBalloonTip(3000, "Restart Pending",
                    "Remember to restart your computer for changes to take effect.",
                    ToolTipIcon.Info);
                dialog.Close();
            };

            dialog.Controls.Add(label);
            dialog.Controls.Add(restartNowBtn);
            dialog.Controls.Add(restartLaterBtn);
            dialog.Controls.Add(cancelBtn);
            dialog.ShowDialog();
        }

        private void ShowScheduleDialog()
        {
            var dialog = new Form
            {
                Text = "Schedule Restart",
                Size = new Size(360, 200),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var label = new Label
            {
                Text = "Select when to restart:",
                Location = new Point(30, 30),
                Size = new Size(300, 20)
            };

            var combo = new ComboBox
            {
                Location = new Point(30, 60),
                Size = new Size(300, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            combo.Items.AddRange(new object[] {
                "In 5 minutes",
                "In 30 minutes",
                "In 1 hour",
                "In 2 hours"
            });
            combo.SelectedIndex = 0;

            var okBtn = new Button
            {
                Text = "Schedule",
                Location = new Point(80, 110),
                Size = new Size(100, 35)
            };
            okBtn.Click += (s, e) =>
            {
                int seconds = combo.SelectedIndex switch
                {
                    0 => 300,
                    1 => 1800,
                    2 => 3600,
                    3 => 7200,
                    _ => 300
                };
                Process.Start("shutdown", $"/r /t {seconds}");
                trayIcon.ShowBalloonTip(3000, "Restart Scheduled",
                    $"Your computer will restart {combo.SelectedItem?.ToString()?.ToLower() ?? "soon"}.",
                    ToolTipIcon.Info);
                dialog.Close();
            };

            var cancelBtn = new Button
            {
                Text = "Cancel",
                Location = new Point(190, 110),
                Size = new Size(100, 35)
            };
            cancelBtn.Click += (s, e) => dialog.Close();

            dialog.Controls.Add(label);
            dialog.Controls.Add(combo);
            dialog.Controls.Add(okBtn);
            dialog.Controls.Add(cancelBtn);
            dialog.ShowDialog();
        }

        private bool IsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private void OnStartupToggle(object sender, EventArgs e)
        {
            if (startupMenuItem.Checked)
            {
                AddToStartup();
            }
            else
            {
                RemoveFromStartup();
            }
        }

        private bool IsInStartup()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{APP_NAME}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        return process.ExitCode == 0;
                    }
                }
            }
            catch { }
            
            return false;
        }

        private void AddToStartup()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string userName = Environment.UserName;
                
                // Create a scheduled task that runs at logon with highest privileges
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /F /TN \"{APP_NAME}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        trayIcon.ShowBalloonTip(2000, "Startup Enabled",
                            "App will now run at Windows startup with admin privileges.", ToolTipIcon.Info);
                    }
                    else
                    {
                        throw new Exception("Failed to create scheduled task");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add to startup: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                startupMenuItem.Checked = false;
            }
        }

        private void RemoveFromStartup()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /F /TN \"{APP_NAME}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        trayIcon.ShowBalloonTip(2000, "Startup Disabled",
                            "App will no longer run at Windows startup.", ToolTipIcon.Info);
                    }
                    else
                    {
                        throw new Exception("Failed to delete scheduled task");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove from startup: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                startupMenuItem.Checked = true;
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trayIcon?.Dispose();
                contextMenu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new VirtTrayApp());
        }
    }
}