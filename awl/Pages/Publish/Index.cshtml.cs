using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace awl.Pages.Publish
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IHostEnvironment _environment;
        [BindProperty]
        public IFormFile UploadedFile { get; set; }
        public bool IsUploaded { get; set; }
        public string Nazwa_pliku { get; set; }
        public string File_guid { get; set; }
        public string File_name { get; set; }
        public bool Connected { get; set; }
        public string Selected_sheet { get; set; }
        public List<SelectListItem> worksheets = new List<SelectListItem>();
        readonly Database database;
        readonly List<string> Modules;

        public string Dodano { get; set; }
        public int Saved { get; set; }
        public int Removed { get; set; }
        public int Updated { get; set; }

        public IndexModel(ILogger<IndexModel> logger, IHostEnvironment environment)
        {
            Connected = true;
            _logger = logger;
            _environment = environment;
            IsUploaded = false;
        
            if (!System.IO.File.Exists(@"config.txt"))
            {
                _logger.LogError("Brak pliku konfiguracyjnego.");
                return;
            }
            else _logger.LogInformation($"Korzystanie z pliku konfiguracyjengo.");
            foreach (string item in System.IO.File.ReadAllLines(@"config.txt"))
            {
                if (!item.Contains("=")) continue;
                string[] line = item.Split("=");
                config.Add(line[0], line[1]);
            }
            try
            {
                try
                {
                    database = new Database(config.GetValueOrDefault("server"), config.GetValueOrDefault("database"), config.GetValueOrDefault("login"), config.GetValueOrDefault("password"));
                    Modules = new List<string>(database.GetSQLElements("przedmioty"));
                }
                catch
                {
                    Console.WriteLine("B³¹d po³¹czenia z baz¹ danych.");
                    return;
                }
            }
            catch (MySqlException e)
            {
                Console.WriteLine();
                Console.WriteLine(e);
                Console.WriteLine();
                Connected = false;
                Redirect("Index");
            }  
        }
        public IActionResult OnGet()
        {
            string login = HttpContext.Session.GetString("logged") ?? "0";
            if (login != "1")
                return RedirectToRoute("../Login/Index");
            Dodano = HttpContext.Session.GetString("dodano") ?? "false";
            Saved = HttpContext.Session.GetInt32("Saved").GetValueOrDefault();
            Updated = HttpContext.Session.GetInt32("Updated").GetValueOrDefault();
            Removed = HttpContext.Session.GetInt32("Removed").GetValueOrDefault();
            return Page();
        }

        readonly Dictionary<string, string> config = new Dictionary<string, string>();
        public async Task OnPostAsync()
        {
            if (UploadedFile == null || UploadedFile.Length == 0)
            {
                return;
            }

            string ext = UploadedFile.FileName.Split('.').Last();

            _logger.LogInformation($"Zapisywanie {UploadedFile.FileName}.");
            File_guid = Guid.NewGuid().ToString();
            File_name = $"{File_guid}.{ext}";
            string targetFileName = $"{_environment.ContentRootPath}/wwwroot/TempFiles/{File_name}";

            using (var stream = new FileStream(targetFileName, FileMode.Create))
            {
                await UploadedFile.CopyToAsync(stream);
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            FileInfo existingFile = new FileInfo(targetFileName);
            using (ExcelPackage excel = new ExcelPackage(existingFile))
            {
                ExcelWorksheets ws = excel.Workbook.Worksheets;
                worksheets.Clear();
                foreach (ExcelWorksheet sheet in ws)
                {
                    worksheets.Add(new SelectListItem
                    {
                        Text = sheet.Name,
                        Value = sheet.Name
                    });
                }
            }
            HttpContext.Session.SetString("nazwa_pliku", UploadedFile.FileName);
            Nazwa_pliku = UploadedFile.FileName;
            IsUploaded = true;
            _logger.LogInformation($"Zapisano jako {File_name}.");
            HttpContext.Session.SetString("dodano", "false");
        }

        public IActionResult OnPostSpr(string selected_sheet, string file_name)
        {
            
            string file = $"{_environment.ContentRootPath}/wwwroot/TempFiles/{file_name}";
            Excel_Database excel;
            try
            {
                excel = new Excel_Database(file, selected_sheet, Modules);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine("B³¹d odczytu pliku xlsx");
                Console.WriteLine(e.Message);
                return RedirectToPage("Index");
            }
            Console.WriteLine("Arkusz: " + selected_sheet);
            Console.WriteLine("Rok: " + excel.year);
            Console.WriteLine("Miesi¹c: " + excel.month);
            Console.WriteLine("Starting point: { " + excel.starting_point[0] + ", " + excel.starting_point[1] + " }");
            Console.WriteLine("Ending point: { " + excel.ending_point[0] + ", " + excel.ending_point[1] + " }");
            System.IO.File.Delete(file);

            List<string> code_database = new List<string>(database.GetSQLElements("zajecia", "code", "WHERE `code` LIKE '" + excel.year + excel.month + "%'"));
            List<string> code_remove = new List<string>();

            var firstDayOfMonth = new DateTime(Convert.ToInt32(excel.year), Convert.ToInt32(excel.month), 1);
            for (int dzien = 0; dzien < 31; dzien++)
            {
                string data = firstDayOfMonth.AddDays(dzien).ToString("d");
                string dzien_sql = firstDayOfMonth.AddDays(dzien).ToString("yyyy/MM/dd");
                excel.starting_point = excel.SeekPoint(data, exact: true);
                if (excel.starting_point[0] < 1 || excel.starting_point[1] < 5)
                {
                    Console.WriteLine("Nie znaleziono daty " + data + " w pliku.");
                    continue;
                }
                Console.WriteLine("\t\t\t\t\t" + data);
                excel.starting_point[0] = excel.starting_point[0] - 1;
                excel.starting_point[1] = excel.starting_point[1] - 5;
                excel.GetGroups();
                for (int godziny = 1; godziny <= 15; godziny++)
                {
                    Console.WriteLine("Godzina: " + godziny);
                    for (int wiersz = excel.starting_point[0] + 4; wiersz <= excel.ending_point[0]; wiersz++)
                    {
                        if (excel.rows_excluded.Contains(wiersz)) continue;
                        string[] range = excel.GetModuleInfo(wiersz, excel.starting_point[1] + godziny);
                        if (range.Length == 2)
                        {
                            wiersz = Convert.ToInt32(range[1]);
                            string code = Convert.ToString(dzien_sql.Replace(".", "") + "/" + (Convert.ToInt32(range[0]) - 4 - excel.rows_to_start) + "/" + godziny);
                            code_remove.Add(code);
                            continue;
                        }
                        Console.WriteLine();
                        Console.WriteLine(string.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n{6}\n{7}", range[0], range[1], range[2], range[3], range[4], range[5], range[6], range[7]));
                        string[] info = { Convert.ToString(dzien_sql.Replace(".", "") + "/" + (Convert.ToInt32(range[8]) - 4 - excel.rows_to_start) + "/" + godziny), dzien_sql, range[0], range[1], range[2], range[3], godziny.ToString(), range[4], range[5], range[7] };
                        //                                                                                                                  code                data        name       info   lecturer   room       start_hour       lenght    groups       type
                        database.addToDatabase(info, "zajecia");
                        wiersz = Convert.ToInt32(range[8]);
                        //Console.ReadKey();
                    }
                }
            }
            database.ExecuteInsertQuery();
            foreach (string code in code_remove) if (code_database.Contains(code)) database.removeFromDatabase(code, "zajecia");
            database.close();
            HttpContext.Session.SetString("dodano", "true");
            HttpContext.Session.SetInt32("Saved", database.inserted);
            HttpContext.Session.SetInt32("Updated", database.updated);
            HttpContext.Session.SetInt32("Removed", database.removed);
            Console.WriteLine("\nDodano: " + database.inserted);
            Console.WriteLine("\nZmieniono: " + database.updated);
            Console.WriteLine("\nUsuniêto: " + database.removed);
            return RedirectToPage("Index");
        }
    }
}
