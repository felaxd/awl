using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace awl.Pages
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

        public IndexModel(ILogger<IndexModel> logger, IHostEnvironment environment)
        {
            Connected = true;
            _logger = logger;
            _environment = environment;
            IsUploaded = false;

            if (!System.IO.File.Exists("config.txt"))
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
                string conn_str = "server=" + config.GetValueOrDefault("server") + ";port=" + config.GetValueOrDefault("port") + ";database=" + config.GetValueOrDefault("database") + ";uid=" + config.GetValueOrDefault("login") + ";pwd=" + config.GetValueOrDefault("password");
                conn = new MySqlConnection(conn_str);
                string conn_str_in = "server=" + config.GetValueOrDefault("server") + ";port=" + config.GetValueOrDefault("port") + ";database=" + config.GetValueOrDefault("database_err") + ";uid=" + config.GetValueOrDefault("login") + ";pwd=" + config.GetValueOrDefault("password");
                conn_in = new MySqlConnection(conn_str_in);
                //conn = new MySqlConnection("server=localhost;port=3306;database=plan;uid=root;pwd=pass5431");
                //conn_in = new MySqlConnection("server=localhost;port=3306;database=err_tmp;uid=root;pwd=pass5431");
                conn.Open();
                conn_in.Open();
                //foreach (string item in GetSQLElements("przedmioty")) if (item != "") Modules.Add(item);
                foreach (string item in GetSQLElements("grupy")) if (item != "") Groups.Add(item);
                foreach (string item in GetSQLElements("prowadzacy")) if (item != "") Lecturer.Add(item);
                //foreach (string item in GetSQLElements("sale")) if (item != "") Rooms.Add(item);
                conn.Close();
            }
            catch(MySqlException e)
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
                return RedirectToPage("/Login/Index");
            return Page();
        }

        private string[] GetSQLElements(string table)
        {
            List<string> string_arr = new List<string>();
            MySqlCommand staryWynik = new MySqlCommand("SELECT name FROM " + table + " ORDER BY name ASC;", conn);
            MySqlDataReader wynik = staryWynik.ExecuteReader();
            while (wynik.Read())
            {
                string_arr.Add(wynik.GetString(0));
            }
            wynik.Close();
            return string_arr.ToArray();
        }

        readonly MySqlConnection conn;
        MySqlConnection conn_in;

        readonly List<string> Groups = new List<string>();
        readonly List<string> Lecturer = new List<string>();
        readonly List<string> Rooms = new List<string>();
        readonly List<string> Modules = new List<string>();

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
            string targetFileName = $"{_environment.ContentRootPath}/wwwroot/TempFiles/{File_guid}.{ext}";

            using (var stream = new FileStream(targetFileName, FileMode.Create))
            {
                await UploadedFile.CopyToAsync(stream);
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            FileInfo existingFile = new FileInfo(targetFileName);
            using (ExcelPackage excel = new ExcelPackage(existingFile)) {
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
        }

        private static string PL_Znaki(string str)
        {
            string pl = "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ";
            string en = "acelnoszzACELNOSZZ";
            string ret = "";
            foreach (char znak in str)
            {
                char znak1 = znak;
                if (pl.Contains(znak)) znak1 = en[pl.IndexOf(znak)];
                ret += znak1;
            }
            return ret;
        }
        private static string[] FindMatches(string str, List<string> list)
        {
            if (str == "") return null;
            List<string> list1 = new List<string>(list.Where(n => n[0] == str[0]));
            List<string> help_list = new List<string>();
            for (int i = 2; i < str.Length; i++)
            {
                string help_str = str.Replace(" ", "").Substring(0, i);
                list1 = new List<string>(list1.Where(n => n.Length >= i && PL_Znaki(n).Replace(" ", "").ToUpper().Substring(0, i) == PL_Znaki(help_str).ToUpper()));
                if (list1.Count == 0) break; else help_list.Clear();
                foreach (string item in list1)
                {
                    if (!help_list.Contains(item)) help_list.Add(item);
                }
                if (list1.Count <= 3) { help_list.Reverse(); break; }
            }
            help_list.Reverse();
            if (help_list.Count == 0) return null;
            return help_list.ToArray();
        }
        public List<Error> Errors_list { get; set; } = new List<Error>();
        List<string> insert_str = new List<string>();
        void Add_err(string file_guid, KeyValuePair<string, List<string>> err)
        {
            string[] matches = null;
            if (err.Value[1] == "prowadzacy") matches = FindMatches(err.Value[0], Lecturer);
            if (err.Value[1] == "przedmiot") matches = FindMatches(err.Value[0], Modules);
            if (err.Value[1] == "sala") matches = FindMatches(err.Value[0], Rooms);
            if (err.Value[1] == "grupa") matches = FindMatches(err.Value[0], Groups);
            string help = "";
            if (err.Value[0].Contains("  ")) help += " (Zawiera podwójną spację)";
            if (err.Value[0].Contains("\\")) help += " (Zawiera niedozwolony znak \\ )";
            if (matches == null) matches = new string[] { "Brak podpowiedzi." };
            insert_str.Add(string.Format("('{0}','{1}','{2}','{3}','{4}','{5}')", MySqlHelper.EscapeString(file_guid), MySqlHelper.EscapeString(err.Key), MySqlHelper.EscapeString(err.Value[0]), MySqlHelper.EscapeString(err.Value[1]), MySqlHelper.EscapeString(string.Join(",", matches)), MySqlHelper.EscapeString(help)));
            if (err.Value[1] == "data") insert_str.Add(string.Format("('{0}','{1}','{2}','{3}','{4}','{5}')", MySqlHelper.EscapeString(file_guid), MySqlHelper.EscapeString(err.Key), MySqlHelper.EscapeString(err.Value[0]), MySqlHelper.EscapeString(err.Value[1]), "", ""));
            if (insert_str.Count >= 200) insert_db();
        }

        void insert_db()
        {
            StringBuilder cmdString = new StringBuilder("INSERT INTO `err` (`sess_id`, `cell`, `name`, `type`, `samples`, `help`) VALUES ");
            cmdString.Append(string.Join(',', insert_str));
            cmdString.Append(";");
            MySqlCommand cmd = new MySqlCommand(cmdString.ToString(), conn_in);
            cmd.ExecuteNonQuery();
            insert_str.Clear();
        }

        public int Status { get; set; }
        public async Task<IActionResult> OnPostSpr(string selected_sheet, string file_guid, string file_name)
        {
            var progress = new Progress<int>(value => {
                    Status = value;
                });
            await Task.Run(() =>
                    {
                        excel(selected_sheet, file_guid, file_name, progress);
                    });
            return RedirectToPage("Error", new { guid = file_guid });
        }
        public void excel(string selected_sheet, string file_guid, string file_name, IProgress<int> progress) {
            _logger.LogInformation($"Sprawdzanie {HttpContext.Session.GetString("nazwa_pliku")} - arkusz {selected_sheet}");
            string worksheet = selected_sheet;
            string targetPath = $"{_environment.ContentRootPath}/wwwroot/TempFiles/{file_name}";
            string conn_str_in = "server=" + config.GetValueOrDefault("server") + ";port=" + config.GetValueOrDefault("port") + ";database=" + config.GetValueOrDefault("database_err") + ";uid=" + config.GetValueOrDefault("login") + ";pwd=" + config.GetValueOrDefault("password");
            conn_in = new MySqlConnection(conn_str_in);
            using (Excel excel = new Excel(targetPath, worksheet))
            {
                conn_in.Open();
                if (excel.year == "0" || excel.month == "0")
                {
                    List<string> er = new List<string> { "Data", "data" };
                    Dictionary<string, List<string>> errors = new Dictionary<string, List<string>> { { "ARKUSZ", er } };
                    foreach (KeyValuePair<string, List<string>> err in errors)
                    {
                        Add_err(file_guid, err);
                        excel.errors.Remove(err.Key);
                        break;
                    }
                    insert_db();
                    conn_in.Close();
                    excel.Dispose();
                    try
                    {
                        System.IO.File.Delete(targetPath);
                    } catch { _logger.LogInformation($"Błąd usuwania pliku."); }
                    Dispose();
                    return;
                    //return;
                }
                excel.GroupsStart();
                var firstDayOfMonth = new DateTime(Convert.ToInt32(excel.year), Convert.ToInt32(excel.month), 1);
                for (int dzien = 0; dzien < 31; dzien++)
                {
                    string data = firstDayOfMonth.AddDays(dzien).ToString("d");
                    //if(dzien != 0) excel.GroupsStart();
                    excel.starting_point = excel.SeekPoint(data, exact: true);
                    if (excel.starting_point[0] < 1 && excel.starting_point[1] < 5)
                    {
                        Console.WriteLine("Nie znaleziono daty " + data + " w pliku.");
                        continue;
                    }
                    excel.starting_point[0] = excel.starting_point[0] - 1;
                    excel.starting_point[1] = excel.starting_point[1] - 5;
                    excel.CheckGroups(excel.starting_point, excel.ending_point);
                    Console.WriteLine("\t\t\t\t\t" + data);
                    for (int godziny = 1; godziny <= 15; godziny++)
                    {
                        for (int wiersz = excel.starting_point[0] + 4; wiersz <= excel.ending_point[0]; wiersz++)
                        {
                            if (excel.rows_excluded.Contains(wiersz)) continue;
                            string[] range = excel.CheckModuleInfo(wiersz, excel.starting_point[1] + godziny);
                            foreach (KeyValuePair<string, List<string>> err in excel.errors)
                            {
                                Add_err(file_guid, err);
                                excel.errors.Remove(err.Key);
                            }
                            wiersz = Convert.ToInt32(range[1]);
                        }
                    }
                    var status = (dzien * 100) / 31;
                    progress.Report(status);
                }
                insert_db();
                conn_in.Close();
                excel.Dispose();
            }
            try
            {
                System.IO.File.Delete(targetPath);
            }
            catch { _logger.LogInformation($"Błąd usuwania pliku."); }
            Dispose();
            return;
        }
        void Dispose()
        {
            Groups.Clear();
            Lecturer.Clear();
            Rooms.Clear();
            Modules.Clear();
            GC.Collect();
        }
    }
}
