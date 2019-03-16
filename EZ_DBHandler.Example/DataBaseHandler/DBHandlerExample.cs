using EZ_DBHandler.DataBaseHandler;
using EZ_DBHandler.DataBaseHandler.MySQL.CustomDataTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EZ_DBHandler.Example.DataBaseHandler
{
    public class DBHandlerExample
    {
        private DBHandler _dBHandler;

        public DBHandlerExample()
        {
            try
            {
                // Startup Routine -> create DB and add Tables
                //CreateDataBaseExample("", DataBaseType.SQLITE);
                CreateDataBaseExampleWithoutTables(DataBaseType.ACCESS);
                //CreateMySQLDataBaseExample(DataBaseType.MYSQL);
                //CreateCustomDataTypeExample();
                _dBHandler.ExceptionEvent += OnExceptionEvent;
                _dBHandler.DeleteEvent += OnDeleteEvent;

                AddTablesExample();
                //AddTableExample();

                // Basic Functions
                //DropTablesExample();
                //InsertRowsExample("table3", 100);
                //GetRowByIDExample();
                //GetLastRowExample();
                //GetLastNRowsExample(5, true);
                //GetLastNRowsExample(5, false);
                //UpdateRowsExample();

                // Specific Functions
                //GetRowsFromTableWithTimeExample(true);
                //GetRowsFromTableWithTimeExample(false);
                //GetRowsFromTableWithIndexExample(true);
                //GetRowsFromTableWithIndexExample(false);

                //DeleteLastNRowsExample("table3", 10);
                //DeleteThreadExample();
                //Console.WriteLine(_dBHandler.GetCurrentRowsFromTable("table3"));
                //_dBHandler.StartDeleteThread(3000);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            GC.Collect();
            Console.WriteLine("Finished!");
            Console.ReadLine();
            _dBHandler.DisposeDBHandler();
        }

        private void OnDeleteEvent(object sender, string e)
        {
            Console.WriteLine("OnDeleteEvent: " + e);
        }

        private void CreateCustomDataTypeExample()
        {
            //new MediumInt(-8388609);
            //new MediumInt(-8388608);
            Console.WriteLine(new MySQLTime(DateTime.Now));
            string text = "";
            for (int i = 0; i < 256; i++)
            {
                text += 'A';
            }
            new TinyText(text);
            new Text(text);
        }

        private void OnExceptionEvent(object sender, Exception e)
        {
            Console.WriteLine(e);
        }
        private void CreateMySQLDataBaseExample(DataBaseType dataBaseType)
        {
            _dBHandler = new DBHandler(
                "name",
                "",
                new List<Table>()
                {
                    new Table(
                        "mysqltable",
                        new Dictionary<string, Type>()
                        {
                            { "id" , typeof(long) },
                            { "column_1", typeof(sbyte) },
                            { "column_2", typeof(short) },
                            { "column_3", typeof(MediumInt) },
                            { "column_4", typeof(int) },
                            { "column_5", typeof(long) },
                            { "column_6", typeof(float) },
                            { "column_7", typeof(double) },
                            { "column_8", typeof(bool) },
                            { "column_9", typeof(MySQLTime) },
                            { "column_10", typeof(DateTime) },
                            { "column_11", typeof(TinyText) },
                            { "column_12", typeof(Text) },
                            { "column_13", typeof(MediumText) },
                            { "column_14", typeof(LongText) }
                        },
                        100)
                },
                dataBaseType);
        }

        private void CreateDataBaseExampleWithoutTables(DataBaseType dataBaseType)
        {
            _dBHandler = new DBHandler(
               "name",
               "localhost",
               dataBaseType);
        }

        private void CreateDataBaseExample(string dbPath, DataBaseType dataBaseType)
        {
            _dBHandler = new DBHandler(
                "name",
                dbPath,
                new List<Table>()
                {
                    new Table(
                        "table1",
                        new Dictionary<string, Type>()
                        {
                            { "id" , typeof(int) },
                            { "firstColumn", typeof(string) },
                            { "secondColumn", typeof(float) }
                        },
                        100),
                    new Table(
                        "table2",
                        new Dictionary<string, Type>()
                        {
                            { "id" , typeof(int) },
                            { "firstColumn", typeof(DateTime) }
                        },
                        10),
                },
                dataBaseType);
        }

        private void AddTableExample()
        {
            _dBHandler.AddTable(new Table(
                "table3",
                new Dictionary<string, Type>()
                {
                    { "id" , typeof(int) },
                    { "Name", typeof(string) },
                    { "Date", typeof(DateTime) },
                    { "value", typeof(double) }
                },
                100));
        }

        private void AddTablesExample()
        {
            List<Table> list = new List<Table>()
                {
                    new Table(
                        "table3",
                        new Dictionary<string, Type>()
                        {
                            { "id" , typeof(int) },
                            { "Name", typeof(string) },
                            { "Date", typeof(DateTime) },
                            { "value", typeof(double) }
                        },
                        100),
                    new Table(
                        "table4",
                        new Dictionary<string, Type>()
                        {
                            { "id" , typeof(int) },
                            { "firstColumn", typeof(string) }
                        },
                        10),
                };
            _dBHandler.AddTables(list);
        }

        private void UpdateRowsExample()
        {
            List<Dictionary<string, Type>> rowsToUpdate = new List<Dictionary<string, Type>>()
                {
                    new Dictionary<string, Type>()
                        {
                            { "id" , typeof(int) },
                            { "Name", typeof(string) },
                            { "Date", typeof(DateTime) },
                            { "value", typeof(double) }
                        },
                    new Dictionary<string, Type>()
                        {
                            { "id" , typeof(int) },
                            { "Name", typeof(string) },
                        }
                };

            List<List<object>> rowsData = new List<List<object>>()
                {
                    new List<object>()
                    {
                        1,
                        "1st whatever",
                        DateTime.Now,
                        0.815
                    },
                    new List<object>()
                    {
                        2,
                        "2 whatever",
                    }
                };

            _dBHandler.UpdateTable("table3", rowsToUpdate, rowsData);
        }

        private void DropTablesExample()
        {
            _dBHandler.DropTables(new List<string>()
                {
                    "table3",
                    "table4",
                });
        }

        private void GetLastNRowsExample(int rows, bool ascending)
        {
            List<List<object>> resultList = _dBHandler.GetLastNRowsFromTable("table3", rows, ascending);

            foreach (var itemList in resultList)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in itemList)
                {
                    if (item.GetType().Equals(typeof(DateTime)))
                        sb.Append(((DateTime)item).ToString("yyyy-MM-dd HH:mm:ss.fff") + "|");
                    sb.Append(item + "|");
                }
                Console.WriteLine(sb.ToString());
            }
        }

        private void GetLastRowExample()
        {
            List<object> resultList = _dBHandler.GetLastRowFromTable("table3");

            StringBuilder sb = new StringBuilder();
            foreach (var item in resultList)
            {
                sb.Append(item + "|");
            }
            Console.WriteLine(sb.ToString());
        }

        private void GetRowByIDExample()
        {
            List<object> resultList = _dBHandler.GetRowById("table3", 20);

            StringBuilder sb = new StringBuilder();
            foreach (var item in resultList)
            {
                sb.Append(item + "|");
            }
            Console.WriteLine(sb.ToString());
        }

        private void InsertRowsExample(string tableName, int numberOfRows)
        {
            List<List<object>> rows = new List<List<object>>();

            for (int iter = 0; iter < numberOfRows; iter++)
            {
                List<object> values1 = new List<object>()
                {
                    0,
                    GetRandomName(false),
                    DateTime.Now,
                    (3.33 + (double) iter)
                };
                rows.Add(values1);
            }

            _dBHandler.InsertIntoTable(tableName, rows);
        }

        private void GetRowsFromTableWithTimeExample(bool ascending)
        {
            string from = "2019-01-26 12:10:28";
            string until = "2019-01-26 14:10:28";

            List<List<object>> resultList2 = _dBHandler.GetRowsFromTableWithTime("table3", "Date", DateTime.Parse(from), DateTime.Parse(until), ascending);

            foreach (var itemList in resultList2)
            {
                StringBuilder sb = new StringBuilder();
                foreach (object item in itemList)
                {
                    if (item.GetType() == typeof(DateTime))
                    {
                        DateTime convertDateTime = (DateTime)item;
                        sb.Append(convertDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") + "|");
                    }
                    else
                        sb.Append(item + "|");
                }
                Console.WriteLine(sb.ToString());
            }
        }

        private void GetRowsFromTableWithIndexExample(bool ascending)
        {
            List<List<object>> resultList3 = _dBHandler.GetRowsFromTableWithIndex("table3", 2, 10, ascending);

            foreach (var itemList in resultList3)
            {
                StringBuilder sb = new StringBuilder();
                foreach (object item in itemList)
                {
                    if (item.GetType() == typeof(DateTime))
                    {
                        DateTime convertDateTime = (DateTime)item;
                        sb.Append(convertDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") + "|");
                    }
                    else
                        sb.Append(item + "|");
                }
                Console.WriteLine(sb.ToString());
            }
        }

        private void DeleteLastNRowsExample(string tableName, int rows)
        {
            _dBHandler.DeleteLastNRows(tableName, rows);
        }

        private void DeleteThreadExample()
        {
            _dBHandler.StartDeleteThread(10000);
        }

        private string GetRandomName(bool withTimeOut = true)
        {
            if (withTimeOut)
                Thread.Sleep(10);
            string name = "";

            List<string> maleNames = new List<string>() { "Liam", "Noah", "William", "James", "Logan", "Benjamin", "Mason", "Elijah", "Oliver", "Jacob" };
            List<string> femaleNames = new List<string>() { "Emma", "Olivia", "Ava", "Isabella", "Sophia", "Mia", "Charlotte", "Amelia", "Evelyn" };
            List<string> lastNames = new List<string>() { "Tappler", "Stacher", "Kolleger", "Floss", "Schoiswohl", "Christandl", "Zwanzger", "Hrab", "Pressl",
                                                          "Zach", "Pensold", "Schriebl"};

            if (new Random().Next(0, 10) % 2 == 0)
            {
                name = maleNames[new Random().Next(0, maleNames.Count - 1)] + " ";
                if (withTimeOut)
                    Thread.Sleep(10);
                name += lastNames[new Random().Next(0, lastNames.Count - 1)];
            }
            else
            {
                name = femaleNames[new Random().Next(0, femaleNames.Count - 1)] + " ";
                if (withTimeOut)
                    Thread.Sleep(10);
                name += lastNames[new Random().Next(0, lastNames.Count - 1)];
            }
            return name;
        }
    }
}
