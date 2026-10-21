using System;
using System.Diagnostics;
using System.IO;

namespace ExperimentPredictorApp
{
    public static class ModelHandler
    {
        // Run a prediction, optionally saving or loading a model file
        public static string RunModel(string modelName, string trainingCsv,
                                      string predictInput, string modelPath = null)
        {
            string scriptsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "python_scripts");

            // Normalize model names with invariant casing. This avoids locale-specific
            // transformations on Turkish Windows (for example I -> ı), which would
            // otherwise generate invalid script names such as predict_mof_predıctıum.py.
            string modelKey = (modelName ?? string.Empty).Trim().ToUpperInvariant();
            string scriptFile = modelKey switch
            {
                "BENCHMARK" => "benchmark.py",
                "LG" => "predict_lg.py",
                "RFR" => "predict_rfr.py",
                "ANN" => "predict_ann.py",
                "LSTM" => "predict_lstm.py",
                "GRU" => "predict_gru.py",
                "TFT" => "predict_tft.py",
                "CNN_LSTM" => "predict_cnn_lstm.py",
                "MOF_PREDICTIUM" => "predict_mof_predictium.py",
                _ => $"predict_{(modelName ?? string.Empty).Trim().ToLowerInvariant()}.py"
            };
            string scriptPath = Path.Combine(scriptsDir, scriptFile);

            string tempTrainFile = Path.Combine(scriptsDir, "temp_training.csv");
            File.WriteAllText(tempTrainFile, trainingCsv);

            string pythonPath = FindPython();
            if (string.IsNullOrEmpty(pythonPath))
                return "Error: Python not found. Please install Python and add it to PATH.";

            // Build arguments — model path is optional 3rd argument
            string args = $"\"{scriptPath}\" \"{Path.GetFileName(tempTrainFile)}\" \"{predictInput}\"";
            if (!string.IsNullOrEmpty(modelPath))
                args += $" \"{modelPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = args,
                WorkingDirectory = scriptsDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return "Error: " + errors;

            return output.Trim();
        }

        // Determine the appropriate file extension for each model type
        public static string GetModelExtension(string modelName)
        {
            return (modelName ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "LG" => ".pkl",
                "RFR" => ".pkl",
                "ANN" => ".pkl",
                "LSTM" => ".pt",
                "GRU" => ".pt",
                "TFT" => ".pt",
                "CNN_LSTM" => ".pt",
                "MOF_PREDICTIUM" => ".pkl",
                _ => ".pkl"
            };
        }

        // Run any Python script with arbitrary arguments
        public static string RunScript(string scriptFileName, string args)
        {
            string scriptsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "python_scripts");
            string scriptPath = Path.Combine(scriptsDir, scriptFileName);
            string pythonPath = FindPython();

            if (string.IsNullOrEmpty(pythonPath))
                return "Error: Python not found.";
            if (!File.Exists(scriptPath))
                return $"Error: Script not found: {scriptPath}";

            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = "\"" + scriptPath + "\" " + args,
                WorkingDirectory = scriptsDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return "Error: " + errors;

            return output.Trim();
        }

        // Locate the Python executable on the system
        private static string FindPython()
        {
            // 1. Check for a local virtual environment first
            string tfenv = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "python_scripts", "tfenv", "Scripts", "python.exe");
            if (File.Exists(tfenv)) return tfenv;

            // 2. Try well-known command names on PATH
            foreach (string cmd in new[] { "python", "python3", "py" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p.WaitForExit(3000);
                    if (p.ExitCode == 0) return cmd;
                }
                catch { }
            }

            // 3. Check common Windows installation paths
            string[] commonPaths =
            {
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python39\python.exe",
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Python", "Python312", "python.exe"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Python", "Python311", "python.exe"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Python", "Python310", "python.exe"),
            };

            foreach (var path in commonPaths)
                if (File.Exists(path)) return path;

            return null;
        }
    }
}