using System.Diagnostics;
using System.IO;

namespace ExperimentPredictorApp
{
    public static class ModelHandler
    {
        private static string pythonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python_scripts", "tfenv", "Scripts", "python.exe");

        public static string RunModel(string modelName, string trainingCsv, string predictInput)
        {
            string scriptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python_scripts");
            string scriptPath = Path.Combine(scriptsDir, $"predict_{modelName.ToLower()}.py");
            string tempTrainFile = Path.Combine(scriptsDir, "temp_training.csv");

            File.WriteAllText(tempTrainFile, trainingCsv);

            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" \"{Path.GetFileName(tempTrainFile)}\" \"{predictInput}\"",
                WorkingDirectory = scriptsDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return "Hata:\n" + errors;
                }

                return output.Trim();
            }

        }
    }
}
