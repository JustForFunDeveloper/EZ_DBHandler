using ADOX;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Threading;

namespace EZ_DBHandler.DataBaseHandler.Access
{
    public class AccessHandler : AbstractDBHandler
    {
        #region Private Members
        private string _connectionString;
        private string _stringFormat = "yyyy-MM-dd HH:mm:ss";
        private Thread FetchThread;
        private Boolean _cancelFetch;
        private Dictionary<string, Thread> _tableDeleteThreads = new Dictionary<string, Thread>();
        /// <summary>
        /// This value is used to stop the thread after every fetch and wait for the mrse.Set() command.
        /// </summary>
        private ManualResetEvent mrse = new ManualResetEvent(false);
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
        public AccessHandler(string databaseName, string connectionPath)
        {
            try
            {
                ChangeConnectionString(databaseName, connectionPath);
            }
            catch (Exception e)
            {
                throw e;
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
                _connectionString =
                    @"Provider=Microsoft.Jet.OLEDB.4.0;" +
                    @"Data Source=" + connectionPath + "\\" + databaseName + ";" +
                    @"User Id=;Password=;";

                if (!File.Exists(connectionPath + "\\" + databaseName))
                {
                    var cat = new Catalog();
                    cat.Create(_connectionString);
                }
                GetTablesfromDatabase();
            }
            catch (Exception e)
            {
                throw e;
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
            catch (Exception e)
            {
                throw e;
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
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Drops the tables with the given table names.
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
                    sqb.DropTable().AddValue(tableName);
                    tableQueries.Add(sqb.ToString());
                }
                CommitBatchQuery(tableQueries);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Inserts the given objects into the given table. The first object "id" is ignored due to the auto increment,
        /// <para/>
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
                            sqb.Tags(sqb.AddValue(convertTime.ToString(_stringFormat)).Flush());
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
            catch (Exception e)
            {
                throw e;
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
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Get's the last n rows from the specified table.
        /// <para/>
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

                if (ascending)
                    sqb.Select().Top().AddValue("5").ALL().From().AddValue(table.TableName).OrderBY().AddValue(table.Columns.First().Key).Asc();
                else
                    sqb.Select().Top().AddValue("5").ALL().From().AddValue(table.TableName).OrderBY().AddValue(table.Columns.First().Key).Desc();

                List<List<object>> results = ReadQuery(sqb.ToString(), GenerateOutputValuesFromTable(table));
                return results;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Gets all rows in the given DateTime slot.
        /// <para/>
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
                string stringFrom = sqb.Tags(from.ToString(_stringFormat)).Flush();
                string stringUntil = sqb.Tags(until.ToString(_stringFormat)).Flush();

                sqb.Select().ALL().From().AddValue(table.TableName).Where().AddValue(DateTimeColumnName);
                sqb.GreaterThen().AddValue(stringFrom).AND().AddValue(DateTimeColumnName).LesserThen().AddValue(stringUntil);

                if (!ascending)
                    sqb.OrderBY().AddValue(DateTimeColumnName).Desc();

                List<List<object>> results = ReadQuery(sqb.ToString(), GenerateOutputValuesFromTable(table));
                return results;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Gets all rows in the given id slot.
        /// <para/>
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
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Deletes the last n rows of the given table.
        /// </summary>
        /// <param name="table">The table to delete the last n data from.</param>
        /// <param name="rows">The amount of data to delete.</param>
        public override void DeleteLastNRows(Table table, int rows)
        {
            // TODO: Check Query
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                string columnName = table.Columns.First().Key;
                string param = sqb.Select().Top().AddValue(rows.ToString()).AddValue(columnName).From().AddValue(table.TableName)
                    .OrderBY().AddValue(columnName).Desc().Flush();
                sqb.Delete().From().AddValue(table.TableName).Where().AddValue(columnName).In().Brackets(param);

                CommitQuery(sqb.ToString());
            }
            catch (Exception e)
            {
                throw e;
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

                            SQLQueryBuilder sqb = new SQLQueryBuilder();
                            string columnName = table.Columns.First().Key;
                            string param = sqb.Select().Top().AddValue(rows.ToString()).AddValue(columnName).From().AddValue(table.TableName)
                                .OrderBY().AddValue(columnName).Desc().Flush();
                            sqb.Delete().From().AddValue(table.TableName).Where().AddValue(columnName).In().Brackets(param);

                            CommitQuery(sqb.ToString());
                            Thread.Sleep(5000);
                        }
                        OnDeleteEvent("Finished the Deletion of the table: " + table.TableName);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Gets the current rows from the table. There are no triggers in access so i had to do it with the count method.
        /// </summary>
        /// <param name="table">The table to get the current rows from.</param>
        /// <returns>Returns the number of rows in the given table</returns>
        public override int GetCurrentRowsFromTable(Table table)
        {
            try
            {
                SQLQueryBuilder sqb = new SQLQueryBuilder();
                string columnName = table.Columns.First().Key;
                sqb.Select().AddValue("Count(" + columnName + ")").From().AddValue(table.TableName);
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
            catch (Exception e)
            {
                throw e;
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
                using (OleDbConnection oleDbConnection = new OleDbConnection(_connectionString))
                {
                    oleDbConnection.Open();
                    using (var cmd = new OleDbCommand(query, oleDbConnection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    oleDbConnection.Close();
                }
            }
            catch (Exception e)
            {
                throw e;
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
                using (OleDbConnection oleDbConnection = new OleDbConnection(_connectionString))
                {
                    using (var cmd = new OleDbCommand(query, oleDbConnection))
                    {
                        cmd.Connection = oleDbConnection;
                        oleDbConnection.Open();
                        using (OleDbDataReader rdr = cmd.ExecuteReader())
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
                                        row.Add(rdr.GetFloat(column.Key));
                                    else if (column.Value == typeof(float))
                                        row.Add(rdr.GetFloat(column.Key));
                                    else if (column.Value == typeof(DateTime))
                                        row.Add(rdr.GetDateTime(column.Key));
                                    else
                                        throw new NotSupportedException(column.Value.Name + " Datatype not supported");
                                }
                                result.Add(row);
                            }
                            rdr.Close();
                            oleDbConnection.Close();
                            return result;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
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
                    using (OleDbConnection oleDbConnection = new OleDbConnection(_connectionString))
                    {
                        using (var cmd = new OleDbCommand(query, oleDbConnection))
                        {
                            cmd.Connection = oleDbConnection;
                            oleDbConnection.Open();
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
                                        OnFetch(new FetchArgs(result));
                                        result.Clear();
                                        currentFetchSize = 0;
                                        mrse.WaitOne();
                                    }
                                    currentFetchSize++;
                                }
                                OnFetch(new FetchArgs(result));
                                rdr.Close();
                            }
                        }
                        oleDbConnection.Close();
                    }
                }
                catch (Exception e)
                {
                    throw e;
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
            catch (Exception e)
            {
                throw e;
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
            OleDbTransaction transaction = null;
            try
            {
                using (OleDbConnection oleDbConnection = new OleDbConnection(_connectionString))
                {
                    using (var cmd = new OleDbCommand())
                    {
                        cmd.Connection = oleDbConnection;
                        oleDbConnection.Open();

                        transaction = oleDbConnection.BeginTransaction();
                        cmd.Connection = oleDbConnection;
                        cmd.Transaction = transaction;
                        foreach (string query in queryList)
                        {
                            cmd.CommandText = query;
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                    oleDbConnection.Close();
                }
            }
            catch (Exception e)
            {
                try
                {
                    // Attempt to roll back the transaction.
                    transaction.Rollback();
                    throw e;
                }
                catch
                {
                    // Do nothing here; transaction is not active.
                }
                finally
                {
                    throw e;
                }
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
                using (OleDbConnection oleDbConnection = new OleDbConnection(_connectionString))
                {
                    oleDbConnection.Open();
                    Thread.Sleep(10);
                    oleDbConnection.Close();
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

                        if (iter.Equals(0))
                        {
                            sqb.AddValue("AUTOINCREMENT").ParamPrimaryKey();
                            paramList.Add(sqb.Flush());
                            iter++;
                            continue;
                        }

                        if (column.Value == typeof(int))
                            sqb.TypeInteger();
                        else if (column.Value == typeof(string))
                            sqb.TypeText();
                        else if (column.Value == typeof(double))
                            sqb.TypeReal();
                        else if (column.Value == typeof(float))
                            sqb.TypeReal();
                        else if (column.Value == typeof(DateTime))
                            sqb.TypeDateTime();
                        else
                            throw new NotSupportedException(column.Value.Name + " Datatype not supported");

                        sqb.ParamNot().Null();

                        paramList.Add(sqb.Flush());
                        iter++;
                    }
                    string values = sqb.Brackets_Multiple(paramList, false).Flush();
                    sqb.Create().Table().AddValue(table.Value.TableName).AddValue(values);
                    tableQueries.Add(sqb.ToString());
                }
                return tableQueries;
            }
            catch (Exception e)
            {
                throw e;
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
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion

        #region Event Methods
        /// <summary>
        /// The method to vinvoke this event.
        /// </summary>
        /// <param name="e">The given argument class (<see cref="FetchArgs"/>)</param>
        /// <returns></returns>
        protected virtual int OnFetch(FetchArgs e)
        {
            try
            {
                FetchEvent?.Invoke(this, e);
                return 0;
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (Exception e)
            {
                throw e;
            }
        }

        private void GetTablesfromDatabase()
        {
            DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.OleDb");

            using (DbConnection connection = factory.CreateConnection())
            {
                connection.ConnectionString = _connectionString;

                connection.Open();

                // First, get schema information of all the tables in current database;  
                string[] Tablesrestrictions = new string[4];
                Tablesrestrictions[3] = "Table";
                DataTable allTablesSchemaTable = connection.GetSchema("Tables", Tablesrestrictions);

                List<string> tableNames = new List<string>();
                for (int i = 0; i < allTablesSchemaTable.Rows.Count; i++)
                    tableNames.Add(allTablesSchemaTable.Rows[i][2].ToString());

                foreach (var table in tableNames)
                {
                    string[] restrictions = new string[4];
                    restrictions[2] = table;
                    // First, get schema information of all the columns in current database.  
                    DataTable allColumnsSchemaTable = connection.GetSchema("Columns", restrictions);

                    Console.WriteLine("Schema Information of All Columns:");
                    ShowColumns(allColumnsSchemaTable);
                    Console.WriteLine();
                }
            }
        }
        #endregion

        private void ShowColumns(DataTable columnsTable)
        {
            var selectedRows = from info in columnsTable.AsEnumerable()
                               select new
                               {
                                   TableName = info["TABLE_NAME"],
                                   ColumnName = info["COLUMN_NAME"],
                                   DataType = info["DATA_TYPE"],
                                   OrdinalPosition = info["ORDINAL_POSITION"]
                               };

            Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}",
                "TABLE_NAME", "COLUMN_NAME", "DATA_TYPE", "ORDINAL_POSITION");
            foreach (var row in selectedRows)
            {
                Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}", row.TableName, row.ColumnName, (OleDbType)row.DataType, row.OrdinalPosition);
            }
        }
    }
}
