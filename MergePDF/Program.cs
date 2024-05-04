using PdfSharp.Pdf.IO;
using PdfSharp.Pdf;

namespace MergePDF
{
    internal class Program
    {

        static void Main(string[] args)
        {

            var pdfFiles = GetPdfs();
            string output = "final.pdf";
            try
            {
                using (var pdfDocument = new PdfDocument())
                {
                    foreach (string pdfFile in pdfFiles)
                    {
                        try
                        {
                            using (var fs = File.OpenRead(pdfFile))
                            {
                                // Perform operations on the valid file stream
                                Console.WriteLine("File opened successfully!");
                                Console.WriteLine("Merging {0}...", pdfFile);
                                /* 
                                 Determines whether the specified stream is a PDF file by inspecting the first eight
                                 bytes of the data. 
                                 If the data begins with PDF-x.y the function returns the version
                                 number as integer (e.g. 17 for PDF 1.7).
                                 If the data is invalid or inaccessible
                                 for any reason, 0 is returned. 
                                 The function never throws an exception.
                                */
                                int isOk = PdfReader.TestPdfFile(fs);
                                Console.WriteLine("Is PDF OK? {0}", isOk == 0 ? "No" : "Yes");
                                Console.WriteLine("PDF Version: {0}", Convert.ToDouble(isOk * 0.1).ToString("0.#"));
                                PdfDocument inputPDFDocument = PdfReader.Open(fs, PdfDocumentOpenMode.Import);
                                pdfDocument.Version = inputPDFDocument.Version;
                                foreach (PdfPage page in inputPDFDocument.Pages)
                                {
                                    pdfDocument.AddPage(page);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error opening file: {ex.Message}");
                        }


                    }
                    using (var ms = new MemoryStream())
                    {
                        pdfDocument.Save(ms);
                        ms.Position = 0;
                        File.WriteAllBytes(output, ms.ToArray());
                    }
                    Console.WriteLine("PDFs merged successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error merging PDFs: " + ex.Message);
            }
        }

        private static List<string> GetPdfs()
        {
            var cwd = Directory.GetCurrentDirectory();
            var pdfs = Directory.GetFiles(cwd, "*.pdf");
            return [.. pdfs];
        }
    }
}
