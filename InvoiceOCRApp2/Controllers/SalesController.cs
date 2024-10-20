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

            if (finalAmount > 0)
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

                
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                TempData["SuccessMessage"] = "File saved successfully!";
            }
            else
            {
                
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







    private string ExtractAmountInWordsOrNumber(string text)
    {
        
        var regex1 = new Regex(@"Total Invoice Amount in words[\s\S]*?([A-Za-z\s]+)", RegexOptions.IgnoreCase);
        var match1 = regex1.Match(text);

        if (match1.Success)
        {
            return match1.Groups[1].Value.Trim();
        }

        
        var regex2 = new Regex(@"Amount Chargeable \(in words\)[\s\S]*?INR\s([A-Za-z\s]+)", RegexOptions.IgnoreCase);
        var match2 = regex2.Match(text);

        if (match2.Success)
        {
            return match2.Groups[1].Value.Trim();
        }

        
        var regex3 = new Regex(@"Rupees\.\r\n•\s([A-Za-z\s]+(?:THOUSAND|LAKH|MILLION|CRORE)[\sA-Za-z]+)", RegexOptions.IgnoreCase);
        var match3 = regex3.Match(text);

        if (match3.Success)
        {
            return match3.Groups[1].Value.Trim();
        }

        
        var regex4 = new Regex(@"(Rupees\s[A-Za-z\s\-]+\sOnly)", RegexOptions.IgnoreCase);
        var match4 = regex4.Match(text);

        if (match4.Success)
        {
            return match4.Groups[1].Value.Trim(); 
        }

        
        var regex5 = new Regex(@"Invoice\sAmount\sIn\sWords\s*[\r\n]+([A-Za-z\s]+Rupees\s?only)", RegexOptions.IgnoreCase);
        var match5 = regex5.Match(text);

        if (match5.Success)
        {
            return match5.Groups[1].Value.Trim(); 
        }

        
        var regex6 = new Regex(@"Total:\s*Rupee'?[\s]*([A-Za-z\s]+Only)", RegexOptions.IgnoreCase);
        var match6 = regex6.Match(text);

        if (match6.Success)
        {
            return match6.Groups[1].Value.Trim();  
        }

        
        var regex7 = new Regex(@"Rupees\s?([A-Za-z\s]+)\sOnly", RegexOptions.IgnoreCase);
        var match7 = regex7.Match(text);

        if (match7.Success)
        {
            return match7.Groups[1].Value.Trim(); 
        }

        
        var regex9 = new Regex(@"(Amoun[ti]\s)?Chargeable\sOn\swords\)\s*INR\s([A-Za-z\s]+)\sOnly", RegexOptions.IgnoreCase);
        var match9 = regex9.Match(text);

        if (match9.Success)
        {
            return match9.Groups[2].Value.Trim();  
        }

        var regex10 = new Regex(@"Amount\sCharge(?:able|a51e)\s\(in\swords\)\s?(?:\r\\n|\s)*INR\s([A-Za-z\s]+)\sOnly", RegexOptions.IgnoreCase);
        var match10 = regex10.Match(text);

        if (match10.Success)
        {
            return match10.Groups[1].Value.Trim();  
        }

        var regex11 = new Regex(@"INR\s([A-Za-z\s]+Only)", RegexOptions.IgnoreCase);
        var match11 = regex11.Match(text);

        if (match11.Success)
        {
            return match11.Groups[1].Value.Trim();  
        }

        var regex12 = new Regex(@"Rupes\s([A-Za-z\s]+Only)", RegexOptions.IgnoreCase);
        var match12 = regex12.Match(text);

        if (match12.Success)
        {
            return match12.Groups[1].Value.Trim();  
        }





       
        var numericRegex = new Regex(@"TOTAL:\s*[\s\S]*?INR\s?([\d,]+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        var numericMatch = numericRegex.Match(text);

        if (numericMatch.Success)
        {
            return numericMatch.Groups[1].Value.Trim(); 
        }

        
        var numericRegex1 = new Regex(@"TOTAL\s*INR\s?([\d,]+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        var numericMatch1 = numericRegex1.Match(text);

        if (numericMatch1.Success)
        {
            return numericMatch1.Groups[1].Value.Trim();  
        }

        
        var numericRegex2 = new Regex(@"SWIPE\s-\s?([\d,]+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        var numericMatch2 = numericRegex2.Match(text);

        if (numericMatch2.Success)
        {
            return numericMatch2.Groups[1].Value.Trim();  
        }

        
        var numericRegex3 = new Regex(@"cash\s-\s?([\d,]+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        var numericMatch3 = numericRegex3.Match(text);

        if (numericMatch3.Success)
        {
            return numericMatch3.Groups[1].Value.Trim();  
        }

        var numericRegex4 = new Regex(@"INR\s([0-9]{5,6}\.[0-9]{2})", RegexOptions.IgnoreCase);
        var numericMatch4 = numericRegex4.Match(text);

        if (numericMatch4.Success)
        {
            return numericMatch4.Groups[1].Value.Trim();  
        }




       
        //var numericRegex2 = new Regex(@"TOTAL\s*[\r\n]*.*?([\d,]+(?:\.\d{2})?)\s*[\r\n]+", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        //var numericMatch2 = numericRegex2.Match(text);

        //if (numericMatch2.Success)
        //{
        //    return numericMatch2.Groups[1].Value.Trim();  // Return the numeric value (e.g., "18700")
        //}



        
        var regex8 = new Regex(@"Y\s([A-Za-z\s]+)\sOnly", RegexOptions.IgnoreCase);
        var match8 = regex8.Match(text);

        if (match8.Success)
        {
            return match8.Groups[1].Value.Trim();  
        }

        return string.Empty; 
    }





    public static decimal ConvertWordsToNumber(string words)
    {
        var wordsToNumbers = new Dictionary<string, decimal>
    {
        { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
        { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 },
        { "eleven", 11 }, { "twelve", 12 }, { "thirteen", 13 }, { "fourteen", 14 }, { "fifteen", 15 },
        { "sixteen", 16 }, { "seventeen", 17 }, { "eighteen", 18 }, { "nineteen", 19 },
        { "twenty", 20 }, { "thirty", 30 }, { "forty", 40 }, { "fifty", 50 }, { "sixty", 60 },
        { "seventy", 70 }, { "eighty", 80 }, { "ninety", 90 }, { "hundred", 100 },
        { "thousand", 1000 }, { "lakh", 100000 }, { "crore", 10000000 }
    };

        decimal total = 0;
        decimal currentNumber = 0;

        var wordsList = words.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in wordsList)
        {
            var lowerWord = word.ToLower();  

            if (wordsToNumbers.TryGetValue(lowerWord, out var value))
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
        var amountInWordsOrNumber = ExtractAmountInWordsOrNumber(text);
        if (!string.IsNullOrEmpty(amountInWordsOrNumber))
        {
           
            if (decimal.TryParse(amountInWordsOrNumber, out var numericAmount))
            {
                return numericAmount; 
            }
            else
            {
                return ConvertWordsToNumber(amountInWordsOrNumber); 
            }
        }
        return 0;
    }
}