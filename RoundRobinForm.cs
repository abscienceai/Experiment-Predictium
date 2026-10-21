using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    /// <summary>
    /// Local-only helper for MOF nanoparticle round-robin studies.
    /// Creates a common data template, merges laboratory files while preserving provenance,
    /// and performs descriptive inter-laboratory reproducibility analyses.
    /// </summary>
    public sealed class RoundRobinForm : Form
    {
        private readonly string rootOutputDir;
        private FlowLayoutPanel buttonPanel = null!;
        private TextBox txtLog = null!;
        private Label lblStatus = null!;
        private ProgressBar progress = null!;

        public RoundRobinForm()
        {
            rootOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vm_outputs", "round_robin");
            Directory.CreateDirectory(rootOutputDir);
            InitUI();
        }

        private void InitUI()
        {
            Text = "MOF-NP Round-Robin & Inter-Laboratory Reproducibility";
            Width = 1060;
            Height = 720;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(900, 620);

            var title = new Label
            {
                Text = "MOF-NP Round-Robin | Data Harmonisation and Inter-Laboratory Reproducibility",
                Left = 18,
                Top = 14,
                Width = 1000,
                Height = 28,
                Font = new System.Drawing.Font("Calibri", 13f, System.Drawing.FontStyle.Bold)
            };

            var info = new Label
            {
                Text = "Workflow: common template -> one file per lab -> automatic merge/provenance -> matched-condition agreement analysis. " +
                       "This module supports study coordination; it does not replace an agreed toxicology protocol or expert interpretation.",
                Left = 18,
                Top = 46,
                Width = 1000,
                Height = 44,
                Font = new System.Drawing.Font("Calibri", 9.5f)
            };

            buttonPanel = new FlowLayoutPanel
            {
                Left = 18,
                Top = 98,
                Width = 1008,
                Height = 146,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            AddButton("1. Create Lab Template", async (s, e) => await CreateTemplateAsync());
            AddButton("2. Merge + Analyze Labs", async (s, e) => await MergeAndAnalyzeAsync());
            AddButton("3. Analyze Existing Merge", async (s, e) => await AnalyzeExistingAsync());
            AddButton("4. Synthetic Self-Test", async (s, e) => await RunDemoAsync());
            AddButton("Open Round-Robin Guide", (s, e) => OpenDocumentation("MOF_NP_ROUND_ROBIN_GUIDE.md"));
            AddButton("Open Results Folder", (s, e) => OpenFolder(rootOutputDir));

            lblStatus = new Label
            {
                Left = 18,
                Top = 258,
                Width = 1000,
                Height = 24,
                Text = "Ready. Start by creating the common laboratory template.",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            progress = new ProgressBar
            {
                Left = 18,
                Top = 288,
                Width = 1008,
                Height = 8,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            txtLog = new TextBox
            {
                Left = 18,
                Top = 310,
                Width = 1008,
                Height = 360,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 9f),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = BuildHelpText()
            };

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
                Width = 195,
                Height = 42,
                Margin = new Padding(5),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new System.Drawing.Font("Calibri", 9f, System.Drawing.FontStyle.Bold)
            };
            b.Click += handler;
            buttonPanel.Controls.Add(b);
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

        private async Task<string> RunRoundRobinAsync(string displayName, string arguments, string outDir)
        {
            try
            {
                SetBusy(true, displayName + " running...");
                txtLog.AppendText("\r\n\r\n> " + displayName + "\r\n");
                string result = await Task.Run(() => ModelHandler.RunScript("round_robin.py", arguments));
                txtLog.AppendText(result + "\r\n");
                if (result.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(result, displayName + " Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return result;
                }
                lblStatus.Text = "Completed: " + displayName + " | " + outDir;
                return result;
            }
            catch (Exception ex)
            {
                string msg = "Error: " + ex.Message;
                txtLog.AppendText(msg + "\r\n");
                MessageBox.Show(ex.Message, displayName + " Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return msg;
            }
            finally
            {
                SetBusy(false, "Ready.");
            }
        }

        private async Task CreateTemplateAsync()
        {
            string outDir = NewOutputDir("template");
            string result = await RunRoundRobinAsync(
                "Create Round-Robin Template",
                "template --outdir " + Quote(outDir),
                outDir);
            if (!result.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Template created. Distribute the same template to every participating laboratory.\n\n" +
                    "Each lab should keep the shared study_id, material_id, batch_id and protocol_version, " +
                    "use its own pseudonymous lab_id, and enter one replicate measurement per row.",
                    "Template Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                OpenFolder(outDir);
            }
        }

        private async Task MergeAndAnalyzeAsync()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select all incoming laboratory files for the same round-robin study",
                Filter = "Data files (*.csv;*.xlsx;*.xls;*.tsv)|*.csv;*.xlsx;*.xls;*.tsv|All files (*.*)|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog(this) != DialogResult.OK || ofd.FileNames.Length == 0) return;

            string root = NewOutputDir("merged_study");
            string mergeDir = Path.Combine(root, "01_merge");
            string analysisDir = Path.Combine(root, "02_analysis");
            Directory.CreateDirectory(mergeDir);
            Directory.CreateDirectory(analysisDir);

            string inputs = string.Join(" ", ofd.FileNames.Select(Quote));
            string mergeResult = await RunRoundRobinAsync(
                "Merge Laboratory Files",
                "merge --inputs " + inputs + " --outdir " + Quote(mergeDir),
                mergeDir);
            if (mergeResult.StartsWith("Error", StringComparison.OrdinalIgnoreCase)) return;

            string mergedPath = Path.Combine(mergeDir, "merged_round_robin.csv");
            if (!File.Exists(mergedPath))
            {
                MessageBox.Show("Merge finished but merged_round_robin.csv was not found.", "Round-Robin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string analysisResult = await RunRoundRobinAsync(
                "Inter-Laboratory Reproducibility Analysis",
                "analyze --input " + Quote(mergedPath) + " --outdir " + Quote(analysisDir),
                analysisDir);
            if (!analysisResult.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Laboratory files were combined without manual copy/paste and analysed only within matched experimental conditions.\n\n" +
                    "Review 01_merge/merge_report.json and metadata_completeness.csv first, then inspect 02_analysis/ for agreement results and review flags.",
                    "Round-Robin Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                OpenFolder(root);
            }
        }

        private async Task AnalyzeExistingAsync()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select merged_round_robin.csv or another round-robin table",
                Filter = "Data files (*.csv;*.xlsx;*.xls;*.tsv)|*.csv;*.xlsx;*.xls;*.tsv|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            string outDir = NewOutputDir("analysis");
            string result = await RunRoundRobinAsync(
                "Inter-Laboratory Reproducibility Analysis",
                "analyze --input " + Quote(ofd.FileName) + " --outdir " + Quote(outDir),
                outDir);
            if (!result.StartsWith("Error", StringComparison.OrdinalIgnoreCase)) OpenFolder(outDir);
        }

        private async Task RunDemoAsync()
        {
            string outDir = NewOutputDir("synthetic_demo");
            string result = await RunRoundRobinAsync(
                "Synthetic Round-Robin Self-Test",
                "demo --outdir " + Quote(outDir),
                outDir);
            if (!result.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "A deterministic synthetic five-laboratory dataset and its analysis were generated. " +
                    "These data are software validation only and must not be presented as experimental MOF toxicity evidence.",
                    "Self-Test Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                OpenFolder(outDir);
            }
        }

        private void SetBusy(bool busy, string status)
        {
            progress.Visible = busy;
            foreach (Control c in buttonPanel.Controls) c.Enabled = !busy;
            lblStatus.Text = status;
            Refresh();
        }

        private string BuildHelpText()
        {
            return
                "MOF-NP ROUND-ROBIN MODULE\r\n" +
                "=========================\r\n" +
                "1) Create Lab Template: generates CSV/XLSX templates and a data dictionary.\r\n" +
                "2) Merge + Analyze Labs: select all returned lab files at once. The program canonicalises column names, " +
                "keeps source_file/source_row provenance, creates metadata-completeness reports, safely harmonises recognised dose units, " +
                "then analyses only matched material/batch/cell/assay/endpoint/dose/exposure/unit conditions.\r\n" +
                "3) Analyze Existing Merge: re-run analysis after a coordinator corrects metadata or excludes invalid rows.\r\n" +
                "4) Synthetic Self-Test: validates the software path with non-experimental demonstration data.\r\n\r\n" +
                "Analysis outputs include within-lab CV, between-lab CV, standard and robust deviation scores, protocol metadata deviations, " +
                "ICC(2,1) where mathematically supportable, pairwise lab correlations, CSV summaries and PNG plots.\r\n\r\n" +
                "IMPORTANT: review flags are investigation prompts, not proof that a laboratory is wrong. Experimental design and toxicology decisions remain with the study team.";
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
