using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;

namespace EZ_DBHandler.DataBaseHandler.SQLite
{
    /// <summary>
    /// This class creates a connection to the sqlite database file which is given within the connectionString in the constructor.
    /// Each database connection should have his own object if this class. This class is not Thread Safe.
    /// </summary>
    public class SQLiteHandler : AbstractDBHandler
    {
        #region Private Members
        private string _connectionString;
        private string _stringFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private Thread FetchThread;
        private Boolean _cancelFetch;
        private Dictionary<string, Thread> _tableDeleteThreads = new Dictionary<string, Thread>();
        /// <summary>
        /// This value is used to stop the thread after every fetch and wait for the mrse.Set() command.
        /// </summary>
        private ManualResetEvent mrse = new ManualResetEvent(false);

        private readonly object _connectionLock = new object();
        #endregion

        #region Event Members
        /// <summary>
        /// The event to get update events on the <see cref="OnSQLiteFetch(FetchArgs)"/> method.
        /// </summary>
        public override event EventHandler<FetchArgs> FetchEvent;
        /// <summary>
        /// This event is invoked if the delete of a certain tables was started.
        /// </summary>
        public override event EventHandler<string> DeleteEvent;
        #endregion

        #region Constructor
        /// <summary>
        /// The constructor which creates a new database connection with the given connectionString
        /// </summary>
        /// <param name="connectionPath"></param>
        /// <param name="databaseName"></param>
        public SQLiteHandler(string databaseName, string connectionPath)
        {
            try
            {
                ChangeConnectionString(databaseName, connectionPath);
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Changes the _connectionString.
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="connectionPath"></param>
        public override void ChangeConnectionString(string databaseName, string connectionPath)
        {
            try
            {
                lock (_connectionLock)
                {
                    _connectionString = "Data Source = " + connectionPath + "\\" + databaseName + "; Version = 3;";
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Changes the _connectionString.
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="connectionPath"></param>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <param name="port"></param>
        public override void ChangeConnectionString(string databaseName, string connectionPath, string user, string password, string port)
        {
            throw new NotSupportedException("This ChangeConnectionString is not supported in SQLite!");
        }

        /// <summary>
        /// Creates all saved tables in the database.
        /// </summary>
        /// <param name="tables"></param>
        public override void CreateTables(ConcurrentDictionary<string, Table> tables)
        {
            try
            {
                CommitBatchQuery(CreateTableQueries(tables));
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Adds tables and creates them at the given database.
        /// </summary>
        /// <param name="tables">A list of tables to add to the database.</param>
        public override void AddTables(ConcurrentDictionary<string, Table> tables)
        {
            try
            {
                CommitBatchQuery(CreateTableQueries(tables));
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Drops the tables with the given table names.
        /// <para/>
        /// SQL query Example: <para/>
        /// DROP TABLE table1
        /// </summary>
        /// <param name="tableNames">A list of table names to delete.</param>
        public override void DropTables(List<string> tableNames)
        {
            try
            {
                List<string> tableQueries = new List<string>();
                foreach (var tableName in tableNames)
                {
                    SQLQueryBuilder sqb = new SQLQueryBuilder();
                    SQLQueryBuilder sqb_count = new SQLQueryBuilder();
                    sqb.DropTable().AddValue(tableName);
                    sqb_count.DropTable().AddValue(tableName + "_count");
                    tableQueries.Add(sqb.ToString());
                    tableQueries.Add(sqb_count.ToString());
                }
                CommitBatchQuery(tableQueries);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Inserts the given objects into the given table. The first object "id" is ignored due to the auto increment,
        /// <para/>
        /// SQL query Example: <para/>
        /// INSERT INTO table3 (Name, Date, value) VALUES ('John Doe', '2018-12-06 12:01:16.767', 22.5);
        /// </summary>
        /// <param name="tableName">The name of the table to insert rows to.</param>
        /// <param name="rows">A list of rows with all column objects to insert.</param>
        /// <param name="tables">All saved tables.</param>
        public override void InsertIntoTable(string tableName, ConcurrentDictionary<string, Table> tables, List<List<object>> rows)
        {
            try
            {
                List<string> queryList = new List<string>();
                foreach (List<object> row in rows)
                {
                    SQLQueryBuilder sqb = new SQLQueryBuilder();
                    List<string> firstBracket = new List<string>();
                    List<string> secondBracket = new List<string>();
                    int listIter = 0;
                    foreach (KeyValuePair<string, Type> column in tables[tableName].Columns)
                    {
                        if (listIter.Equals(0))
                        {
                            listIter++;
                            continue;
                        }

                        if (!row[listIter].GetType().Equals(column.Value))
                            throw new TypeLoadException("Type of the data doesn't match the columns type!");

                        firstBracket.Add(sqb.AddValue(column.Key).Flush());

                        if (column.Value == typeof(int))
                            sqb.AddValue(row[listIter].ToString());
                        else if (column.Value == typeof(string))
                            sqb.Apostrophe(sqb.AddValue(row[listIter].ToString()).Flush());
                        else if (column.Value == typeof(double))
                            sqb.AddValue(row[listIter].ToString().Replace(',', '.'));
                        else if (column.Value == typeof(float))
                            sqb.AddValue(row[listIter].ToString().Replace(',', '.'));
                        else if (column.Value == typeof(DateTime))
                        {
                            DateTime convertTime = (DateTime)row[listIter];
                            sqb.Apostrophe(sqb.AddValue(convertTime.ToString(_stringFormat)).Flush());
                        }
                        else
                            throw new NotSupportedException(column.Value.Name + " Datatype not supported");

                        secondBracket.Add(sqb.Flush());
                        listIter++;
                    }
                    string columnNames = sqb.Brackets_Multiple(firstBracket, false).Flush();
                    string columnValues = sqb.Brackets_Multiple(secondBracket, false).Flush();
                    sqb.InsertInto().AddValue(tableName).AddValue(columnNames).Values().AddValue(columnValues);
                    queryList.Add(sqb.ToString());
                }
                CommitBatchQuery(queryList);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Updates the given columns with the given id in the first column.
        /// Each row of rowsToUpdate must have the same size as rowsData.
        /// </summary>
        /// <param name="tableName">The table where rows should be updated.</param>
        /// <param name="rowsToUpdate">The rows with the name and data type to update.</param>
        /// <param name="rowsData">The rows with all column data which should be updated.</param>
        public override void UpdateTable(string tableName, List<Dictionary<string, Type>> rowsToUpdate, List<List<object>> rowsData)
        {
            try
            {
                int rowIter = 0;
                List<string> queryList = new List<string>();
                foreach (Dictionary<string, Type> row in rowsToUpdate)
                {
                    SQLQueryBuilder sqb = new SQLQueryBuilder();
                    List<object> columnData = rowsData[rowIter];
                    List<string> columns = new List<string>();

                    if (!row.Count.Equals(columnData.Count))
                        throw new InvalidDataException(rowIter.ToString() + ". row size from rowsToUpdate doesn't match current row size from rowsData");

                    int columnIter = 0;
                    foreach (KeyValuePair<string, Type> column in row)
                    {
                        if (columnIter.Equals(0))
                        {
                            columnIter++;
                            continue;
                        }

                        if (!columnData[columnIter].GetType().Equals(column.Value))
                            throw new TypeLoadException("Type of the data doesn't match the columns type!");

                        if (column.Value == typeof(int))
                            sqb.AddValue(columnData[columnIter].ToString());
                        else if (column.Value == typeof(string))
                            sqb.Apostrophe(sqb.AddValue(columnData[columnIter].ToString()).Flush());
                        else if (column.Value == typeof(double))
                            sqb.AddValue(columnData[columnIter].ToString().Replace(',', '.'));
                        else if (column.Value == typeof(float))
                            sqb.AddValue(columnData[columnIter].ToString().Replace(',', '.'));
                        else if (column.Value == typeof(DateTime))
                        {
                            DateTime convertTime = (DateTime)columnData[columnIter];
                            sqb.Apostrophe(sqb.AddValue(convertTime.ToString(_stringFormat)).Flush());
                        }
                        else
                            throw new NotSupportedException(column.Value.Name + " Datatype not supported");

                        string value = sqb.Flush();

                        sqb.AddValue(column.Key).Equal().AddValue(value);

                        columns.Add(sqb.Flush());
                        columnIter++;
                    }
                    sqb.Update().AddValue(tableName).Set().AddValues(columns).Where().AddValue(row.First().Key).Equal().AddValue(columnData[0].ToString());
                    queryList.Add(sqb.ToString());
                    rowIter++;
                }
                CommitBatchQuery(queryList);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Get's the last n rows from the specified table.
        /// <para/>
        /// SQL query Example:<para/>
        /// SELECT * FROM (SELECT * FROM name.table3 ORDER BY id DESC LIMIT 1) ORDER BY id ASC;
        /// </summary>
        /// <param name="rows">number of the rows to display.</param>
        /// <param name="table">The table to get the values from.</param>
        /// <param name="ascending">Ascending or descending by first param.</param>
        /// <returns></returns>
        public override List<List<object>> GetLastNRowsFromTable(Table table, int rows, bool ascending = true)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                sqb.Select().ALL().From().AddValue(table.TableName).OrderBY().AddValue(table.Columns.First().Key).Desc().Limit().AddValue(rows.ToString());

                if (ascending)
                {
                    string value = sqb.Flush();
                    sqb.Select().ALL().From().Brackets(value).OrderBY().AddValue(table.Columns.First().Key).Asc();
                }

                List<List<object>> results = ReadQuery(sqb.ToString(), GenerateOutputValuesFromTable(table));
                return results;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Gets all rows in the given DateTime slot.
        /// <para/>
        /// SQL query Example: <para/>
        /// select * from table3 where Date >= "2018-12-06 11:10:32.632" and Date -= "2018-12-06 12:05:57.526";
        /// </summary>
        /// <param name="table">The name of the table to get the data from.</param>
        /// <param name="DateTimeColumnName">The name of the column with the DateTime values.</param>
        /// <param name="from">A DateTime object with the beginning of the timeslot.</param>
        /// <param name="until">A DateTime object with the end of the timeslot.</param>
        /// <param name="ascending">Ascending or descending by DateTimeColumn param.</param>
        /// <returns></returns>
        public override List<List<object>> GetRowsFromTableWithTime(Table table, string DateTimeColumnName, DateTime from, DateTime until, bool ascending = true)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                string stringFrom = sqb.Apostrophe(from.ToString(_stringFormat)).Flush();
                string stringUntil = sqb.Apostrophe(until.ToString(_stringFormat)).Flush();

                sqb.Select().ALL().From().AddValue(table.TableName).Where().AddValue(DateTimeColumnName);
                sqb.GreaterThen().AddValue(stringFrom).AND().AddValue(DateTimeColumnName).LesserThen().AddValue(stringUntil);

                if (!ascending)
                    sqb.OrderBY().AddValue(DateTimeColumnName).Desc();

                List<List<object>> results = ReadQuery(sqb.ToString(), GenerateOutputValuesFromTable(table));
                return results;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Gets all rows in the given id slot.
        /// <para/>
        /// SQL query Example: <para/>
        /// SELECT * from table3 where id >= 0 and id -= 1
        /// </summary>
        /// <param name="table"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="ascending">Ascending or descending by first param.</param>
        /// <returns></returns>
        public override List<List<object>> GetRowsFromTableWithIndex(Table table, int start, int end, bool ascending = true)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                sqb.Select().ALL().From().AddValue(table.TableName).Where().AddValue(table.Columns.First().Key);
                sqb.GreaterThen().AddValue(start.ToString()).AND().AddValue(table.Columns.First().Key).LesserThen().AddValue(end.ToString());

                if (!ascending)
                    sqb.OrderBY().AddValue(table.Columns.First().Key).Desc();

                List<List<object>> results = ReadQuery(sqb.ToString(), GenerateOutputValuesFromTable(table));
                return results;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Deletes the last n rows of the given table.
        /// </summary>
        /// <param name="table">The table to delete the last n data from.</param>
        /// <param name="rows">The amount of data to delete.</param>
        public override void DeleteLastNRows(Table table, int rows)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                string columnName = table.Columns.First().Key;
                string param = sqb.Select().AddValue(columnName).From().AddValue(table.TableName).OrderBY().
                    AddValue(columnName).Asc().Limit().AddValue(rows.ToString()).Flush();
                sqb.Delete().From().AddValue(table.TableName).Where().AddValue(columnName).In().Brackets(param);

                CommitQuery(sqb.ToString());
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// If the current rowCount value is higher then the MaxRows value the deletion will be started.
        /// Now the size of the table will be shrinked to 70% of the MaxRows value. The oldest values will be deleted.
        /// If the amount of the rows to be deleted is higher than the global _maxDeleteRowSize then only every 5 seconds the highest amount possible
        /// will be deleted until the initial calculated amountToDelete is 0.
        ///  <para/>
        /// Example: <para/>
        /// If you have 50 000 000 as MaxRows defined. Then 70% would be 35 000 000 rows. So 15 000 000 would have to be deleted.
        /// If youre _maxDeleteRowSize is 100 000 this would mean 150 turns every 5 seconds. This would take then round about 12,5 minutes to delete all of them.
        /// If you calculated with 500 entries per second. This would only mean 375 000 entries in these 12,5 minutes.
        /// So this should be enough time to delete all entries with plenty of time in between for other sources to write to the database.
        /// </summary>
        /// <param name="tables">All saved tables.</param>
        public override void CheckDeleteTables(ConcurrentDictionary<string, Table> tables)
        {
            try
            {
                foreach (Table table in tables.Values)
                {
                    // Get the current row value
                    long result = GetCurrentRowsFromTable(table);

                    if (result >= table.MaxRows)
                    {
                        // Calculate 70% and the amount to delete
                        double seventyPercent = (double)table.MaxRows * (double)0.7;
                        long amountToDelete = result - (long)Math.Round(seventyPercent);

                        string text = "Started to delete entries on table: " + table.TableName + Environment.NewLine;
                        text += "Current Rows: " + result + " | Max table rows: " + table.MaxRows + " | Amount to delete: " + amountToDelete;
                        OnDeleteEvent(text);

                        while (amountToDelete > 0)
                        {
                            long rows;
                            if (amountToDelete > MaxDeleteRowSize)
                            {
                                rows = MaxDeleteRowSize;
                            }
                            else
                                rows = amountToDelete;

                            amountToDelete -= rows;

                            SQLQueryBuilder deleteQuery = new SQLQueryBuilder();
                            string columnName = table.Columns.First().Key;
                            string param = deleteQuery.Select().AddValue(columnName).From().AddValue(table.TableName).OrderBY().
                                AddValue(columnName).Asc().Limit().AddValue(rows.ToString()).Flush();
                            deleteQuery.Delete().From().AddValue(table.TableName).Where().AddValue(columnName).In().Brackets(param);

                            CommitQuery(deleteQuery.ToString());
                            Thread.Sleep(5000);
                        }
                        OnDeleteEvent("Finished the Deletion of the table: " + table.TableName);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Gets the current rows from the table. Since this is accomplished with a trigger table its just a small querie.
        /// </summary>
        /// <param name="table">The table to get the current rows from.</param>
        /// <returns>Returns the number of rows in the given table</returns>
        public override int GetCurrentRowsFromTable(Table table)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                sqb.Select().AddValue("rowCount").From().AddValue(table.TableName + "_count").Where().AddValue("id").Equal().AddValue("1");
                List<List<object>> result = ReadQuery(sqb.ToString(),
                    new List<KeyValuePair<int, Type>>()
                    {
                        new KeyValuePair<int, Type>(0, typeof(int))
                    });

                if (result == null)
                    return 0;
                else
                    return (int)result[0][0];
            }
            catch (Exception)
            {
                throw;
            }
        }

        #region Low Level Methods
        /// <summary>
        /// A method for queries which don't need an answer or result.
        /// Like insert, update, create etc.
        /// </summary>
        /// <param name="query"></param>
        public override void CommitQuery(string query)
        {
            try
            {
                using (SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString))
                {
                    sQLiteConnection.Open();
                    using (var cmd = new SQLiteCommand(query, sQLiteConnection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    sQLiteConnection.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// A method for queries which give an answer or a result.
        /// Like Select. With this method it's also possible to define the columns which are needed for the given table.
        /// For Each row the needed column is seperated with '|'.
        /// At the moment only string and int64 values are implemented. Every other type is ignored.
        /// </summary>
        /// <param name="query">The select query.</param>
        /// <param name="columns">A list of pairs witch asks for the the given column and type of column in the order of the list.</param>
        /// <returns></returns>
        public override List<List<object>> ReadQuery(string query, List<KeyValuePair<int, Type>> columns)
        {
            try
            {
                using (SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString))
                {
                    sQLiteConnection.Open();
                    using (var cmd = new SQLiteCommand(query, sQLiteConnection))
                    {
                        using (var rdr = cmd.ExecuteReader())
                        {
                            List<List<object>> result = new List<List<object>>();
                            while (rdr.Read())
                            {
                                List<object> row = new List<object>();
                                foreach (KeyValuePair<int, Type> column in columns)
                                {
                                    if (column.Value == typeof(int))
                                        row.Add(rdr.GetInt32(column.Key));
                                    else if (column.Value == typeof(string))
                                        row.Add(rdr.GetString(column.Key));
                                    else if (column.Value == typeof(double))
                                        row.Add(rdr.GetDouble(column.Key));
                                    else if (column.Value == typeof(float))
                                        row.Add(rdr.GetFloat(column.Key));
                                    else if (column.Value == typeof(DateTime))
                                        row.Add(rdr.GetDateTime(column.Key));
                                    else
                                        throw new NotSupportedException(column.Value.Name + " Datatype not supported");
                                }
                                result.Add(row);
                            }
                            sQLiteConnection.Close();
                            return result;
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// This is an asynchron version of the <see cref="ReadQuery(string, List{KeyValuePair{int, Type}})"/> method.
        /// The answer is send through an event in the given fetchsize.
        /// </summary>
        /// <param name="query">The select query.</param>
        /// <param name="columns">A list of pairs witch asks for the the given column and type of column in the order of the list.</param>
        /// <param name="fetchsize">The fetch size defines the length of the list which is sent through an event.</param>
        public override void FetchQuery(string query, List<KeyValuePair<int, Type>> columns, int fetchsize)
        {
            _cancelFetch = false;
            FetchThread = new Thread(() =>
            {
                try
                {
                    using (SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString))
                    {
                        sQLiteConnection.Open();
                        using (var cmd = new SQLiteCommand(query, sQLiteConnection))
                        {
                            using (var rdr = cmd.ExecuteReader())
                            {
                                List<List<object>> result = new List<List<object>>();
                                int currentFetchSize = 1;
                                while (rdr.Read())
                                {
                                    if (_cancelFetch)
                                        return;

                                    List<object> row = new List<object>();
                                    foreach (KeyValuePair<int, Type> column in columns)
                                    {
                                        if (column.Value == typeof(int))
                                            row.Add(rdr.GetInt64(column.Key));
                                        else if (column.Value == typeof(string))
                                            row.Add(rdr.GetString(column.Key));
                                        else if (column.Value == typeof(double))
                                            row.Add(rdr.GetDouble(column.Key));
                                        else if (column.Value == typeof(float))
                                            row.Add(rdr.GetFloat(column.Key));
                                        else if (column.Value == typeof(DateTime))
                                            row.Add(rdr.GetDateTime(column.Key));
                                        else
                                            throw new NotSupportedException(column.Value.Name + " Datatype not supported");
                                    }
                                    result.Add(row);

                                    if (currentFetchSize >= fetchsize)
                                    {
                                        OnSQLiteFetch(new FetchArgs(result));
                                        result.Clear();
                                        currentFetchSize = 0;
                                        mrse.WaitOne();
                                    }
                                    currentFetchSize++;
                                }
                                OnSQLiteFetch(new FetchArgs(result));
                            }
                        }
                        sQLiteConnection.Close();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            });
            FetchThread.Start();
        }

        /// <summary>
        /// This command starts the FetchThread again and you will get the next fetch of the data.
        /// </summary>
        public override void NextFetch()
        {
            try
            {
                mrse.Set();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// This command cancels the current fetch command.
        /// </summary>
        public override void CancelFetch()
        {
            _cancelFetch = true;
        }

        /// <summary>
        /// Commits all queries at once.
        /// </summary>
        /// <param name="queryList">The list with all queries to execute.</param>
        public override void CommitBatchQuery(List<string> queryList)
        {
            try
            {
                using (SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString))
                {
                    sQLiteConnection.Open();
                    using (var cmd = new SQLiteCommand(sQLiteConnection))
                    {
                        using (var transaction = sQLiteConnection.BeginTransaction())
                        {
                            foreach (string query in queryList)
                            {
                                cmd.CommandText = query;
                                cmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }
                    }
                    sQLiteConnection.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Checks if the Connection to the DataBase could be established.
        /// </summary>
        /// <returns>Returns true if the connection could be established otherwise false.</returns>
        public override bool CheckDBStatus()
        {
            try
            {
                using (SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString))
                {
                    sQLiteConnection.Open();
                    Thread.Sleep(10);
                    sQLiteConnection.Close();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion
        #endregion

        #region Private Methods
        /// <summary>
        /// Generates a querie list to create tables.
        /// <para/>
        /// SQL query Example: <para/>
        /// CREATE TABLE table1 (id INTEGER PRIMARY KEY, firstColumn TEXT NOT NULL, secondColumn REAL NOT NULL)
        /// </summary>
        /// <returns>Returns a list of queries to create the local tables.</returns>
        private List<string> CreateTableQueries(ConcurrentDictionary<string, Table> tables)
        {
            try
            {
                List<string> tableQueries = new List<string>();
                foreach (KeyValuePair<string, Table> table in tables)
                {
                    SQLQueryBuilder sqb = new SQLQueryBuilder();
                    List<string> paramList = new List<string>();

                    int iter = 0;
                    foreach (KeyValuePair<string, Type> column in table.Value.Columns)
                    {
                        sqb.AddValue(column.Key);
                        if (column.Value == typeof(int))
                            sqb.TypeInteger();
                        else if (column.Value == typeof(string))
                            sqb.TypeText();
                        else if (column.Value == typeof(double))
                            sqb.TypeReal();
                        else if (column.Value == typeof(float))
                            sqb.TypeReal();
                        else if (column.Value == typeof(DateTime))
                            sqb.TypeText();
                        else
                            throw new NotSupportedException(column.Value.Name + " Datatype not supported");

                        if (iter.Equals(0))
                            sqb.ParamPrimaryKey();
                        else
                            sqb.ParamNot().Null();

                        paramList.Add(sqb.Flush());
                        iter++;
                    }
                    string values = sqb.Brackets_Multiple(paramList, false).Flush();
                    sqb.Create().Table().IfNotExists().AddValue(table.Value.TableName).AddValue(values);
                    tableQueries.Add(sqb.ToString());
                    tableQueries.Add(CreateTriggerTable(table.Value.TableName));
                    tableQueries.Add(InsertStartValueTriggerTable(table.Value.TableName));
                    tableQueries.Add(CreateCounterAddTriger(table.Value.TableName));
                    tableQueries.Add(CreateCounterSubTriger(table.Value.TableName));
                }
                return tableQueries;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Creates the trigger table where the row count is automatically update through a custom insert and delete trigger.
        /// <para/>
        /// SQL query Example: <para/>
        /// create table if not exists tableName_count (id integer primary key, number int);
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>The finished sql string.</returns>
        private string CreateTriggerTable(string tableName)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                string param = sqb.Brackets(sqb.AddValue("id").TypeInteger().ParamPrimaryKey().Comma().AddValue("rowCount").TypeInteger().Flush()).Flush();
                sqb.Create().Table().IfNotExists().AddValue(tableName + "_count").AddValue(param);
                return sqb.ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Inserts the first and only row of this table. Bascially its set's the rowCOunt to 0.
        /// <para/>
        /// SQL query Example: <para/>
        /// insert into table1Count (rowCount) values (0);
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>The finished sql string.</returns>
        private string InsertStartValueTriggerTable(string tableName)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                string param = sqb.AddValue("id").Comma().AddValue("rowCount").Flush();
                sqb.Insert().Or().Ignore().Into().AddValue(tableName + "_count").Brackets(param).Values().Brackets("1, 0");
                return sqb.ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Creates the insert trigger to keep track of the row count.
        /// Basically every insert command adds +1 to the row count.
        /// <para/>
        /// SQL query Example: <para/>
        /// create trigger if not exists table_trigger_add after insert on table BEGIN update table_count set rowCount = rowCount + 1 where id = 1; END
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>The finished sql string.</returns>
        private string CreateCounterAddTriger(string tableName)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                sqb.Create().Trigger().IfNotExists().AddValue(tableName + "_trigger_add").After().Insert().On().AddValue(tableName);
                sqb.Begin().Update().AddValue(tableName + "_count").Set().AddValue("rowCount").Equal().AddValue("rowCount + 1");
                sqb.Where().AddValue("id").Equal().AddValue("1").CommaPoint(true).End();
                return sqb.ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Creates the delete trigger to keep track of the row count.
        /// Basically every delete command subtracts +1 to the row count.
        /// <para/>
        /// SQL query Example: <para/>
        /// create trigger if not exists table_trigger_sub after delete on table BEGIN update table_count set rowCount = rowCount - 1 where id = 1; END
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>The finished sql string.</returns>
        private string CreateCounterSubTriger(string tableName)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                sqb.Create().Trigger().IfNotExists().AddValue(tableName + "_trigger_sub").After().Delete().On().AddValue(tableName);
                sqb.Begin().Update().AddValue(tableName + "_count").Set().AddValue("rowCount").Equal().AddValue("rowCount - 1");
                sqb.Where().AddValue("id").Equal().AddValue("1").CommaPoint(true).End();
                return sqb.ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Gets the tableName and returns the type list for all columns.
        /// </summary>
        /// <param name="table">The table to create a type list.</param>
        /// <returns>returns a list for the types from the given tableName.</returns>
        private List<KeyValuePair<int, Type>> GenerateOutputValuesFromTable(Table table)
        {
            try
            {
                List<KeyValuePair<int, Type>> columnsToGet = new List<KeyValuePair<int, Type>>();
                Dictionary<string, Type> columns = table.Columns;

                int counter = 0;
                foreach (Type type in columns.Values)
                {
                    KeyValuePair<int, Type> keyValuePair = new KeyValuePair<int, Type>(counter, type);
                    columnsToGet.Add(keyValuePair);
                    counter++;
                }

                return columnsToGet;
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Event Methods
        /// <summary>
        /// The method to vinvoke this event.
        /// </summary>
        /// <param name="e">The given argument class (<see cref="FetchArgs"/>)</param>
        /// <returns></returns>
        protected virtual int OnSQLiteFetch(FetchArgs e)
        {
            try
            {
                FetchEvent?.Invoke(this, e);
                return 0;
            }
            catch (Exception)
            {
                throw;
            }
        }
        /// <summary>
        /// The method to invoke this event.
        /// </summary>
        /// <param name="text"></param>
        protected virtual void OnDeleteEvent(string text)
        {
            try
            {
                DeleteEvent?.Invoke(this, text);
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion
    }
}
