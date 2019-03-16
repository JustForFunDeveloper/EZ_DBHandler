using System;

namespace EZ_DBHandler.DataBaseHandler.MySQL.CustomDataTypes
{
    /// <summary>
    /// This class represents the MySQL Datatype MEDIUMTEXT
    /// MEDIUMTEXT [MySQL DataTypes] || 16777216 - 3 max. Size [bytes]
    /// </summary>
    public class MediumText
    {
        private string _data;
        private int _maxLength = 16777216 - 1;

        /// <summary>
        /// The data.
        /// </summary>
        public string Data { get => _data; }

        /// <summary>
        /// The given data will be checked and stored.
        /// Throws an FormatException if something went wrong.
        /// </summary>
        /// <param name="data"> The given data.</param>
        public MediumText(string data)
        {
            if (data.Length > _maxLength)
                throw new FormatException("The given data has the wrong format or size! [maxLength = 16777216 - 1]");
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
