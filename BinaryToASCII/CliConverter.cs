using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public class CliConverter {
    private BinaryReader _binaryReader;
    private StreamWriter _asciiWriter;
    private double _unitsDivisor = 1.0; // To convert integer coordinates to real-world units

    // Helper methods for reading Big-Endian data
    private short ReadInt16BigEndian() {
        byte[] bytes = _binaryReader.ReadBytes(2);
        Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    private ushort ReadUInt16BigEndian() {
        byte[] bytes = _binaryReader.ReadBytes(2);
        Array.Reverse(bytes);
        return BitConverter.ToUInt16(bytes, 0);
    }

    private int ReadInt32BigEndian() {
        byte[] bytes = _binaryReader.ReadBytes(4);
        Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private float ReadSingleBigEndian() {
        byte[] bytes = _binaryReader.ReadBytes(4);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    public void ConvertToAscii(string inputFilePath, string outputFilePath) {
        List<string> headerLines = new List<string>();
        bool isBinaryFormat = false;
        bool headerEnded = false;

        // Ensure consistent number formatting (using '.' as decimal separator)
        CultureInfo invariantCulture = CultureInfo.InvariantCulture;

        try {
            // Phase 1: Read the header and determine format
            using (StreamReader headerReader = new StreamReader(inputFilePath)) {
                string line;
                while ((line = headerReader.ReadLine()) != null) {
                    if (line.StartsWith("$$UNITS/", StringComparison.OrdinalIgnoreCase)) {
                        string[] parts = line.Split('/');
                        if (parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Any, invariantCulture, out double val)) {
                            if (val != 0) _unitsDivisor = val;
                        }
                        headerLines.Add(line);
                    } else if (line.StartsWith("$$BINARY", StringComparison.OrdinalIgnoreCase)) {
                        isBinaryFormat = true;
                        // Replace $$BINARY with $$ASCII for the output header
                        headerLines.Add("$$ASCII");
                    } else if (line.StartsWith("$$HEADEREND", StringComparison.OrdinalIgnoreCase)) {
                        headerLines.Add(line);
                        headerEnded = true;
                        break; // Stop reading header
                    } else {
                        headerLines.Add(line);
                    }
                }
            }

            if (!isBinaryFormat) {
                Console.WriteLine("The input file does not appear to be in binary CLI format (missing $$BINARY in header).");
                // Optionally, you could just copy the file if it's already ASCII or has no $$BINARY tag.
                // For this converter, we'll stop.
                return;
            }

            if (!headerEnded) {
                Console.WriteLine("Could not find $$HEADEREND in the input file.");
                return;
            }

            // Phase 2: Process binary data and write ASCII output
            using (FileStream fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
            using (_binaryReader = new BinaryReader(fs))
            using (_asciiWriter = new StreamWriter(outputFilePath, false, Encoding.ASCII)) // Ensure ASCII encoding
            {
                // Write the collected (and potentially modified) header
                foreach (string headerLine in headerLines) {
                    _asciiWriter.WriteLine(headerLine);
                }

                // Optional: Common in ASCII CLI after header
                _asciiWriter.WriteLine("$$GEOMETRYSTART");

                // Position the binary reader to start of geometry data
                // This requires knowing the byte length of the header.
                // A simpler way is to re-open and seek, or read header bytes with BinaryReader too.
                // For now, let's get the position after headerReader was done.
                // Calculate header length in bytes
                long headerByteLength = 0;
                foreach (string hLine in headerLines) {
                    headerByteLength += Encoding.UTF8.GetByteCount(hLine) + Encoding.UTF8.GetByteCount(Environment.NewLine); // Approximation
                }
                // This calculation of headerByteLength is tricky because of ReadLine() behavior.
                // A more robust way:
                fs.Position = 0; // Reset stream position
                long currentPosition = 0;
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8, true, 1024, true)) // true to leave stream open
                {
                    string line;
                    while ((line = sr.ReadLine()) != null) {
                        currentPosition = fs.Position; // Position *after* CRLF or LF
                        if (line.StartsWith("$$HEADEREND")) break;
                    }
                }
                _binaryReader.BaseStream.Position = currentPosition;


                // Read and process binary commands
                while (_binaryReader.BaseStream.Position < _binaryReader.BaseStream.Length) {
                    byte commandId = _binaryReader.ReadByte(); // Assuming 1-byte Command ID

                    switch (commandId) {
                        case 127: // Start Layer long (Z is REAL)
                            {
                                float z = ReadSingleBigEndian();
                                _asciiWriter.WriteLine($"$$LAYER/{z.ToString("F6", invariantCulture)}");
                            }
                            break;

                        case 128: // Start Layer short (Z is unsigned INTEGER)
                            {
                                ushort zInt = ReadUInt16BigEndian();
                                double z = zInt / _unitsDivisor;
                                _asciiWriter.WriteLine($"$$LAYER/{z.ToString("F6", invariantCulture)}");
                            }
                            break;

                        case 129: // Start PolyLine short
                            ProcessPolyLine(isLongFormat: false, invariantCulture);
                            break;

                        case 130: // Start PolyLine long
                            ProcessPolyLine(isLongFormat: true, invariantCulture);
                            break;

                        case 131: // Start Hatches short
                            ProcessHatches(isLongFormat: false, invariantCulture);
                            break;

                        case 132: // Start Hatches long
                            ProcessHatches(isLongFormat: true, invariantCulture);
                            break;

                        default:
                            Console.WriteLine($"Unknown or unsupported binary command ID: {commandId} at position {_binaryReader.BaseStream.Position - 1}. Stopping.");
                            _asciiWriter.WriteLine($"$$COMMENT/Unknown binary command ID {commandId} encountered during conversion.");
                            goto endProcessing; // Exit loop
                    }
                }

            endProcessing:;
                _asciiWriter.WriteLine("$$GEOMETRYEND");
                // You might want to add $$PARTEND or other footers if they were in the original header/trailer.
            }

            Console.WriteLine($"Successfully converted '{inputFilePath}' to ASCII CLI format: '{outputFilePath}'");

        } catch (EndOfStreamException) {
            Console.WriteLine("Finished processing (reached end of stream).");
            // This is often normal when reading binary files command by command.
            // Ensure the ASCII writer also finalizes its output.
            if (_asciiWriter != null) {
                try {
                    _asciiWriter.WriteLine("$$GEOMETRYEND"); // Try to close geometry section
                    _asciiWriter.Flush();
                } catch { }
            }
            Console.WriteLine($"Successfully converted (or partially converted) '{inputFilePath}' to ASCII CLI format: '{outputFilePath}'");
        } catch (Exception ex) {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private void ProcessPolyLine(bool isLongFormat, CultureInfo culture) {
        ushort id, direction, numPoints;
        int id_long, direction_long, numPoints_long;

        StringBuilder sb = new StringBuilder();

        if (isLongFormat) {
            id_long = ReadInt32BigEndian();
            direction_long = ReadInt32BigEndian();
            numPoints_long = ReadInt32BigEndian();
            sb.Append($"$$POLYLINE/{id_long},{direction_long},{numPoints_long}");

            for (int i = 0; i < numPoints_long; i++) {
                float x = ReadSingleBigEndian();
                float y = ReadSingleBigEndian();
                sb.Append($",{x.ToString("F6", culture)},{y.ToString("F6", culture)}");
            }
        } else // Short format
          {
            id = ReadUInt16BigEndian();
            direction = ReadUInt16BigEndian();
            numPoints = ReadUInt16BigEndian();
            sb.Append($"$$POLYLINE/{id},{direction},{numPoints}");

            for (int i = 0; i < numPoints; i++) {
                double x = ReadUInt16BigEndian() / _unitsDivisor;
                double y = ReadUInt16BigEndian() / _unitsDivisor;
                sb.Append($",{x.ToString("F6", culture)},{y.ToString("F6", culture)}");
            }
        }
        _asciiWriter.WriteLine(sb.ToString());
    }

    private void ProcessHatches(bool isLongFormat, CultureInfo culture) {
        ushort id, numHatchLines;
        int id_long, numHatchLines_long;

        StringBuilder sb = new StringBuilder();

        if (isLongFormat) {
            id_long = ReadInt32BigEndian();
            numHatchLines_long = ReadInt32BigEndian(); // Assuming 'n' is num hatch lines
            sb.Append($"$$HATCHES/{id_long},{numHatchLines_long}");

            for (int i = 0; i < numHatchLines_long; i++) {
                float sx = ReadSingleBigEndian();
                float sy = ReadSingleBigEndian();
                float ex = ReadSingleBigEndian();
                float ey = ReadSingleBigEndian();
                sb.Append($",{sx.ToString("F6", culture)},{sy.ToString("F6", culture)},{ex.ToString("F6", culture)},{ey.ToString("F6", culture)}");
            }
        } else // Short format
          {
            id = ReadUInt16BigEndian();
            numHatchLines = ReadUInt16BigEndian();
            sb.Append($"$$HATCHES/{id},{numHatchLines}");

            for (int i = 0; i < numHatchLines; i++) {
                double sx = ReadUInt16BigEndian() / _unitsDivisor;
                double sy = ReadUInt16BigEndian() / _unitsDivisor;
                double ex = ReadUInt16BigEndian() / _unitsDivisor;
                double ey = ReadUInt16BigEndian() / _unitsDivisor;
                sb.Append($",{sx.ToString("F6", culture)},{sy.ToString("F6", culture)},{ex.ToString("F6", culture)},{ey.ToString("F6", culture)}");
            }
        }
        _asciiWriter.WriteLine(sb.ToString());
    }

    public static void Main(string[] args) {
        if (args.Length < 2) {
            Console.WriteLine("Usage: CliConverter <input_binary_cli_file> <output_ascii_cli_file>");
            return;
        }

        string inputFile = args[0];
        string outputFile = args[1];

        if (!File.Exists(inputFile)) {
            Console.WriteLine($"Error: Input file not found: {inputFile}");
            return;
        }

        CliConverter converter = new CliConverter();
        converter.ConvertToAscii(inputFile, outputFile);
    }
}