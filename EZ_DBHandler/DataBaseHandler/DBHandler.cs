using EZ_DBHandler.DataBaseHandler.Access;
using EZ_DBHandler.DataBaseHandler.MySQL;
using EZ_DBHandler.DataBaseHandler.SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace EZ_DBHandler.DataBaseHandler
{
    /// <summary>
    /// A class to define the tables in a database.
    /// </summary>
    public class Table
    {
        /// <summary>
        /// The table name.
        /// </summary>
        public string TableName;
        /// <summary>
        /// A list of all columns with the prefered data type.
        /// </summary>
        public Dictionary<string, Type> Columns;
        /// <summary>
        /// Max allowed rows for this table. The DBHandler clean thread will delete a percentage of the old rows.
        /// </summary>
        public long MaxRows;

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="tableName"><see cref="TableName"/></param>
        /// <param name="columns"><see cref="Columns"/></param>
        /// <param name="maxRows"><see cref="MaxRows"/></param>
        public Table(string tableName, Dictionary<string, Type> columns, long maxRows)
        {
            TableName = tableName;
            Columns = columns;
            MaxRows = maxRows;
        }

        /// <summary>
        /// Createas the columns from the given classType.
        /// </summary>
        /// <param name="classType"></param>
        /// <param name="maxRows"></param>
        /// <param name="hasBufferedTable"></param>
        public Table(Type classType, long maxRows, bool hasBufferedTable = false)
        {
            TableName = classType.Name;
            Columns = CreateTableFromDataModel(classType);
            MaxRows = maxRows;
        }

        /// <summary>
        /// Creates the Columns from the classType.
        /// </summary>
        /// <param name="classType"></param>
        /// <returns>Returns all columns created from the classType.</returns>
        private Dictionary<string, Type> CreateTableFromDataModel(Type classType)
        {
            try
            {
                Dictionary<string, Type> columns = new Dictionary<string, Type>();

                //check if properties are available, if not use class fields
                PropertyInfo[] properties = classType.GetProperties();
                if (properties.Length > 0)
                {
                    foreach (PropertyInfo property in properties)
                        columns.Add(property.Name, property.PropertyType);
                }
                else
                {
                    FieldInfo[] fields = classType.GetFields();
                    foreach (FieldInfo field in fields)
                        columns.Add(field.Name, field.FieldType);
                }

                return columns;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Enum to declare the db type.
    /// </summary>
    public enum DataBaseType
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        SQLITE,
        MSSQL,
        MYSQL,
        ACCESS
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }

    /// <summary>
    /// An easy to use DBHandler class which allows to choose between multiple Database Types.
    /// Features:  
    /// -> Automatic Trigger Tables to hold a count for all rows in the table.
    /// -> Automatic creation of the database and tables based on the given Tables.
    /// -> Easy to use queries with the SQLQueryBuilder.
    /// -> Added basic MultiThread functionality
    /// 
    /// TODO: Not implemented / Future Features?:
    /// -> Indexes not used at the moment.
    /// -> Linq instead of SQLQueryBuilder.
    /// -> Entity Framework.
    /// -> Fetch isn't probably Thread Safe yet!
    /// -> Parametrized Queries to be safe from sql injection.
    /// </summary>
    public class DBHandler
    {
        #region Private Members
        private AbstractDBHandler _abstractDBHandler;
        private string _dbName;
        private string _dbPath;
        private string _fileExtension;
        private string _user;
        private string _password;
        private string _port;
        private ConcurrentDictionary<string, Table> _tables;
        private DataBaseType _dataBaseType;
        private readonly string SQLiteFileExtension = ".db";
        private readonly string AccessFileExtension = ".accdb";

        private Timer _dbStatusTimer;
        private bool _dbStatus = false;
        private int _dbStatusIntervall = 5 * 1000;

        private Timer _dbDeleteTimer;
        private int _dbDeleteIntervall;
        #endregion

        #region Public Members
        /// <summary>
        /// The name of the current database.
        /// </summary>
        public string DbName
        {
            get => _dbName;
            set
            {
                _dbName = value;
                ChangeConnectionString();
            }
        }
        /// <summary>
        /// Path for the current DB File.
        /// SQLite Only!
        /// </summary>
        public string DbPath
        {
            get => _dbPath;
            set
            {
                _dbPath = value;
                ChangeConnectionString();
            }
        }
        /// <summary>
        /// Getter for the current File Extension.
        /// </summary>
        public string FileExtension { get => _fileExtension; }
        /// <summary>
        /// Username for the database.
        /// MSSQL und MYSQL only!
        /// </summary>
        public string User
        {
            get => _user;
            set
            {
                _user = value;
                ChangeConnectionString();
            }
        }
        /// <summary>
        /// Password for the current DB File.
        /// SQLite Only!
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                ChangeConnectionString();
            }
        }
        /// <summary>
        /// Port for the current DB File.
        /// SQLite Only!
        /// </summary>
        public string Port
        {
            get => _port;
            set
            {
                _port = value;
                ChangeConnectionString();
            }
        }
        /// <summary>
        /// A Dictionary of tables whch should be created in the database if necessary.
        /// </summary>
        public ConcurrentDictionary<string, Table> Tables { get => _tables; }
        /// <summary>
        /// The current database type.
        /// </summary>
        public DataBaseType DataBaseType { get => _dataBaseType; }
        /// <summary>
        /// The maximum size of rows which should be deleted at once.
        /// </summary>
        public long MaxDeleteRowSize { get => _abstractDBHandler.MaxDeleteRowSize; set => _abstractDBHandler.MaxDeleteRowSize = value; }

        /// <summary>
        /// Subscription for the FetchEevents when <see cref="FetchQuery(string, List{KeyValuePair{int, Type}}, int)"/> is used.
        /// </summary>
        public event EventHandler<FetchArgs> FetchEvent
        {
            add
            {
                if (_abstractDBHandler != null)
                    _abstractDBHandler.FetchEvent += value;
                else
                    throw new NullReferenceException("SQLiteHandler not instantiated");
            }
            remove
            {
                if (_abstractDBHandler != null)
                    _abstractDBHandler.FetchEvent -= value;
                else
                    throw new NullReferenceException("SQLiteHandler not instantiated");
            }
        }
        /// <summary>
        /// An event which is invoked as soon a exception occured.<para/>
        /// Used to log Exceptions in this Handler.
        /// </summary>
        public event EventHandler<Exception> ExceptionEvent;
        /// <summary>
        /// Is raised if the status of the database changes.
        /// </summary>
        public event EventHandler<bool> DataBaseStatusEvent;
        /// <summary>
        /// Is Raised if a delete event is occuring.
        /// Sends some basic data to the table on which a deletion of rows is performed.
        /// </summary>
        public event EventHandler<string> DeleteEvent
        {
            add
            {
                if (_abstractDBHandler != null)
                    _abstractDBHandler.DeleteEvent += value;
                else
                    throw new NullReferenceException("SQLiteHandler not instantiated");
            }
            remove
            {
                if (_abstractDBHandler != null)
                    _abstractDBHandler.DeleteEvent -= value;
                else
                    throw new NullReferenceException("SQLiteHandler not instantiated");
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Basic constructor.
        /// MySQL standard values:
        /// connectionPath = "localhost" | user = "root" | password = "root" | port = "3306"
        /// </summary>
        /// <param name="dbName">The name of the database without any file extensions<see cref="DbName"/></param>
        /// <param name="dbPath"><see cref="DbPath"/></param>
        /// <param name="dataBaseType"><see cref="DataBaseType"/></param>
        /// <param name="createIfNotExists">If true creates a database if it doesn't exist allready.</param>
        public DBHandler(string dbName, string dbPath, DataBaseType dataBaseType, bool createIfNotExists = true)
        {
            try
            {
                CheckDbName(dbName);
                CheckDbPath(dbPath, dataBaseType);
                SetExtension(dataBaseType);
                _dbName = dbName;
                _dbPath = dbPath;
                _dataBaseType = dataBaseType;

                // Set standard values
                _user = "root";
                _password = "root";
                _port = "3306";
                _tables = new ConcurrentDictionary<string, Table>();

                ConnectToDatabase(DataBaseType, createIfNotExists);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Basic constructor with connectionPath and user credentials.
        /// </summary>
        /// <param name="dbName">The name of the database without any file extensions<see cref="DbName"/></param>
        /// <param name="dbPath"><see cref="DbPath"/></param>
        /// <param name="dataBaseType"><see cref="DataBaseType"/></param>
        /// <param name="createIfNotExists">If true creates a database if it doesn't exist allready.</param>
        /// <param name="user">The user for the database credentials.</param>
        /// <param name="password">The pwassword for the database credentials.</param>
        /// <param name="port">The port to connect to the database.</param>
        public DBHandler(string dbName, string dbPath, string user, string password, string port, DataBaseType dataBaseType, bool createIfNotExists = true)
        {
            try
            {
                CheckDbName(dbName);
                CheckDbPath(dbPath, dataBaseType);
                SetExtension(dataBaseType);
                _dbName = dbName;
                _dbPath = dbPath;
                _user = user;
                _password = password;
                _port = port;
                _dataBaseType = dataBaseType;
                _tables = new ConcurrentDictionary<string, Table>();

                ConnectToDatabase(DataBaseType, createIfNotExists);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Basic constructor and creates the given tables.
        /// MySQL standard values:
        /// connectionPath = "localhost" | user = "root" | password = "root" | port = "3306"
        /// </summary>
        /// <param name="dbName"><see cref="DbName"/></param>
        /// <param name="dbPath"><see cref="DbPath"/></param>
        /// <param name="tables"><see cref="Tables"/></param>
        /// <param name="dataBaseType"><see cref="DataBaseType"/></param>
        /// <param name="createIfNotExists">If true creates a database if it doesn't exist allready.</param>
        public DBHandler(string dbName, string dbPath, List<Table> tables, DataBaseType dataBaseType, bool createIfNotExists = true)
        {
            try
            {
                CheckDbName(dbName);
                SetExtension(dataBaseType);
                CheckDbPath(dbPath, dataBaseType);
                _dbName = dbName;
                _dbPath = dbPath;
                _tables = new ConcurrentDictionary<string, Table>();
                foreach (var table in tables)
                {
                    if (!_tables.TryAdd(table.TableName, table))
                        throw new MemberAccessException("Couldn't add table: " + table.TableName);
                }
                _dataBaseType = dataBaseType;

                // Set standard values
                _user = "root";
                _password = "root";
                _port = "3306";

                ConnectToDatabase(DataBaseType, createIfNotExists);
                _abstractDBHandler.CreateTables(_tables);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Adds a table and creates them at the given database.
        /// Multiple tables for a better performance should be created  with the <see cref="AddTables(List{Table})"/> method.
        /// </summary>
        /// <param name="table">The table to add to the database.</param>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool AddTable(Table table)
        {
            try
            {
                if (!_tables.TryAdd(table.TableName, table))
                    throw new MemberAccessException("Couldn't add table: " + table.TableName);
                _abstractDBHandler.AddTables(_tables);
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Adds tables and creates them at the given database.
        /// </summary>
        /// <param name="tables">A list of tables to add to the database.</param>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool AddTables(List<Table> tables)
        {
            try
            {
                foreach (Table table in tables)
                {
                    if (!_tables.TryAdd(table.TableName, table))
                        throw new MemberAccessException("Couldn't add table: " + table.TableName);
                }
                _abstractDBHandler.AddTables(_tables);
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Drops the tables with the given table names.
        /// </summary>
        /// <param name="tableNames">A list of table names to delete.</param>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool DropTables(List<string> tableNames)
        {
            try
            {
                foreach (var tableName in tableNames)
                {
                    if (!_tables.ContainsKey(tableName))
                        throw new KeyNotFoundException("Table doesn't exists. Drop table exited without changes to the database!");
                }
                foreach (var tableName in tableNames)
                {
                    if (!_tables.TryRemove(tableName, out Table table))
                        throw new MemberAccessException("Couldn't remove table: " + table.TableName);
                }
                _abstractDBHandler.DropTables(tableNames);
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Inserts the given objects into the given table. The first object "id" is ignored due to the auto increment,
        /// </summary>
        /// <param name="tableName">The name of the table to insert rows to.</param>
        /// <param name="rows">A list of rows with all column objects to insert.</param>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool InsertIntoTable(string tableName, List<List<object>> rows)
        {
            try
            {
                if (!_tables.ContainsKey(tableName))
                    throw new KeyNotFoundException("Table doesn't exist!");
                if (!rows.First().Count().Equals(_tables[tableName].Columns.Count))
                    throw new InvalidDataException("Table row size doesn't fit with the given table");

                _abstractDBHandler.InsertIntoTable(tableName, _tables, rows);
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Updates the given columns with the given id in the first column.
        /// Each row of rowsToUpdate must have the same size as rowsData.
        /// </summary>
        /// <param name="tableName">The table where rows should be updated.</param>
        /// <param name="rowsToUpdate">The rows with the name and data type to update.</param>
        /// <param name="rowsData">The rows with all column data which should be updated.</param>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool UpdateTable(string tableName, List<Dictionary<string, Type>> rowsToUpdate, List<List<object>> rowsData)
        {
            try
            {
                if (!_tables.ContainsKey(tableName))
                    throw new KeyNotFoundException("Table doesn't exist!");

                if (!rowsToUpdate.Count.Equals(rowsData.Count))
                    throw new InvalidDataException("rowsToUpdate size does'nt match rowsData size");

                _abstractDBHandler.UpdateTable(tableName, rowsToUpdate, rowsData);
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Get's the specified row from the specified table.
        /// </summary>
        /// <param name="tableName">The table name to get the row from.</param>
        /// <param name="rowId">The rowId to get the data from.</param>
        /// <returns>returns the specified row in a list otherwise empty List. Null if an error occured.</returns>
        public List<object> GetRowById(string tableName, int rowId)
        {
            try
            {
                List<List<object>> result = GetRowsFromTableWithIndex(tableName, rowId, rowId);
                if (result.Count <= 0)
                    return new List<object>();
                else
                    return result[0];
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return null;
            }
        }

        /// <summary>
        /// Get's the last row from the specified table.
        /// </summary>
        /// <param name="tableName">The table name to get the data from.</param>
        /// <returns>returns the last row in a list otherwise an empty list. Null if an error occured.</returns>
        public List<object> GetLastRowFromTable(string tableName)
        {
            try
            {
                List<List<object>> result = GetLastNRowsFromTable(tableName, 1);
                if (result.Count <= 0)
                    return new List<object>();
                else
                    return result[0];
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return null;
            }
        }

        /// <summary>
        /// Get's the last n rows from the specified table.
        /// </summary>
        /// <param name="tableName">The table name to get the data from.</param>
        /// <param name="rows">number of the rows to display.</param>
        /// <param name="ascending">Ascending or descending by first param.</param>
        /// <returns>returns the data in rows and in columns otherwise an empty list. Null if an error occured.</returns>
        public List<List<object>> GetLastNRowsFromTable(string tableName, int rows = 100, bool ascending = true)
        {
            try
            {
                if (!_tables.ContainsKey(tableName))
                    throw new KeyNotFoundException("Table doesn't exist!");

                Table table = _tables[tableName];

                return _abstractDBHandler.GetLastNRowsFromTable(table, rows, ascending);
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return null;
            }

        }

        /// <summary>
        /// Gets all rows in the given DateTime slot.
        /// </summary>
        /// <param name="tableName">The name of the table to get the data from.</param>
        /// <param name="DateTimeColumnName">The name of the column with the DateTime values.</param>
        /// <param name="from">A DateTime object with the beginning of the timeslot.</param>
        /// <param name="until">A DateTime object with the end of the timeslot.</param>
        /// <param name="ascending">Ascending or descending by DateTimeColumn param.</param>
        /// <returns>returns the data in rows and in columns otherwise an empty list. Null if an error occured.</returns>
        public List<List<object>> GetRowsFromTableWithTime(string tableName, string DateTimeColumnName, DateTime from, DateTime until, bool ascending = true)
        {
            try
            {
                if (!_tables.ContainsKey(tableName))
                    throw new KeyNotFoundException("Table doesn't exist!");

                Table table = _tables[tableName];

                if (table.Columns[DateTimeColumnName] != typeof(DateTime))
                    throw new TypeLoadException("DateTimeColumn isn't a DateTime column!");

                return _abstractDBHandler.GetRowsFromTableWithTime(table, DateTimeColumnName, from, until, ascending);
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return null;
            }
        }

        /// <summary>
        /// Gets all rows in the given id slot.
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="ascending">Ascending or descending by first param.</param>
        /// <returns>returns the data in rows and in columns otherwise an empty list. Null if an error occured.</returns>
        public List<List<object>> GetRowsFromTableWithIndex(string tableName, int start, int end, bool ascending = true)
        {
            try
            {
                if (!_tables.ContainsKey(tableName))
                    throw new KeyNotFoundException("Table doesn't exist!");

                Table table = _tables[tableName];

                return _abstractDBHandler.GetRowsFromTableWithIndex(table, start, end, ascending);
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return null;
            }
        }

        /// <summary>
        /// Deletes the last n rows of the given table.
        /// </summary>
        /// <param name="tableName">The name of the table to delete the last n data from.</param>
        /// <param name="rows">The amount of data to delete.</param>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool DeleteLastNRows(string tableName, int rows)
        {
            try
            {
                if (!_tables.ContainsKey(tableName))
                    throw new KeyNotFoundException("Table doesn't exist!");

                if (rows > _abstractDBHandler.MaxDeleteRowSize)
                    throw new InvalidDataException("rows max size: " + _abstractDBHandler.MaxDeleteRowSize);

                _abstractDBHandler.DeleteLastNRows(_tables[tableName], rows);
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Every x millis the CheckDeleteTables will be called and checked if the MaxRows value of the database was passed and a deletion will be started if necessary.
        /// </summary>
        /// <param name="millis">The interval of the check in milliseconds.</param>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool StartDeleteThread(int millis)
        {
            try
            {
                _dbDeleteIntervall = millis;
                if (_tables.Count <= 0)
                    throw new IndexOutOfRangeException("No Tables to check");
                _dbDeleteTimer = new Timer(OnDeleteCallback, null, 60000, Timeout.Infinite);
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Stops the delete timer and all active threads.
        /// </summary>
        /// <returns>False if the function wasn't succesfull.</returns>
        public bool StopDeleteThread()
        {
            try
            {
                if (_dbDeleteTimer != null)
                {
                    _dbDeleteTimer.Dispose();
                    _dbDeleteTimer = null;
                }
                return true;
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return false;
            }
        }

        /// <summary>
        /// Gets the current rows from the table. Since this is accomplished with a trigger table its just a small querie.
        /// </summary>
        /// <param name="tableName">The tableName to get the current rows from.</param>
        /// <returns>Returns the number of rows in the given table otherwise -1</returns>
        public int GetCurrentRowsFromTable(string tableName)
        {
            try
            {
                return _abstractDBHandler.GetCurrentRowsFromTable(_tables[tableName]);
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return -1;
            }
        }

        /// <summary>
        /// Gets all Columns of the specific table and returns the KeyValuePair representation.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>Returns the KeyValue Representation of all columns. Otherwise null.</returns>
        public List<KeyValuePair<int, Type>> GetAllColumnsFromTable(string tableName)
        {
            try
            {
                if (_tables.TryGetValue(tableName, out Table table))
                {
                    List<KeyValuePair<int, Type>> columns = new List<KeyValuePair<int, Type>>();
                    int iter = 0;
                    foreach (var item in table.Columns.Values)
                    {
                        columns.Add(new KeyValuePair<int, Type>(iter, item));
                        iter++;
                    }
                    return columns;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                return null;
            }
        }

        #region Low Level Methods
        /// <summary>
        /// <see cref="AbstractDBHandler.CommitQuery(string)"/>
        /// </summary>
        /// <param name="query"></param>
        public void CommitQuery(string query)
        {
            _abstractDBHandler.CommitQuery(query);
        }

        /// <summary>
        /// <see cref="AbstractDBHandler.CommitBatchQuery(List{string})"/>
        /// </summary>
        /// <param name="queryList"></param>
        public void CommitBatchQuery(List<string> queryList)
        {
            _abstractDBHandler.CommitBatchQuery(queryList);
        }

        /// <summary>
        /// <see cref="AbstractDBHandler.ReadQuery(string, List{KeyValuePair{int, Type}})"/>
        /// </summary>
        public List<List<object>> ReadQuery(string query, List<KeyValuePair<int, Type>> columns)
        {
            return _abstractDBHandler.ReadQuery(query, columns);
        }

        /// <summary>
        /// <see cref="AbstractDBHandler.FetchQuery(string, List{KeyValuePair{int, Type}}, int)"/>
        /// </summary>
        public void FetchQuery(string query, List<KeyValuePair<int, Type>> columns, int fetchsize)
        {
            _abstractDBHandler.FetchQuery(query, columns, fetchsize);
        }

        /// <summary>
        /// <see cref="AbstractDBHandler.CancelFetch"/>
        /// </summary>
        public void CancelFetch()
        {
            _abstractDBHandler.CancelFetch();
        }

        /// <summary>
        /// <see cref="AbstractDBHandler.NextFetch"/>
        /// </summary>
        public void NextFetch()
        {
            _abstractDBHandler.NextFetch();
        }

        /// <summary>
        /// Stops all Timers to be able to release all data.
        /// </summary>
        public void DisposeDBHandler()
        {
            try
            {
                if (_dbStatusTimer != null)
                {
                    _dbStatusTimer.Dispose();
                    _dbStatusTimer = null;
                }
                if (_dbDeleteTimer != null)
                {
                    _dbDeleteTimer.Dispose();
                    _dbDeleteTimer = null;
                }
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
            }
        }
        #endregion
        #endregion

        #region Private Methods
        /// <summary>
        /// Sets the correspondig File Extension.
        /// </summary>
        /// <param name="dataBaseType"></param>
        private void SetExtension(DataBaseType dataBaseType)
        {
            switch (dataBaseType)
            {
                case DataBaseType.SQLITE:
                    _fileExtension = SQLiteFileExtension;
                    break;
                case DataBaseType.MSSQL:
                    throw new NotSupportedException("MSSQL Database not implemented yet!");
                case DataBaseType.MYSQL:
                    _fileExtension = "";
                    break;
                case DataBaseType.ACCESS:
                    _fileExtension = AccessFileExtension;
                    break;
                default:
                    throw new NotSupportedException("Wrong Database type!");
            }
        }

        /// <summary>
        /// Checks the dbName for valid characters. Only alphanumeric and underscores are allowed.
        /// </summary>
        /// <param name="dbName"></param>
        private void CheckDbName(string dbName)
        {
            if (dbName.Equals(""))
                throw new ArgumentNullException("dbName is empty!");

            var regexItem = new Regex("^[a-zA-Z0-9_]*$");

            if (!regexItem.IsMatch(dbName))
                throw new ArgumentException("Invalid dbName! Only alphanumeric and underscores are allowed");
        }

        /// <summary>
        /// Checks the dbPath for a correct entry based on the database type.
        /// </summary>
        /// <param name="dbPath"></param>
        /// <param name="dataBaseType"></param>
        private void CheckDbPath(string dbPath, DataBaseType dataBaseType)
        {
            //TODO: This could be a pre check of the given dbPath.
            switch (dataBaseType)
            {
                case DataBaseType.SQLITE:
                    break;
                case DataBaseType.MSSQL:
                    throw new NotSupportedException("MSSQL Database not implemented yet!");
                case DataBaseType.MYSQL:
                    if (dbPath.Equals(""))
                        _dbPath = "localhost";
                    break;
                case DataBaseType.ACCESS:
                    break;
                default:
                    throw new NotSupportedException("Wrong Database type!");
            }
        }

        /// <summary>
        /// Connects to given <see cref="DataBaseType"/>
        /// </summary>
        /// <param name="dataBaseType">The database type to connect to.</param>
        /// <param name="createIfNotExists">If true will create the database if it doesn't exist.</param>
        private void ConnectToDatabase(DataBaseType dataBaseType, bool createIfNotExists)
        {
            try
            {
                switch (dataBaseType)
                {
                    case DataBaseType.SQLITE:
                        ConnectToSQLiteDB(createIfNotExists);
                        break;
                    case DataBaseType.MSSQL:
                        throw new NotSupportedException("MSSQL Database not implemented yet!");
                    case DataBaseType.MYSQL:
                        ConnectToMySQL(createIfNotExists);
                        break;
                    case DataBaseType.ACCESS:
                        ConnectToAccess(createIfNotExists);
                        break;
                    default:
                        throw new NotSupportedException("Wrong Database type!");
                }
                StartStatusTimer(_dbStatusIntervall);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Checks the createIfNotExists flag and connects to the database.
        /// </summary>
        /// <param name="createIfNotExists">If true will create the database if it doesn't exist.</param>
        private void ConnectToMySQL(bool createIfNotExists)
        {
            _abstractDBHandler = new MySQLHandler(_dbName, _dbPath, _user, _password, _port, createIfNotExists);
        }

        /// <summary>
        /// Checks the createIfNotExists flag and connects to the database.
        /// </summary>
        /// <param name="createIfNotExists">If true will create the database if it doesn't exist.</param>
        private void ConnectToSQLiteDB(bool createIfNotExists)
        {
            if (_dbPath.Equals(""))
                _dbPath = Directory.GetCurrentDirectory();
            else if (!Directory.Exists(_dbPath))
                throw new DirectoryNotFoundException("Invalid path!");

            string fullPath = _dbPath + "\\" + _dbName + _fileExtension;
            if (!createIfNotExists)
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Can't find file!");

            _abstractDBHandler = new SQLiteHandler(_dbName + _fileExtension, _dbPath);
        }

        /// <summary>
        /// Checks the createIfNotExists flag and connects to the database.
        /// </summary>
        /// <param name="createIfNotExists">If true will create the database if it doesn't exist.</param>
        private void ConnectToAccess(bool createIfNotExists)
        {
            if (_dbPath.Equals(""))
                _dbPath = Directory.GetCurrentDirectory();
            else if (!Directory.Exists(_dbPath))
                throw new DirectoryNotFoundException("Invalid path!");

            string fullPath = _dbPath + "\\" + _dbName + _fileExtension;
            if (!createIfNotExists)
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Can't find file!");

            _abstractDBHandler = new AccessHandler(_dbName + _fileExtension, _dbPath);
        }

        /// <summary>
        /// Changes the connectionString based on the entry
        /// </summary>
        private void ChangeConnectionString()
        {
            try
            {
                CheckDbName(_dbName);
                CheckDbPath(_dbPath, _dataBaseType);
                SetExtension(_dataBaseType);
                switch (_dataBaseType)
                {
                    case DataBaseType.SQLITE:
                        _abstractDBHandler.ChangeConnectionString(_dbName + _fileExtension, _dbPath);
                        break;
                    case DataBaseType.MSSQL:
                        throw new NotSupportedException("MSSQL Database not implemented yet!");
                    case DataBaseType.MYSQL:
                        _abstractDBHandler.ChangeConnectionString(_dbName, _dbPath, _user, _password, _port);
                        break;
                    default:
                        throw new NotSupportedException("Wrong Database type!");
                }

            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
            }
        }

        /// <summary>
        /// Starts the StatusTimer.
        /// </summary>
        private void StartStatusTimer(int millis)
        {
            try
            {
                _dbStatusIntervall = millis;
                _dbStatusTimer = new Timer(OnStatusCallback, null, _dbStatusIntervall, Timeout.Infinite);
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
            }
        }

        /// <summary>
        /// The Callback method for the delete timer.
        /// </summary>
        /// <param name="state"></param>
        private void OnDeleteCallback(object state)
        {
            try
            {
                if (_dbDeleteTimer != null)
                {
                    _abstractDBHandler.CheckDeleteTables(_tables);
                    _dbDeleteTimer.Change(_dbDeleteIntervall, Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
                _dbDeleteTimer.Change(_dbDeleteIntervall, Timeout.Infinite);
            }
        }

        /// <summary>
        /// The callback method for the status timer.
        /// </summary>
        /// <param name="state"></param>
        private void OnStatusCallback(object state)
        {
            try
            {
                if (_dbStatusTimer != null)
                {
                    bool returnValue = _abstractDBHandler.CheckDBStatus();
                    if (_dbStatus != returnValue)
                    {
                        _dbStatus = returnValue;
                        OnDataBaseStatusEvent(returnValue);
                    }
                    _dbStatusTimer.Change(_dbStatusIntervall, Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
                OnExceptionEvent(e);
            }
        }
        #endregion

        #region EventMethods
        /// <summary>
        /// <see cref="ExceptionEvent"/>
        /// </summary>
        /// <param name="e"><see cref="ExceptionEvent"/></param>
        protected virtual void OnExceptionEvent(Exception e)
        {
            //For Unit Testing activate this
            //throw;
            ExceptionEvent?.Invoke(this, e);
        }

        /// <summary>
        /// <see cref="DataBaseStatusEvent"/>
        /// </summary>
        /// <param name="value"></param>
        protected virtual void OnDataBaseStatusEvent(bool value)
        {
            DataBaseStatusEvent?.Invoke(this, value);
        }
        #endregion
    }
}
