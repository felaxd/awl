using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace awl.Pages.Logout
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            HttpContext.Session.Remove("logged");
            return RedirectToPage("../Login/Index");
        }
    }
}
