using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InvoiceOCRApp2.Data;
using InvoiceOCRApp2.Models;

public class SalesController : Controller
{
    private readonly ApplicationDbContext _context;

    public SalesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file != null && file.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Perform OCR processing
            var finalAmount = await GetAmountFromOCR(filePath);

            if (finalAmount > 0) // Process successful
            {
                // Save file information in database
                var invoice = new Invoice
                {
                    ImagePath = file.FileName,
                    Value = "Processed Value",
                    FinalAmount = finalAmount
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "File saved successfully!";
            }
            else
            {
                // Handle case when OCR fails
                return BadRequest("OCR failed to extract amount.");
            }
        }

        return RedirectToAction("Index");
    }

    private async Task<decimal> GetAmountFromOCR(string filePath)
    {
        var apiKey = "K87668844488957"; 
        var url = "https://api.ocr.space/parse/image";
        var client = new RestClient(url);
        var request = new RestRequest
        {
            Method = Method.Post
        };
        request.AddFile("file", filePath);
        request.AddParameter("apikey", apiKey);
        request.AddParameter("language", "eng");

        var response = await client.ExecuteAsync(request);

        if (string.IsNullOrEmpty(response.Content))
        {
            throw new Exception("OCR API returned an empty response.");
        }

        var result = JsonConvert.DeserializeObject<OCRResult>(response.Content);

        if (result == null || result.ParsedResults == null || result.ParsedResults.Length == 0 || string.IsNullOrEmpty(result.ParsedResults[0].ParsedText))
        {
            return 0;
        }

        var amount = ExtractTotalAmountFromWords(result.ParsedResults[0].ParsedText);
        return amount;
    }



    //original for voltas

    //private string ExtractAmountInWords(string text)
    //{
    //    // 'Total Invoice Amount in words' नंतरचे वाक्यांश काढण्यासाठी regex.
    //    var regex = new Regex(@"Total Invoice Amount in words[\s\S]*?([A-Za-z\s]+)", RegexOptions.IgnoreCase);
    //    var match = regex.Match(text);

    //    if (match.Success)
    //    {
    //        return match.Groups[1].Value.Trim();
    //    }

    //    return string.Empty;
    //}



    private string ExtractAmountInWords(string text)
    {
       
        var regex1 = new Regex(@"Total Invoice Amount in words[\s\S]*?([A-Za-z\s]+)", RegexOptions.IgnoreCase);
        var match1 = regex1.Match(text);

        
        if (match1.Success)
        {
            return match1.Groups[1].Value.Trim();
        }
        else
        {
           
            var regex2 = new Regex(@"Amount Chargeable \(in words\)[\s\S]*?INR\s([A-Za-z\s]+)", RegexOptions.IgnoreCase);
            var match2 = regex2.Match(text);

            if (match2.Success)
            {
                return match2.Groups[1].Value.Trim();
            }
        }

        return string.Empty;
    }


    // for Ram

    //private string ExtractAmountInWords(string text)
    //{
    //    // 'Total Invoice Amount in words' किंवा 'Amount Chargeable (in words)' नंतरचे वाक्यांश काढण्यासाठी regex.
    //    var regex = new Regex(@"(?:Total Invoice Amount in words|Amount Chargeable \(in words\))[\s\S]*?INR\s([A-Za-z\s]+)", RegexOptions.IgnoreCase);
    //    var match = regex.Match(text);

    //    if (match.Success)
    //    {
    //        return match.Groups[1].Value.Trim();
    //    }

    //    return string.Empty;
    //}




    public static decimal ConvertWordsToNumber(string words)
    {
        var wordsToNumbers = new Dictionary<string, decimal>
        {
            { "One", 1 }, { "Two", 2 }, { "Three", 3 }, { "Four", 4 }, { "Five", 5 },
            { "Six", 6 }, { "Seven", 7 }, { "Eight", 8 }, { "Nine", 9 }, { "Ten", 10 },
            { "Eleven", 11 }, { "Twelve", 12 }, { "Thirteen", 13 }, { "Fourteen", 14 }, { "Fifteen", 15 },
            { "Sixteen", 16 }, { "Seventeen", 17 }, { "Eighteen", 18 }, { "Nineteen", 19 },
            { "Twenty", 20 }, { "Thirty", 30 }, { "Forty", 40 }, { "Fifty", 50 }, { "Sixty", 60 },
            { "Seventy", 70 }, { "Eighty", 80 }, { "Ninety", 90 }, { "Hundred", 100 },
            { "Thousand", 1000 }, { "Lakh", 100000 }, { "Crore", 10000000 }
        };

        decimal total = 0;
        decimal currentNumber = 0;

        var wordsList = words.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in wordsList)
        {
            if (wordsToNumbers.TryGetValue(word, out var value))
            {
                if (value == 100)
                {
                    currentNumber *= value;
                }
                else if (value == 1000 || value == 100000 || value == 10000000)
                {
                    total += currentNumber * value;
                    currentNumber = 0;
                }
                else
                {
                    currentNumber += value;
                }
            }
        }

        total += currentNumber;
        return total;
    }

    private decimal ExtractTotalAmountFromWords(string text)
    {
        var amountInWords = ExtractAmountInWords(text);
        if (!string.IsNullOrEmpty(amountInWords))
        {
            return ConvertWordsToNumber(amountInWords);
        }
        return 0;
    }
}
