using System;

namespace TAGnology_Global_Library.DataBaseHandler.MySQL.CustomDataTypes
{
    /// <summary>
    /// This class represents the MySQL Datatype LONGTEXT
    /// LONGTEXT [MySQL DataTypes] || 4294967296 - 4 max. Size [bytes]
    /// </summary>
    public class LongText
    {
        private string _data;
        private long _maxLength = 4294967296 - 1;

        /// <summary>
        /// The data.
        /// </summary>
        public string Data { get => _data; }

        /// <summary>
        /// The given data will be checked and stored.
        /// Throws an FormatException if something went wrong.
        /// </summary>
        /// <param name="data"> The given data.</param>
        public LongText(string data)
        {
            if (data.Length > _maxLength)
                throw new FormatException("The given data has the wrong format or size! [maxLength = 4294967296 - 1]");
            _data = data;
        }

        /// <summary>
        /// Overrides the ToString() method an returns a string with the correct format.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _data;
        }
    }
}
