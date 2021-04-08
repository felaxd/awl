using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace awl.Pages.Adm
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        [BindProperty]
        public string Config { get; set; }
        public string Dodano { get; set; }
        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            string login = HttpContext.Session.GetString("logged") ?? "0";
            if (login != "1")
                return RedirectToPage("../Login/Index");
            if (!System.IO.File.Exists(@"config.txt"))
            {
                _logger.LogError("Brak pliku konfiguracyjnego. Tworzenie...");
                System.IO.File.Create(@"config.txt");
            }
            else _logger.LogInformation($"Korzystanie z pliku konfiguracyjengo.");
            Config = System.IO.File.ReadAllText(@"config.txt");
            Dodano = HttpContext.Session.GetString("dodano") ?? "false";
            HttpContext.Session.SetString("dodano", "false");
            return Page();
        }
        public IActionResult OnPostUpdate(string config)
        {
            try
            {
                System.IO.File.WriteAllText(@"config.txt", config);
                HttpContext.Session.SetString("dodano", "true");
                _logger.LogInformation("Zapisano konfiguracje.");
            } catch { _logger.LogError("B³¹d zapisu konfiguracji."); }
            return RedirectToPage("Index");
        }
    }
}
