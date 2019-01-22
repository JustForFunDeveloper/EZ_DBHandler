using System;

namespace TAGnology_Global_Library.DataBaseHandler.MySQL.CustomDataTypes
{
    /// <summary>
    /// This class represents the MySQL Datatype TIME
    /// TIME [MySQL DataTypes] || 4 max. Size [bytes] || "-838:59:59.000000' to '838:59:59.000000" [Format]
    /// </summary>
    public class MySQLTime
    {
        private DateTime _data;
        private readonly string _stringFormat = "HH:mm:ss.fff";

        /// <summary>
        /// The data.
        /// </summary>
        public DateTime Data { get => _data; }

        /// <summary>
        /// The given data will be checked and stored.
        /// Throws an FormatException if something went wrong.
        /// </summary>
        /// <param name="data"> The given data.</param>
        public MySQLTime(DateTime data)
        {
            _data = data;
        }

        /// <summary>
        /// Overrides the ToString() method an returns a string with the correct format.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _data.ToString(_stringFormat);
        }
    }
}
