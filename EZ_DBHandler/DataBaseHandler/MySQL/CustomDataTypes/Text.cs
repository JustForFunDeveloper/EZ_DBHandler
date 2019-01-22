using System;

namespace TAGnology_Global_Library.DataBaseHandler.MySQL.CustomDataTypes
{
    /// <summary>
    /// This class represents the MySQL Datatype TEXT
    /// TEXT [MySQL DataTypes] || 65536 - 2 max. Size [bytes]
    /// </summary>
    public class Text
    {
        private string _data;
        private int _maxLength = 65536 - 1;

        /// <summary>
        /// The data.
        /// </summary>
        public string Data { get => _data; }

        /// <summary>
        /// The given data will be checked and stored.
        /// Throws an FormatException if something went wrong.
        /// </summary>
        /// <param name="data"> The given data.</param>
        public Text(string data)
        {
            if (data.Length > _maxLength)
                throw new FormatException("The given data has the wrong format or size! [maxLength = 65536 - 1]");
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

        /// <summary>
        /// Overwritten conversion from Text to string.
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator string(Text value)
        {
            return value.Data;
        }
    }
}
