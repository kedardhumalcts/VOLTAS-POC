using System;

namespace InvoiceOCRApp2.Models
{
    public class Invoice
    {
        public int Id { get; set; } // Auto-increment
        public string ImagePath { get; set; }
        public string Value { get; set; }
        public decimal FinalAmount { get; set; } // Final Amount column
    }
}
