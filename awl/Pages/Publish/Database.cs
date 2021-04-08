using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;

namespace awl.Pages.Publish
{
    class Database
    {
        public int updated = 0, removed = 0, inserted = 0;
        public MySqlConnection conn;
        public MySqlCommand cmd;
        public Database(string host="localhost", string database="", string login="root", string password="")
        {
            conn = new MySqlConnection("server=" + host + ";database=" + database + ";uid=" + login + ";pwd=" + password);
            cmd = null;
            conn.Open();
        }
        public string[] GetSQLElements(string table, string row="name", string where="")
        {
            List<string> string_arr = new List<string>();
            MySqlCommand staryWynik = new MySqlCommand("SELECT `" + row +"` FROM `" + table + "` " + where + " ORDER BY `" + row + "` ASC;", conn);
            MySqlDataReader wynik = staryWynik.ExecuteReader();
            while (wynik.Read())
            {
                string_arr.Add(wynik.GetString(0));
            }
            wynik.Close();
            return string_arr.ToArray();
        }
        public void removeFromDatabase(string code, string table)
        {
            string cmdString = "DELETE FROM `" + table + "` WHERE `code` ='" + code + "';";
            Console.WriteLine(cmdString);
            cmd = new MySqlCommand(cmdString, conn);
            cmd.ExecuteNonQuery();
            removed++;
        }
        public string[] getFromDatabase(string table) {
            List<string> result = new List<string>();
            MySqlCommand staryWynik = new MySqlCommand("SELECT `name` FROM `" + table + "`;", conn);
            MySqlDataReader wynik = staryWynik.ExecuteReader();
            while (wynik.Read())
            {
                result.Add(wynik.GetString(0));
            }
            wynik.Close();
            return result.ToArray();
        }
        List<string> insert_rows = new List<string>();
        
        public void addToDatabase(string[] info, string table)
        {
            if (table == "zajecia")
            {
                for (int i = 0; i < info.Length; i++) info[i] = info[i].Replace(@"\", "|").Trim();
                //0code   1data   2name 3info  4lecturer  5room       6start_hour      7lenght   8groups   9type
                MySqlCommand staryWynik = new MySqlCommand("SELECT * FROM `" + table + "` WHERE `code` ='" + info[0] + "' AND `date` ='"+ info[1] +"';", conn);
                MySqlDataReader wynik = staryWynik.ExecuteReader();
                MySqlCommand cmd;
                if (wynik.Read())
                {
                    if (wynik.GetString(3) != info[2] || wynik.GetString(4) != info[3] || wynik.GetString(5) != info[4] || wynik.GetString(6) != info[5] || wynik.GetString(8) != info[7] || wynik.GetString(9) != info[8] || wynik.GetString(10) != info[9])
                    {
                        wynik.Close();
                        String Query = "UPDATE `" + table + "` SET `name` = @name, `info` = @info, `lecturer` = @lecturer, `room` = @room, `lenght` = @lenght, `groups` = @groups, `type` = @type WHERE `code` = @code;";
                        cmd = new MySqlCommand(Query, conn);
                        cmd.Parameters.AddWithValue("@name", info[2]);
                        cmd.Parameters.AddWithValue("@info", info[3]);
                        cmd.Parameters.AddWithValue("@lecturer", info[4]);
                        cmd.Parameters.AddWithValue("@room", info[5]);
                        cmd.Parameters.AddWithValue("@lenght", info[7]);
                        cmd.Parameters.AddWithValue("@groups", info[8]);
                        cmd.Parameters.AddWithValue("@type", info[9]);
                        cmd.Parameters.AddWithValue("@code", info[0]);
                        
                        if (cmd.ExecuteNonQuery() != -1)
                        {
                            Console.WriteLine(cmd);
                            updated++;
                        }
                    }
                    else
                    {
                        wynik.Close();
                        return;
                    }
                }
                else
                {
                    wynik.Close();
                    //cmdString = "INSERT INTO `" + table + "` (`code`, `date`, `name`, `info`, `lecturer`, `room`, `start_hour`, `lenght`, `groups`, `type`) VALUES ('" + info[0] + "','" + info[1] + "','" + info[2] + "','" + info[3] + "','" + info[4] + "','" + info[5] + "','" + info[6] + "','" + info[7] + "','" + info[8] + "','" + info[9] + "')";
                    //String Query = "INSERT INTO `" + table + "` (`code`, `date`, `name`, `info`, `lecturer`, `room`, `start_hour`, `lenght`, `groups`, `type`) VALUES (@code, @date, @name, @info, @lecturer, @room, @start_hour, @lenght, @groups, @type)";
                    //cmd = new MySqlCommand(Query, conn);
                    //cmd.Parameters.AddWithValue("@code", info[0]);
                    //cmd.Parameters.AddWithValue("@date", info[1]);
                    //cmd.Parameters.AddWithValue("@name", info[2]);
                    //cmd.Parameters.AddWithValue("@info", info[3]);
                    //cmd.Parameters.AddWithValue("@lecturer", info[4]);
                    //cmd.Parameters.AddWithValue("@room", info[5]);
                    //cmd.Parameters.AddWithValue("@start_hour", info[6]);
                    //cmd.Parameters.AddWithValue("@lenght", info[7]);
                    //cmd.Parameters.AddWithValue("@groups", info[8]);
                    //cmd.Parameters.AddWithValue("@type", info[9]);

                    insert_rows.Add(string.Format("('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')", MySqlHelper.EscapeString(info[0]), 
                                                                                               MySqlHelper.EscapeString(info[1]), 
                                                                                               MySqlHelper.EscapeString(info[2]),
                                                                                               MySqlHelper.EscapeString(info[3]),
                                                                                               MySqlHelper.EscapeString(info[4]),
                                                                                               MySqlHelper.EscapeString(info[5]),
                                                                                               MySqlHelper.EscapeString(info[6]),
                                                                                               MySqlHelper.EscapeString(info[7]),
                                                                                               MySqlHelper.EscapeString(info[8]),
                                                                                               MySqlHelper.EscapeString(info[9])
                                                                                               ));
                    Console.WriteLine("INSERT INTO `zajecia` (`code`, `date`, `name`, `info`, `lecturer`, `room`, `start_hour`, `lenght`, `groups`, `type`) VALUES " + insert_rows[insert_rows.Count - 1]);
                    inserted++;
                    if(insert_rows.Count >= 500) ExecuteInsertQuery();
                }
                wynik.Close();
                //if (cmdString != "")
                //{
                //cmd = new MySqlCommand(cmdString, conn);
                //cmd.ExecuteNonQuery();
                //}
            }
            
        }

        public void ExecuteInsertQuery()
        {
            if (insert_rows.Count != 0)
            {
                StringBuilder insert_str = new StringBuilder("INSERT INTO `zajecia` (`code`, `date`, `name`, `info`, `lecturer`, `room`, `start_hour`, `lenght`, `groups`, `type`) VALUES ");
                insert_str.Append(string.Join(',', insert_rows));
                insert_str.Append(";");
                cmd = new MySqlCommand(insert_str.ToString(), conn);
                if (cmd.ExecuteNonQuery() != -1)
                    Console.WriteLine("Dodano " + insert_rows.Count + " rekordów.");
                insert_rows.Clear();
            }
        }

        public void close() {
            conn.Close();
        }
    }
}
