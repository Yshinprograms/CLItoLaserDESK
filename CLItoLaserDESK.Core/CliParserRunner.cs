// File: CLItoLaserDESK.Core\CliParserRunner.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CLItoLaserDESK.Core.Models; // Assuming your models are in this namespace

namespace CLItoLaserDESK.Core // Or CLItoLaserDESK.Core.Services
{
    public class CliParserRunner {
        private readonly string _colainExecutablePath;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public CliParserRunner(string colainExecutablePath) {
            _colainExecutablePath = colainExecutablePath ?? throw new ArgumentNullException(nameof(colainExecutablePath));
            if (!File.Exists(_colainExecutablePath)) {
                throw new FileNotFoundException($"colain_parser.exe executable not found at specified path: '{_colainExecutablePath}'. Please ensure the path is correct.", _colainExecutablePath);
            }

            _jsonSerializerOptions = new JsonSerializerOptions {
                // PropertyNameCaseInsensitive = true; // Use this if your C# properties are PascalCase and JSON keys are snake_case, AND you haven't used [JsonPropertyName]
                // If you used [JsonPropertyName] on your model properties, this isn't strictly necessary for those.
            };
        }

        // MODIFICATION 1: Change return type
        public async Task<ParserOutput> ParseCliFileAsync(string cliFilePath, bool isLongCli) {
            if (string.IsNullOrWhiteSpace(cliFilePath))
                throw new ArgumentNullException(nameof(cliFilePath));
            if (!File.Exists(cliFilePath))
                throw new FileNotFoundException($"CLI input file not found at path: '{cliFilePath}'.", cliFilePath);

            string cliTypeArg = isLongCli ? "long" : "short";

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = _colainExecutablePath,
                Arguments = $"\"{cliFilePath}\" {cliTypeArg}", // Quote file path in case it has spaces
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo }) {
                Console.WriteLine($"[CliParserRunner] Executing: \"{process.StartInfo.FileName}\" {process.StartInfo.Arguments}");
                process.Start();

                Task<string> outputJsonTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorOutputTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                string outputJson = await outputJsonTask;
                string errorOutput = await errorOutputTask;

                if (process.ExitCode != 0) {
                    Console.Error.WriteLine($"[CliParserRunner] colain_parser.exe Stderr: {errorOutput}");
                    throw new Exception($"colain_parser.exe failed with exit code {process.ExitCode}. See console for Stderr from the parser.");
                }

                if (string.IsNullOrWhiteSpace(outputJson)) {
                    Console.Error.WriteLine($"[CliParserRunner] colain_parser.exe Stderr (if any): {errorOutput}");
                    throw new Exception("colain_parser.exe produced no standard output, but exited successfully. This might indicate an issue with the parser or the input file not producing data.");
                }

                try {
                    ParsedCliFile? nullableParsedData = JsonSerializer.Deserialize<ParsedCliFile>(outputJson, _jsonSerializerOptions);

                    if (nullableParsedData == null) {
                        string partialJson = outputJson.Length > 500 ? outputJson.Substring(0, 500) + "..." : outputJson;
                        Console.Error.WriteLine($"[CliParserRunner] Deserialization resulted in a null object. JSON: {partialJson}");
                        throw new InvalidOperationException("JSON deserialization resulted in a null object. The JSON might represent 'null' or be malformed in a way that doesn't throw JsonException but yields null.");
                    }

                    ParsedCliFile parsedData = nullableParsedData;

                    Console.WriteLine("[CliParserRunner] Successfully deserialized JSON output.");
                    // MODIFICATION 2: Return ParserOutput containing both parsed data and raw JSON
                    return new ParserOutput(parsedData, outputJson);
                } catch (JsonException jsonEx) {
                    string partialJson = outputJson.Length > 1000 ? outputJson.Substring(0, 1000) + "..." : outputJson;
                    Console.Error.WriteLine($"[CliParserRunner] --- Problematic JSON Start (first 1000 chars) --- \n{partialJson}\n--- Problematic JSON End ---");
                    throw new Exception($"Failed to deserialize JSON output from colain_parser.exe: {jsonEx.Message}. Check the JSON structure and C# models.", jsonEx);
                }
            }
        }
    }
}