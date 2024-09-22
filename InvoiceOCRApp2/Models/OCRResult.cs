using System;

namespace InvoiceOCRApp2.Models
{
    public class OCRResult
    {
        public ParsedResults[] ParsedResults { get; set; }
    }

    public class ParsedResults
    {
        public string ParsedText { get; set; }
    }
}
