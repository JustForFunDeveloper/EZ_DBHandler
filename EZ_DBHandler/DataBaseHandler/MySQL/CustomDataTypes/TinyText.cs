using System;

namespace TAGnology_Global_Library.DataBaseHandler.MySQL.CustomDataTypes
{
    /// <summary>
    /// This class represents the MySQL Datatype TINYTEXT
    /// TINYTEXT [MySQL DataTypes] || 256 - 1 max. Size [bytes]
    /// </summary>
    public class TinyText
    {
        private string _data;
        private int _maxLength = 256 - 1;

        /// <summary>
        /// The data.
        /// </summary>
        public string Data { get => _data; }

        /// <summary>
        /// The given data will be checked and stored.
        /// Throws an FormatException if something went wrong.
        /// </summary>
        /// <param name="data"> The given data.</param>
        public TinyText(string data)
        {
            _data = data;
            if (data.Length > _maxLength)
                throw new FormatException("The given data has the wrong format or size! [maxLength = 256 - 1]");
            
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
