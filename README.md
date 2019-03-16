# EZ_DBHandler
Easy to use DBHandler for SQLite, MySQL and Access Database.

## Blog -> https://www.die-technik-und-ich.at/?p=473

# Features
An easy to use DBHandler class which allows to choose between multiple database types.

## Implemented:

- Automatic trigger tables to get a count for all rows in the table.
- Automatic creation of the database and tables based on the given tables or classes. 
- Easy to use queries with the SQLQueryBuilder.
- Added basic multiThread functionality.
- Create a DBHandler instance for each database connection
 
## Not implemented / Future Features?:
- Indexes not used at the moment.
- Linq instead of SQLQueryBuilder.
- Fetch isn't probably thread safe yet.
- Parametrized queries to be safe from sql injection.

# How to use

## Create a MYSQL dataBase without tables:

```
_dBHandler = new DBHandler(
               "name",
               "",
               DataBaseType.MYSQL);
```

## Create a MYSQL dataBase with given tables:

```
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
                DataBaseType.MYSQL);
```

## Create a MYSQL dataBase with tables from a data model:
```
_dBHandler = new DBHandler(
               "name",
               "",
               DataBaseType.MYSQL);
_dBHandler.AddTables(CreateTablesFromDataModels(new List<Type>()
                {
                    typeof(Log)
                }));
```

### Create a data model
Check for available types for the database in the class. (e.g MySQLHandler.cs)
```
    public class Log
    {
        public long id { get; set; }
        public DateTime time { get; set; }
        public TinyText messageLevel { get; set; }
        public string message { get; set; }
    }
```