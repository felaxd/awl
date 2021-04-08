using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace awl.Pages.Login
{
    public class IndexModel : PageModel
    {
        public IndexModel()
        {
            Err_login = 0;
            
        }
        public string Login { get; set; }
        public string Password { get; set; }
        public int Err_login { get; set; }
        public IActionResult OnGet()
        {
            //HttpContext.Session.SetString("logged", "0");
            string login = HttpContext.Session.GetString("logged") ?? "0";
            if (login == "1")
                return RedirectToPage("../Index");
            string x = HttpContext.Session.GetString("err_login") ?? "0";
            if (x == "1")
                Err_login = 1;
            HttpContext.Session.SetString("err_login", "false");
            return Page();
        }

        public IActionResult OnPost(string login, string password)
        {
            if (login == "admin" && password == "admin") { 
                HttpContext.Session.SetString("logged", "1");
                return RedirectToPage("../Index");
            } else { 
                HttpContext.Session.SetString("logged", "0");
                HttpContext.Session.SetString("err_login", "1");
                return RedirectToPage("");
            }
        }
    }
}
