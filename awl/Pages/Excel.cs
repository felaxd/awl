using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using MySql.Data.MySqlClient;

namespace awl.Pages
{
    public class Excel : IDisposable
    {
        private string[] GetSQLElements(string table)
        {
            List<string> string_arr = new List<string>();
            conn.Open();
            MySqlCommand staryWynik = new MySqlCommand("SELECT name FROM " + table + " ORDER BY name ASC;", conn);
            MySqlDataReader wynik = staryWynik.ExecuteReader();
            while (wynik.Read())
            {
                string_arr.Add(wynik.GetString(0));
            }
            wynik.Close();
            conn.Close();
            return string_arr.ToArray();
        }

        readonly MySqlConnection conn;
        readonly ExcelPackage excel;
        readonly ExcelWorksheet ws;
        public Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>(2);
        readonly List<string> Groups = new List<string>();
        readonly List<string> Lecturer = new List<string>();
        readonly List<string> Rooms = new List<string>();
        readonly List<string> Modules = new List<string>();
        readonly List<string> przedmioty_upper = new List<string>();
        public List<int> rows_excluded = new List<int>();
        List<int[]> cells_excluded = new List<int[]>();
        public string year = "0", month = "0";
        public int[] starting_point = new int[2];
        public int[] ending_point = new int[2];
        public Excel(string filename, string worksheet)
        {
            Dictionary<string, string> config = new Dictionary<string, string>();
            if (!File.Exists("config.txt"))
            {
                Console.WriteLine("Brak pliku konfiguracyjnego.");
                return;
            }
            //else Console.WriteLine("\n\n\n\n\nPLIK PLIK\n\n\n\n\n");
            foreach (string item in File.ReadAllLines(@"config.txt"))
            {
                if (!item.Contains("=")) continue;
                string[] line = item.Split("=");
                config.Add(line[0], line[1]);
            }
            string conn_str = "server=" + config.GetValueOrDefault("server") + ";port=" + config.GetValueOrDefault("port") + ";database=" + config.GetValueOrDefault("database") + ";uid=" + config.GetValueOrDefault("login") + ";pwd=" + config.GetValueOrDefault("password");
            conn = new MySqlConnection(conn_str);
            //conn = new MySqlConnection("server=db;port=3306;database=plan;uid=root;pwd=pass543");
            foreach (string item in GetSQLElements("przedmioty")) if (item != "") Modules.Add(item);
            foreach (string item in GetSQLElements("grupy")) if (item != "") Groups.Add(item);
            foreach (string item in GetSQLElements("prowadzacy")) if (item != "") Lecturer.Add(item);
            foreach (string item in GetSQLElements("sale")) if (item != "") Rooms.Add(item);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            FileInfo existingFile = new FileInfo(filename);
            excel = new ExcelPackage(existingFile);
            ws = excel.Workbook.Worksheets[worksheet];
            if (ws == null) { Console.WriteLine("ws is null"); return; }
            starting_point = SeekPoint("STUDIA WOJSKOWE");
            if (starting_point[0] == 0 && starting_point[1] == 0)
            {
                Console.WriteLine("Plan cywilny");
                starting_point = SeekPoint("STUDIA CYWILNE");
            } else Console.WriteLine("Plan główny");
            starting_point[1] = starting_point[1] - 1;

            try { 
                DateTime first_date = Convert.ToDateTime(ws.Cells[starting_point[0] + 1, starting_point[1] + 5].Value); 

                Console.WriteLine(first_date);
                if (first_date.Year < 2020 || ws.Cells[starting_point[0] + 1, starting_point[1] + 5].Value == null)
                {
                    Console.WriteLine("Brak podanej daty");
                    return;
                }
                year = first_date.Year.ToString();
                month = first_date.Month.ToString();
            }
            catch { return; }
            for (int i = 1000; i > starting_point[0]; i--)
            {
                if (ws.Cells[i, starting_point[1] - 1].Style.Border.Bottom.Style.ToString() != "None") { ending_point[0] = i; ending_point[1] = starting_point[1] + 16 * 31; break; }
                if (ws.Cells[i, starting_point[1]].Style.Border.Bottom.Style.ToString() != "None") { ending_point[0] = i; ending_point[1] = starting_point[1] + 16 * 31; break; }
                if (ws.Cells[i, starting_point[1]+1].Style.Border.Bottom.Style.ToString() != "None") { ending_point[0] = i; ending_point[1] = starting_point[1] + 16 * 31; break; }
            }

            foreach (string item in Modules)
            {
                przedmioty_upper.Add(item.Replace(" ", "").ToUpper().Trim());
            }
            Console.WriteLine("Starting point: { " + starting_point[0] + ", " + starting_point[1] + " }");
            Console.WriteLine("Ending point: { " + ending_point[0] + ", " + ending_point[1] + " }");
            Console.WriteLine("Initialzied...");
            //int[] range = {367, 5, 372, 6};
            //checkModuleName(range);
            //Console.WriteLine(SerialName("ANALIZA MATEMTYCZNA"));
            //Console.ReadKey();
        }

        public void Dispose()
        {
            excel.Dispose();
            errors.Clear();
            Groups.Clear();
            Lecturer.Clear();
            Rooms.Clear();
            Modules.Clear();
            przedmioty_upper.Clear();
            rows_excluded.Clear();
            cells_excluded.Clear();
            conn.Dispose();
            GC.Collect();
        }
        /// <summary>
        /// Tworzenie podziału na grupy, 
        ///  dodawanie wierszy do tablicy wierszy pomijanych przez program (mniej wykonań pętli, brak błędów typu przedmiot o nazwie kierunku lub dnia tygodnia)
        /// </summary>
        public void GroupsStart()
        {
            string color;
            List<string> cells = new List<string>();
            List<string> groups_names = new List<string>();
            for (int i = starting_point[0] + 4; i <= ending_point[0]; i++)
            {
                color = ws.Cells[i, starting_point[1]].Style.Fill.BackgroundColor.LookupColor();
                int first_row = i;
                int len = 0;
                if (ws.Cells[first_row, starting_point[1]].Style.Border.Bottom.Style.ToString() != "None" || ws.Cells[first_row + 1, starting_point[1]].Style.Border.Top.Style.ToString() != "None" || ws.Cells[first_row, starting_point[1] + 1].Text == "1" || ws.Cells[first_row, starting_point[1] + 1].Text.Contains("KIERUNEK") || ws.Cells[first_row, starting_point[1] + 1].Text.Contains("STUDIA") || ws.Cells[first_row, starting_point[1] + 1].Text.Contains("PARZYSTY"))
                {
                    if (ws.Cells[first_row, starting_point[1]].Text.Replace(" ", "") != "" && ws.Cells[first_row, starting_point[1]].Text != ws.Cells[starting_point[0] + 1, starting_point[1]].Text) continue;
                    if (!rows_excluded.Contains(first_row))  rows_excluded.Add(first_row);
                    continue;
                }
                while (ws.Cells[first_row, starting_point[1]].Style.Fill.BackgroundColor.LookupColor() == color)
                {
                    len++;
                    if (ws.Cells[first_row, starting_point[1]].Text != "") groups_names.Add(ws.Cells[first_row, starting_point[1]].Text.Trim().Replace("  ", " "));
                    if (ws.Cells[first_row, starting_point[1]].Style.Border.Bottom.Style.ToString() != "None" || ws.Cells[first_row + 1, starting_point[1]].Style.Border.Top.Style.ToString() != "None") break;
                    first_row++;
                    if (ws.Cells[first_row, starting_point[1] + 1].Text.Trim() == "1" && ws.Cells[first_row, starting_point[1] + 2].Text.Trim() == "2")
                        if (!rows_excluded.Contains(first_row)) rows_excluded.Add(first_row);
                    if (first_row > ending_point[0]) break;
                }
                //if (ws.Cells[first_row, starting_point[1]].Style.Fill.BackgroundColor.LookupColor() != color) first_row -= 1;
                if (string.Join("|", groups_names).Replace(" ", "") == "" || string.Join("|", groups_names).Replace(" ", "") == ws.Cells[starting_point[0] + 1, starting_point[1]].Text.Replace("  ", " "))
                {
                    for (int j = i; j < i + len; j++) if(!rows_excluded.Contains(j)) rows_excluded.Add(j);
                }
                //for (int j = i; j < i + len; j++) cells.Add(ws.Cells[j, starting_point[1]].Address);
                //List<string> list = new List<string> { string.Join("|", groups_names).Trim().Replace(" ", "_"), "grupa" };
                //if (string.Join("|", groups_names).Replace(" ", "") != "" && string.Join("|", groups_names).Replace(" ", "") != ws.Cells[starting_point[0] + 1, starting_point[1]].Text.Replace("  ", " ")) 
                //    if (!Groups.Contains(string.Join("|", groups_names).Trim().Replace(" ", "_"))) 
                //        errors.Add("[ " + string.Join(" ], [ ", cells) + " ]", list);
                cells.Clear();
                groups_names.Clear();
                i = first_row;
            }
        }

        /// <summary>
        /// Wyszukuje w arkuszu pierwszego wystąpienia komórki z podamym tekstem.
        /// </summary>
        /// <param name="range"></param>
        /// <returns>
        /// Tablice dwuelementową numerów { wiersz, kolumna }
        /// </returns>
        public int[] SeekPoint(string szukana, int[] range = null, bool exact = false)
        {
            int start_row = 1, start_col = 1;
            int end_row, end_col;
            if (range == null)
            {
                end_row = ws.Dimension?.End?.Row ?? 1000;
                end_col = ws.Dimension?.End?.Column ?? 1000;
            }
            else
            {
                start_row = range[0];
                start_col = range[1];
                end_row = range[2];
                end_col = range[3];
            }
            int[] startingPoint = { 0, 0 };
            for (int row = start_row; row <= end_row; row++)
            {
                for (int col = start_col; col <= end_col; col++)
                {
                    if (exact)
                    {
                        if (ws.Cells[row, col].Text.Replace(" ", "") == szukana.Replace(" ", ""))
                        {
                            startingPoint[0] = row;
                            startingPoint[1] = col;
                            return startingPoint;
                        }
                    }
                    else if (ws.Cells[row, col].Text.Replace(" ", "").Contains(szukana.Replace(" ", "")))
                    {
                        startingPoint[0] = row;
                        startingPoint[1] = col;
                        return startingPoint;
                    }

                }
            }
            return startingPoint;
        }
        /// <summary>
        /// Zwraca rozmiar wagonika
        /// </summary>
        /// <param name="range"></param>
        /// <returns>
        /// Tablice numerów { pierwszy_wiersz, pierwsza_kolumna , ostatni_wiersz, ostatnia_kolumna}
        /// </returns>
        public int[] GetModuleRange(int first_row, int first_col)
        {
            string color;
            int first_row_h = first_row, first_col_h = first_col;
            int last_row, last_col;
            if (rows_excluded.Contains(first_row) || (ws.Cells[first_row, starting_point[1]].Style.Border.Left.Style.ToString() == "None" && ws.Cells[first_row, starting_point[1]].Style.Border.Right.Style.ToString() == "None" && ws.Cells[first_row, starting_point[1]].Style.Fill.BackgroundColor.LookupColor() == "#FF000000"))
            {
                int[] range_er = { first_row, first_col, first_row, first_col };
                return range_er;
            }
            color = ws.Cells[first_row, first_col].Style.Fill.BackgroundColor.LookupColor();
            if (color == "#FF000000" || color == "#FF808080")
            {
                if (ws.Cells[first_row, first_col].Text.Replace(" ", "") == "")
                {
                    int[] range_er = { first_row, first_col, first_row + 2, first_col };
                    return range_er;
                }
                while (ws.Cells[first_row, first_col].Style.Border.Right.Style.ToString() == "None")
                {
                    first_col++;
                    if (first_col > ending_point[1])
                    {
                        int[] range_e = { first_row, first_col, first_row + 2, first_col };
                        return range_e;
                    }
                    if (ws.Cells[first_row, first_col].Style.Fill.BackgroundColor.LookupColor() != color)
                    {
                        List<string> list = new List<string> { "Zmiana koloru bez krawędzi wagonika.", "" };
                        if (!errors.ContainsKey("[ " + ws.Cells[first_row, first_col].Address + " ]")) errors.Add("[ " + ws.Cells[first_row, first_col].Address + " ]", list);
                        first_col--;
                        break;
                    }
                    if (ws.Cells[first_row, first_col + 1].Style.Border.Left.Style.ToString() != "None") break;
                }
                last_col = first_col;
                while (ws.Cells[first_row, first_col].Style.Border.Bottom.Style.ToString() == "None")
                {
                    first_row++;
                    if (ws.Cells[first_row + 1, first_col].Style.Border.Top.Style.ToString() != "None") break;
                }
                last_row = first_row;
                int[] range = { first_row_h, first_col_h, last_row, last_col };
                return range;
            }
            else
            {
                if (ws.Cells[first_row, first_col].Text.Replace(" ", "").Length <= 1)
                {
                    int[] range_e = { first_row, first_col, first_row + 2, first_col };
                    return range_e;
                }
                color = ws.Cells[first_row, first_col].Style.Fill.BackgroundColor.LookupColor();
                while (ws.Cells[first_row, first_col].Style.Fill.BackgroundColor.LookupColor() == color)
                {
                    if (ws.Cells[first_row, first_col].Style.Border.Right.Style.ToString() != "None" || ws.Cells[first_row, first_col + 1].Style.Border.Left.Style.ToString() != "None") break;
                    first_col++;
                    if (ws.Cells[first_row, first_col].Style.Fill.BackgroundColor.LookupColor() != color)
                    {
                        List<string> list = new List<string> { "Zmiana koloru bez krawędzi wagonika.", "" };
                        if (!errors.ContainsKey("[ " + ws.Cells[first_row, first_col].Address + " ]")) errors.Add("[ " + ws.Cells[first_row, first_col].Address + " ]", list);
                        first_col--;
                        break;
                    }
                }
                last_col = first_col;
                color = ws.Cells[first_row, last_col].Style.Fill.BackgroundColor.LookupColor();
                while (ws.Cells[first_row, last_col].Style.Fill.BackgroundColor.LookupColor() == color)
                {
                    first_row += 3;
                    if (rows_excluded.Contains(first_row) || ws.Cells[first_row - 1, last_col].Style.Border.Bottom.Style.ToString() != "None" || ws.Cells[first_row, last_col].Style.Border.Top.Style.ToString() != "None") break;
                }
                last_row = first_row - 1;
                int[] range = { first_row_h, first_col_h, last_row, last_col };
                AddCellsToExcluded(range);
                return range;
            }
        }
        void AddCellsToExcluded(int[] range)
        {
            for (int i = range[0]; i <= range[2]; i++)
            {
                for (int j = range[1]; j <= range[3]; j++)
                {
                    int[] help = { i, j };
                    cells_excluded.Add(help);
                }
            }
        }
        /// <summary>
        /// Funkcja przyjmuje tablicę 4 punktów {wiersz_poczatkowy, kolumna_początkowa, wiersz_końcowy, kolumna_końcowa}
        /// </summary>
        /// <param name="range"></param>
        /// <returns>Adres zakresu w postaci Excela:  XX:XX</returns>
        public string ConvertRangeToText(int[] range)
        {
            return ws.Cells[range[0], range[1]].Address + ":" + ws.Cells[range[2], range[3]].Address;
        }
        /// <summary>
        /// Zwraca string z nazwą przedmiotu
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        private bool GetModuleName(int[] range)
        {
            if (range[0] != range[2] && range[1] != range[3]) return false;
            if ((range[0] != range[2] && range[1] == range[3]) && ws.Cells[range[0], range[1]].Text != "") return false;
            return true;
        }
        private string[] SerialName(string name)
        {
            List<string> foreach1 = new List<string>(name.ToUpper().Trim().Split(' '));
            foreach1.Reverse();
            string help_str = name.ToUpper(); ;
            string help_str_normal = name;
            foreach (string item in foreach1)
            {
                if (przedmioty_upper.Contains(help_str.Replace(" ", "").Trim())) { string[] ret1 = { help_str_normal, name.Replace(help_str_normal, "").Trim() }; return ret1; }
                if (item.Length != help_str.Length)
                {
                    help_str = help_str.Substring(0, (help_str.Length - item.Length)).Trim();
                    help_str_normal = help_str_normal.Substring(0, (help_str_normal.Length - item.Length - 1)).Trim();
                }
                else break;
            }
            string[] ret = { name, "" };
            return ret;
        }
        private void CheckModuleName(int[] range)
        {
            string str = ws.Cells[range[0], range[1]].Text.Trim();
            List<string> list = new List<string> { str, "przedmiot" };
            if (str == "") return;
            string[] name = SerialName(str);
            if (!Modules.Contains(name[0]) && !errors.ContainsKey("[ " + ws.Cells[range[0], range[1]].Address + " ]")) { errors.Add("[ " + ws.Cells[range[0], range[1]].Address + " ]", list); }
        }
        /// <summary>
        /// Zwraca string z nazwą sali z podziałem na każdego prowadzącego
        /// </summary>
        /// <param name="range"></param>
        /// <returns>prowadzacy=sala</returns>
        private void CheckModuleRoomName(int[] range)
        {
            //List<string> rooms = new List<string>();

            string[] help_lect = GetModuleLecturesNames(range).Split(',');
            string default_room = ws.Cells[range[2], range[3]].Text.Trim().Replace(",", ".");
            foreach (string lect in help_lect)
            {
                int[] pkt = SeekPoint(lect, range);
                string room = ws.Cells[pkt[0], range[3]].Text.Replace(",", ".");
                if (room.Trim() == "" || room == lect) room = default_room;
                if (room == "") continue;
                List<string> list = new List<string> { room.Trim(), "sala" };
                if (!Rooms.Contains(room.Trim()) && !errors.ContainsKey("[ " + ws.Cells[pkt[0], range[3]].Address + " ]")) errors.Add("[ " + ws.Cells[pkt[0], range[3]].Address + " ]", list);
            }
        }
        /// <summary>
        /// Zwraca string z nazwami prowadzących oddzielonymi [,]
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        private string GetModuleLecturesNames(int[] range)
        {
            List<string> lecturers = new List<string>();
            string color = ws.Cells[range[0], range[1]].Style.Fill.BackgroundColor.LookupColor();
            int row = range[0] + 1; //zaczyna od drugiej komórki w zakresie
            if ((range[0] != range[2] && range[1] == range[3]) && ws.Cells[range[0], range[1]].Text != "") return ws.Cells[row, range[1]].Text.Replace("  ", " ");
            int limit = range[2];
            if (ws.Cells[limit, range[1]].Style.Fill.BackgroundColor.LookupColor() != color || (range[3] - range[1] + 1) == 1) limit--;
            while (row <= limit)
            {
                if (ws.Cells[row, range[1]].Text == "") { row++; continue; }
                if (ws.Cells[row, range[1]].Text == "30 GODZIN") { row++; continue; }
                if ((int)Convert.ToChar(ws.Cells[row, range[1]].Value.ToString()[0]) < 65) { break; }
                string[] help_arr = ws.Cells[row, range[1]].Text.Replace("  ", " ").Split(',');
                foreach (string help_str in help_arr)
                {
                    string help_str1 = help_str.Replace("/ ", "/").Trim();
                    if (help_str1.Contains("/")) help_str1 = help_str1.Split('/')[1];
                    if (!Lecturer.Contains(help_str1)) Lecturer.Add(help_str1);
                    lecturers.Add(help_str1);
                }
                row++;
            }
            string lecturers_ret = string.Join(",", lecturers);
            return lecturers_ret;
        }

        private void CheckModuleLecturesNames(int[] range)
        {
            //List<string> lecturers = new List<string>();
            string color = ws.Cells[range[0], range[1]].Style.Fill.BackgroundColor.LookupColor();
            int row = range[0] + 1; //zaczyna od drugiej komórki w zakresie
            int limit = range[2];
            if (ws.Cells[limit, range[1]].Style.Fill.BackgroundColor.LookupColor() != color || (range[3] - range[1] + 1) == 1) limit--;
            while (row <= limit)
            {
                if (ws.Cells[row, range[1]].Text == "") { row++; continue; }
                if ((int)Convert.ToChar(ws.Cells[row, range[1]].Value.ToString()[0]) < 65) { break; }
                string[] help_arr = ws.Cells[row, range[1]].Text.Split(',');
                foreach (string help_str in help_arr)
                {
                    string help_str1 = help_str.Replace("/ ", "/").Trim();
                    string[] help_str2 = help_str1.Split('/');
                    foreach (var str in help_str2)
                    {
                        List<string> list = new List<string> { str, "prowadzacy" };
                        if (!Lecturer.Contains(str) && !errors.ContainsKey("[ " + ws.Cells[row, range[1]].Address + " ]")) 
                            errors.Add("[ " + ws.Cells[row, range[1]].Address + " ]", list);
                    }
                }
                row++;
            }
        }

        /// <summary>
        /// Zwraca Informacje zawarte w wagoniku.
        /// Tablica String
        /// </summary>
        /// <param name="row">wiersz_początku_wagonika</param>
        /// <param name="col">kolumna_początku_wagonika</param>
        /// <returns>
        /// Dla niepustego wagonika: tablica { nazwa_przedmiotu, nazwika_wykładowców, sala, długość_wagonika, grupy, zakres_excelowy, indeks_ostatniego_wiersza_wagonika }
        /// <para/>
        /// Dla pustego wagonika: tablica { indeks_ostatniego_wiersza_wagonika }
        /// </returns>
        public string[] CheckModuleInfo(int row, int col)
        {
            int[] help = { row, col };
            if (rows_excluded.Contains(row))
            {
                string[] info = { (row).ToString(), (row).ToString() };
                return info;
            }
            if (cells_excluded.Contains(help)) // || (ws.Cells[row, col].Style.Border.Left.Style.ToString() == "None" && ws.Cells[row, col - 1].Style.Border.Right.Style.ToString() == "None")
            {
                string[] info = { (row).ToString(), (row + 2).ToString() };
                return info;
            }
            int[] range = GetModuleRange(row, col);
            if (GetModuleName(range)) { string[] info = { range[0].ToString(), range[2].ToString() }; return info; }
            CheckModuleLecturesNames(range);
            //CheckModuleRoomName(range);
            //CheckModuleName(range);
            string[] info_ret = { range[0].ToString(), range[2].ToString() };
            return info_ret;
        }

        public void CheckGroups(int[] start, int[] end)
        {
            string color;
            List<string> cells = new List<string>();
            List<string> groups_names = new List<string>();
            for (int i = start[0] + 4; i <= end[0]; i++)
            {
                color = ws.Cells[(int)i, start[1]].Style.Fill.BackgroundColor.LookupColor();
                int first_row = i;
                int len = 0;
                //if (ws.Cells[i, start[1]].Style.Border.Bottom.Style.ToString() != "None" || ws.Cells[i + 1, start[1]].Style.Border.Top.Style.ToString() != "None" || ws.Cells[i, start[1] + 1].Text == "1" || ws.Cells[i, start[1] + 1].Text.Contains("KIERUNEK") || ws.Cells[i, start[1] + 1].Text.Contains("STUDIA") || ws.Cells[i, start[1] + 1].Text.Contains("PARZYSTY"))
                //{
                //    if (ws.Cells[i, start[1]].Text.Replace(" ", "") != "" && ws.Cells[i, start[1]].Text != ws.Cells[start[0] + 1, start[1]].Text) continue;
                //    if (!rows_excluded.Contains(i)) rows_excluded.Add(i);
                //    continue;
                //}
                while (ws.Cells[i, start[1]].Style.Fill.BackgroundColor.LookupColor() == color)
                {
                    len++;
                    if (ws.Cells[i, start[1]].Text != "") groups_names.Add(ws.Cells[i, start[1]].Text.Trim().Replace("  ", " "));
                    if (ws.Cells[i, start[1]].Style.Border.Bottom.Style.ToString() != "None" || ws.Cells[i + 1, start[1]].Style.Border.Top.Style.ToString() != "None") break;
                    i++;
                    if (i > end[0]) break;
                }
                if (ws.Cells[i, start[1]].Style.Fill.BackgroundColor.LookupColor() != color) i -= 1;
                //if (string.Join("|", groups_names).Replace(" ", "") == "" || string.Join("|", groups_names).Replace(" ", "") == ws.Cells[start[0] + 1, start[1]].Text.Replace("  ", " "))
                //{
                //    //for (int j = first_row; j < first_row + len; j++) if (!rows_excluded.Contains(j)) rows_excluded.Add(j);
                //}
                for (int j = first_row; j < first_row + len; j++) cells.Add(ws.Cells[j, start[1]].Address);
                List<string> list = new List<string> { string.Join("|", groups_names).Trim().Replace(" ", "_"), "grupa" };
                if (string.Join("|", groups_names).Replace(" ", "") != "" && string.Join("|", groups_names).Replace(" ", "") != ws.Cells[start[0] + 1, start[1]].Text.Replace("  ", " ")) 
                    if (!Groups.Contains(string.Join("|", groups_names).Trim().Replace(" ", "_"))) 
                        errors.Add("[ " + string.Join(" ], [ ", cells) + " ]", list);
                cells.Clear();
                groups_names.Clear();
            }
        }

    }
}