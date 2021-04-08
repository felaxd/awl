using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace awl.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string guid { get; set; }
        public List<Error> Errors_list { get; set; } = new List<Error>();
        public int Errors { get; set; }

        private readonly ILogger<ErrorModel> _logger;

        public string Input { get; set; }
        public string Type { get; set; }
        public ErrorModel(ILogger<ErrorModel> logger)
        {
            _logger = logger;
            if (!System.IO.File.Exists("config.txt"))
            {
                Console.WriteLine("Brak pliku konfiguracyjnego.");
                return;
            }
            foreach (string item in System.IO.File.ReadAllLines(@"config.txt"))
            {
                if (!item.Contains("=")) continue;
                string[] line = item.Split("=");
                config.Add(line[0], line[1]);
            }
        }
        Dictionary<string, string> config = new Dictionary<string, string>();

        public IActionResult OnGet(string guid)
        {
            string login = HttpContext.Session.GetString("logged") ?? "0";
            if (login != "1")
                return RedirectToPage("/Login/Index");
            Errors_list = new List<Error>();
            Dodano = HttpContext.Session.GetString("dodano") ?? "";
            HttpContext.Session.SetString("dodano", "");
            MySqlConnection conn = new MySqlConnection("server=" + config.GetValueOrDefault("server") + ";port=" + config.GetValueOrDefault("port") + ";database=" + config.GetValueOrDefault("database_err") + ";uid=" + config.GetValueOrDefault("login") + ";pwd=" + config.GetValueOrDefault("password"));
            conn.Open();
            MySqlCommand staryWynik = new MySqlCommand("SELECT * FROM `err` WHERE `sess_id`='" + MySqlHelper.EscapeString(guid) + "';", conn);
            //staryWynik.Parameters.AddWithValue("@guid", guid);
            MySqlDataReader wynik = staryWynik.ExecuteReader();
            while (wynik.Read())
            {
                Error er = new Error();
                er.addError(wynik.GetString(2), wynik.GetString(3), wynik.GetString(4), wynik.GetString(6), wynik.GetString(5)); // 6 = 5, 5 = 6
                Errors_list.Add(er);
            }
            Errors = Errors_list.Count();
            wynik.Close();
            conn.Close();
            return Page();
        }
        public string Dodano { get; set; }
        public IActionResult OnPost(string input, string type) {
            input = input.Trim();
            MySqlConnection conn_in = new MySqlConnection("server=" + config.GetValueOrDefault("server") + ";port=" + config.GetValueOrDefault("port") + ";database=" + config.GetValueOrDefault("database") + ";uid=" + config.GetValueOrDefault("login") + ";pwd=" + config.GetValueOrDefault("password"));
            conn_in.Open();
            String Query;
            if (type == "prowadzacy") Query = "SELECT COUNT(*) FROM `prowadzacy` WHERE `name`='" + MySqlHelper.EscapeString(input) + "';"; else if (type == "grupa") Query = "SELECT COUNT(*) FROM `grupy` WHERE `name`='" + MySqlHelper.EscapeString(input) + "';"; else { return RedirectToPage("", guid = guid); }
            MySqlCommand cmd_sel = new MySqlCommand(Query, conn_in);
            //cmd_sel.Parameters.AddWithValue("@name", input);
            var result = Convert.ToInt32(cmd_sel.ExecuteScalar());
            if (result > 0) { HttpContext.Session.SetString("dodano", "" + input + " ju¿ znajduje siê w bazie danych."); return RedirectToPage("", guid = guid); }
            if(type == "prowadzacy") Query = "INSERT INTO `prowadzacy` (`name`) VALUES (@name);"; else if(type == "grupa") Query = "INSERT INTO `grupy` (`name`) VALUES (@name);"; else { HttpContext.Session.SetString("dodano", "B³¹d podczas zapisu."); return RedirectToPage("", guid = guid); }
            MySqlCommand cmd = new MySqlCommand(Query, conn_in);
            cmd.Parameters.AddWithValue("@name", input);
            if (cmd.ExecuteNonQuery() != -1) { Console.WriteLine("Dodano " + input + " do bazy."); HttpContext.Session.SetString("dodano", "Dodano " + input + " do bazy."); }
            MySqlConnection conn = new MySqlConnection("server=" + config.GetValueOrDefault("server") + ";port=" + config.GetValueOrDefault("port") + ";database=" + config.GetValueOrDefault("database_err") + ";uid=" + config.GetValueOrDefault("login") + ";pwd=" + config.GetValueOrDefault("password"));
            conn_in.Close();
            conn.Open();
            cmd = new MySqlCommand("DELETE FROM `err` WHERE `name`='" + MySqlHelper.EscapeString(input) + "';", conn);
            cmd.ExecuteNonQuery();
            conn.Close();
            return RedirectToPage("", guid = guid);
        }
    }
}
