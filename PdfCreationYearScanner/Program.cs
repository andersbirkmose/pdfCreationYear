using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CsvHelper;
using PdfPig;
using System.Text.RegularExpressions;

namespace PdfCreationYearScanner
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: PdfCreationYearScanner <input-file> [output-file]");
                Console.WriteLine("  input-file: Path to a text file containing PDF URLs (one per line)");
                Console.WriteLine("  output-file: (Optional) Path to the output CSV file. Default: output.csv");
                return;
            }

            string inputFilePath = args[0];
            string outputFilePath = args.Length > 1 ? args[1] : "output.csv";

            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine("Error: Input file not found.");
                return;
            }

            var urls = await File.ReadAllLinesAsync(inputFilePath);
            var results = new List<PdfMetadataResult>();

            using (var httpClient = new HttpClient())
            {
                int processed = 0;
                int failed = 0;

                foreach (var url in urls)
                {
                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    try
                    {
                        Console.WriteLine("Processing: " + url);
                        var creationYear = await ExtractCreationYearFromPdfAsync(httpClient, url);
                        results.Add(new PdfMetadataResult
                        {
                            Url = url,
                            CreationYear = creationYear
                        });
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed: " + url + " - " + ex.Message);
                        results.Add(new PdfMetadataResult
                        {
                            Url = url,
                            CreationYear = null
                        });
                        failed++;
                    }
                }

                Console.WriteLine("Processing complete. Success: " + processed + ", Failed: " + failed);
            }

            WriteToCsv(results, outputFilePath);
            Console.WriteLine("Results saved to: " + outputFilePath);
        }

        private static async Task<string?> ExtractCreationYearFromPdfAsync(HttpClient httpClient, string pdfUrl)
        {
            using (var response = await httpClient.GetAsync(pdfUrl))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var document = PdfDocument.Open(stream))
                    {
                        var creationDate = document.Information.CreationDate;
                        if (!string.IsNullOrEmpty(creationDate))
                        {
                            return ExtractYearFromDateString(creationDate);
                        }

                        var modifyDate = document.Information.ModifyDate;
                        if (!string.IsNullOrEmpty(modifyDate))
                        {
                            return ExtractYearFromDateString(modifyDate);
                        }

                        return null;
                    }
                }
            }
        }

        private static string? ExtractYearFromDateString(string dateString)
        {
            try
            {
                if (dateString.StartsWith("D:") && dateString.Length >= 6)
                {
                    return dateString.Substring(2, 4);
                }

                if (DateTime.TryParse(dateString, out var date))
                {
                    return date.Year.ToString();
                }

                if (DateTime.TryParseExact(dateString, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
                {
                    return exactDate.Year.ToString();
                }

                var yearMatch = Regex.Match(dateString, @"\d{4}");
                if (yearMatch.Success)
                {
                    return yearMatch.Value;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static void WriteToCsv(List<PdfMetadataResult> results, string outputPath)
        {
            using (var writer = new StreamWriter(outputPath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }
    }

    public class PdfMetadataResult
    {
        public string Url { get; set; } = string.Empty;
        public string? CreationYear { get; set; }
    }
}
