// File: CLItoLaserDESK.Core\Models\ParserOutput.cs
namespace CLItoLaserDESK.Core.Models {
    /// <summary>
    /// Holds the results from the CLI parser, including both the
    /// deserialized C# object model and the raw JSON string output.
    /// </summary>
    public class ParserOutput {
        public ParsedCliFile ParsedData { get; }
        public string RawJsonOutput { get; }

        public ParserOutput(ParsedCliFile parsedData, string rawJsonOutput) {
            ParsedData = parsedData ?? throw new ArgumentNullException(nameof(parsedData));
            RawJsonOutput = rawJsonOutput ?? throw new ArgumentNullException(nameof(rawJsonOutput));
        }
    }
}