using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    /// <summary>
    /// CA22147 / EU4MOFs advanced tools added for the MOF-Predictium VM work plan.
    /// Long-running scientific calculations are executed by python_scripts/mof_vm_tools.py.
    /// </summary>
    public sealed class AdvancedMofToolsForm : Form
    {
        private readonly string currentTrainingCsv;
        private readonly List<string> currentHeaders;
        private readonly string rootOutputDir;
        private TextBox txtLog = null!;
        private Label lblStatus = null!;
        private ProgressBar progress = null!;
        private FlowLayoutPanel buttonPanel = null!;

        public AdvancedMofToolsForm(string trainingCsv, List<string> headers)
        {
            currentTrainingCsv = trainingCsv ?? string.Empty;
            currentHeaders = headers ?? new List<string>();
            rootOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vm_outputs");
            Directory.CreateDirectory(rootOutputDir);
            InitUI();
        }

        private void InitUI()
        {
            Text = "MOF-Predictium Advanced VM Tools";
            Width = 1060;
            Height = 720;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(900, 620);

            var title = new Label
            {
                Text = "MOF-Predictium | EU4MOFs Advanced Validation, Screening and Inverse Design",
                Left = 18,
                Top = 14,
                Width = 1000,
                Height = 28,
                Font = new System.Drawing.Font("Calibri", 13f, System.Drawing.FontStyle.Bold)
            };

            var info = new Label
            {
                Text = "Milestone 2: robustness, uncertainty, OOD, multi-target, chemistry-aware evaluation   |   " +
                       "Milestone 3: HT screening, PCA/UMAP, Pareto, inverse design   |   Extension: MOF-NP round-robin",
                Left = 18,
                Top = 46,
                Width = 1000,
                Height = 34,
                Font = new System.Drawing.Font("Calibri", 9.5f)
            };

            buttonPanel = new FlowLayoutPanel
            {
                Left = 18,
                Top = 86,
                Width = 1008,
                Height = 190,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            AddButton("1. Robustness Suite", async (s, e) => await RunRobustnessAsync());
            AddButton("2. Multi-Target", async (s, e) => await RunMultiTargetAsync());
            AddButton("3. Stratified / Group", async (s, e) => await RunStratifiedAsync());
            AddButton("4. Batch HT Screening", async (s, e) => await RunScreeningAsync());
            AddButton("5. PCA / UMAP Map", async (s, e) => await RunMapAsync());
            AddButton("6. Pareto Front", async (s, e) => await RunParetoAsync());
            AddButton("7. Inverse Design", async (s, e) => await RunInverseAsync());
            AddButton("8. Tutorial Dataset", async (s, e) => await RunTutorialAsync());
            AddButton("9. MOF-NP Round Robin", (s, e) => OpenRoundRobin());
            AddButton("Open Results Folder", (s, e) => OpenFolder(rootOutputDir));
            AddButton("Open Tutorial Guide", (s, e) => OpenDocumentation("EU4MOFS_TUTORIAL.md"));

            lblStatus = new Label
            {
                Left = 18,
                Top = 286,
                Width = 1000,
                Height = 24,
                Text = string.IsNullOrWhiteSpace(currentTrainingCsv)
                    ? "Status: no numeric training data are currently loaded in the main window. File-based tools are still available."
                    : "Status: current main-window training dataset is available for robustness and batch screening.",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            progress = new ProgressBar
            {
                Left = 18,
                Top = 316,
                Width = 1008,
                Height = 8,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            txtLog = new TextBox
            {
                Left = 18,
                Top = 338,
                Width = 1008,
                Height = 330,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 9f),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            txtLog.Text = BuildHelpText();

            Controls.Add(title);
            Controls.Add(info);
            Controls.Add(buttonPanel);
            Controls.Add(lblStatus);
            Controls.Add(progress);
            Controls.Add(txtLog);
        }

        private void AddButton(string text, EventHandler handler)
        {
            var b = new Button
            {
                Text = text,
                Width = 190,
                Height = 42,
                Margin = new Padding(5),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new System.Drawing.Font("Calibri", 9f, System.Drawing.FontStyle.Bold)
            };
            b.Click += handler;
            buttonPanel.Controls.Add(b);
        }

        private string BuildHelpText()
        {
            return
                "MOF-Predictium VM toolbox\r\n" +
                "==========================\r\n" +
                "Robustness Suite: Isolation Forest, Gaussian augmentation, SMOTER-compatible target-tail augmentation, " +
                "bootstrap ensemble uncertainty, learning curves and descriptor-space OOD detection.\r\n\r\n" +
                "Multi-Target: load a headered CSV/XLSX in which the last N columns are regression targets.\r\n" +
                "Stratified / Group: load a headered CSV/XLSX, then specify target and metadata columns such as " +
                "topology,metal_node,linker_family. Generates subgroup and leave-one-group-out metrics.\r\n" +
                "Batch HT Screening: trains on the numeric dataset loaded in the main window, predicts a candidate file, " +
                "ranks candidates, estimates ensemble dispersion and flags descriptor-space OOD candidates.\r\n" +
                "PCA / UMAP: builds 2D property/descriptor maps from numeric columns.\r\n" +
                "Pareto Front: select >=2 named objectives and min/max direction for each.\r\n" +
                "Inverse Design: optimizes descriptors toward a desired property value with a differentiable surrogate, then retrieves nearest existing candidates.\r\n" +
                "Tutorial Dataset: generates a deterministic synthetic educational MOF dataset (seed 42).\r\n" +
                "MOF-NP Round Robin: common lab template, multi-file merge/provenance, metadata completeness and matched-condition inter-laboratory reproducibility analysis.\r\n\r\n" +
                "All results are written under vm_outputs/.";
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private string NewOutputDir(string prefix)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dir = Path.Combine(rootOutputDir, prefix + "_" + stamp);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private string SaveCurrentTrainingCsv()
        {
            if (string.IsNullOrWhiteSpace(currentTrainingCsv))
                throw new InvalidOperationException("Load a numeric Excel dataset in the main window first.");
            string dir = Path.Combine(rootOutputDir, "_working");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "current_training.csv");
            File.WriteAllText(path, currentTrainingCsv);
            return path;
        }

        private string? ChooseTable(string title)
        {
            using var ofd = new OpenFileDialog
            {
                Title = title,
                Filter = "Data files (*.csv;*.xlsx;*.xls;*.tsv)|*.csv;*.xlsx;*.xls;*.tsv|All files (*.*)|*.*"
            };
            return ofd.ShowDialog(this) == DialogResult.OK ? ofd.FileName : null;
        }

        private async Task RunToolAsync(string displayName, string arguments, string outDir)
        {
            try
            {
                SetBusy(true, displayName + " running...");
                txtLog.AppendText("\r\n\r\n> " + displayName + "\r\n");
                string result = await Task.Run(() => ModelHandler.RunScript("mof_vm_tools.py", arguments));
                txtLog.AppendText(result + "\r\n");
                if (result.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(result, displayName + " Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                lblStatus.Text = "Completed: " + displayName + " | " + outDir;
                var open = MessageBox.Show(displayName + " completed.\n\nOpen the output folder?", "Completed",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (open == DialogResult.Yes) OpenFolder(outDir);
            }
            catch (Exception ex)
            {
                txtLog.AppendText("ERROR: " + ex.Message + "\r\n");
                MessageBox.Show(ex.Message, displayName + " Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, "Ready.");
            }
        }

        private void SetBusy(bool busy, string status)
        {
            progress.Visible = busy;
            foreach (Control c in buttonPanel.Controls) c.Enabled = !busy;
            lblStatus.Text = status;
            Refresh();
        }

        private async Task RunRobustnessAsync()
        {
            try
            {
                string training = SaveCurrentTrainingCsv();
                string outDir = NewOutputDir("robustness");
                await RunToolAsync("Robustness Suite",
                    "robustness --input " + Quote(training) + " --outdir " + Quote(outDir), outDir);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Robustness Suite", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        private async Task RunMultiTargetAsync()
        {
            string? input = ChooseTable("Select a headered multi-target dataset");
            if (input == null) return;
            string answer = Microsoft.VisualBasic.Interaction.InputBox(
                "How many LAST columns are regression targets? (minimum 2)", "Multi-Target Prediction", "2");
            if (!int.TryParse(answer, out int nTargets) || nTargets < 2)
            {
                MessageBox.Show("Enter an integer >= 2.");
                return;
            }
            string outDir = NewOutputDir("multitarget");
            await RunToolAsync("Multi-Target Prediction",
                "multitarget --input " + Quote(input) + " --targets " + nTargets + " --outdir " + Quote(outDir), outDir);
        }

        private async Task RunStratifiedAsync()
        {
            string? input = ChooseTable("Select a headered dataset with chemistry metadata");
            if (input == null) return;
            string target = Microsoft.VisualBasic.Interaction.InputBox(
                "Exact target column name:", "Stratified Evaluation", currentHeaders.Count > 0 ? currentHeaders[^1] : "target");
            if (string.IsNullOrWhiteSpace(target)) return;
            string groups = Microsoft.VisualBasic.Interaction.InputBox(
                "Comma-separated metadata columns (example: topology,metal_node,linker_family):",
                "Stratified / Group Evaluation", "topology,metal_node,linker_family");
            if (string.IsNullOrWhiteSpace(groups)) return;
            string exclude = Microsoft.VisualBasic.Interaction.InputBox(
                "Optional comma-separated columns to EXCLUDE from model features (for example other property targets).\n" +
                "Leave blank if the file contains only descriptors + target + group metadata.",
                "Leakage Control", "");
            string outDir = NewOutputDir("stratified");
            await RunToolAsync("Stratified / Group Evaluation",
                "stratified --input " + Quote(input) + " --target " + Quote(target) +
                " --groups " + Quote(groups) + " --exclude " + Quote(exclude) +
                " --outdir " + Quote(outDir), outDir);
        }

        private async Task RunScreeningAsync()
        {
            string training;
            try { training = SaveCurrentTrainingCsv(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }
            string? candidates = ChooseTable("Select candidate MOF descriptor table");
            if (candidates == null) return;
            string model = Microsoft.VisualBasic.Interaction.InputBox(
                "Screening model: LG, RFR, ANN, LSTM, GRU, TFT, CNN_LSTM or MOF_PREDICTIUM",
                "Batch HT Screening", "MOF_PREDICTIUM").Trim().ToUpperInvariant();
            var supportedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "LG", "RFR", "ANN", "LSTM", "GRU", "TFT", "CNN_LSTM", "MOF_PREDICTIUM" };
            if (!supportedModels.Contains(model))
            {
                MessageBox.Show("Choose one of the eight built-in models.");
                return;
            }
            string direction = Microsoft.VisualBasic.Interaction.InputBox(
                "Ranking direction for predicted target: max or min", "Batch HT Screening", "max").Trim().ToLowerInvariant();
            if (direction != "max" && direction != "min") direction = "max";
            string outDir = NewOutputDir("screening");
            await RunToolAsync("Batch High-Throughput Screening",
                "screen --training " + Quote(training) + " --candidates " + Quote(candidates) +
                " --model " + model + " --direction " + direction + " --outdir " + Quote(outDir), outDir);
        }

        private async Task RunMapAsync()
        {
            string? input = ChooseTable("Select MOF descriptor/property table for PCA/UMAP");
            if (input == null) return;
            string colorColumn = Microsoft.VisualBasic.Interaction.InputBox(
                "Optional numeric property column used to colour the 2D map (for example predicted_target).\n" +
                "Leave blank for an uncoloured map.", "PCA / UMAP Colour", "");
            string outDir = NewOutputDir("property_map");
            await RunToolAsync("PCA / UMAP Property Map",
                "map --input " + Quote(input) + " --color " + Quote(colorColumn) +
                " --outdir " + Quote(outDir), outDir);
        }

        private async Task RunParetoAsync()
        {
            string? input = ChooseTable("Select a headered table for Pareto analysis");
            if (input == null) return;
            string objectives = Microsoft.VisualBasic.Interaction.InputBox(
                "Comma-separated objective column names (minimum 2):", "Pareto Front", "property_1,property_2");
            if (string.IsNullOrWhiteSpace(objectives)) return;
            string directions = Microsoft.VisualBasic.Interaction.InputBox(
                "Comma-separated direction for each objective (max/min):", "Pareto Front", "max,max");
            if (string.IsNullOrWhiteSpace(directions)) return;
            string outDir = NewOutputDir("pareto");
            await RunToolAsync("Pareto-Front Analysis",
                "pareto --input " + Quote(input) + " --objectives " + Quote(objectives) +
                " --directions " + Quote(directions) + " --outdir " + Quote(outDir), outDir);
        }

        private async Task RunInverseAsync()
        {
            string training;
            try { training = SaveCurrentTrainingCsv(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }
            string? candidates = ChooseTable("Select candidate descriptor table for inverse design");
            if (candidates == null) return;
            string targetValue = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the desired TARGET PROPERTY value.\n" +
                "A differentiable surrogate will optimize descriptors by gradient descent and then retrieve the closest existing candidates.",
                "Gradient-Based Inverse Design", "");
            if (!double.TryParse(targetValue.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double parsedTarget))
            {
                MessageBox.Show("Enter a valid numeric target property value.");
                return;
            }
            string targetArg = parsedTarget.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            string outDir = NewOutputDir("inverse_design");
            await RunToolAsync("Gradient-Based Inverse Design",
                "inverse --training " + Quote(training) + " --candidates " + Quote(candidates) +
                " --target-value " + targetArg + " --top 20 --outdir " + Quote(outDir), outDir);
        }

        private async Task RunTutorialAsync()
        {
            string outDir = NewOutputDir("tutorial_dataset");
            await RunToolAsync("Tutorial Dataset Generation",
                "tutorial --outdir " + Quote(outDir) + " --rows 240", outDir);
        }

        private void OpenRoundRobin()
        {
            using var form = new RoundRobinForm();
            form.ShowDialog(this);
        }

        private void OpenDocumentation(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", fileName);
            if (!File.Exists(path))
            {
                MessageBox.Show("Documentation file was not found: " + path);
                return;
            }
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open folder: " + ex.Message);
            }
        }
    }
}
