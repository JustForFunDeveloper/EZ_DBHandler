using System;

namespace TAGnology_Global_Library.DataBaseHandler.MySQL.CustomDataTypes
{
    /// <summary>
    /// This class represents the MySQL Datatype MEDIUMINT
    /// MEDIUMINT [MySQL DataTypes] || 3 max. Size [bytes] || -8388608 bis 8388607 [Range]
    /// </summary>
    public class MediumInt
    {
        private int _data;
        private readonly int _minSize = -8388608;
        private readonly int _maxSize = 8388607;

        /// <summary>
        /// The data.
        /// </summary>
        public int Data { get => _data; }

        /// <summary>
        /// The given data will be checked and stored.
        /// Throws an FormatException if something went wrong.
        /// </summary>
        /// <param name="data"> The given data.</param>
        public MediumInt(int data)
        {
            if (!CheckData(data))
                throw new FormatException("The given data has the wrong format or size! [MediumInt => -8388608 until 8388607]");
            _data = data;
        }

        private bool CheckData(int data)
        {
            if (data > _minSize && data < _maxSize)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Overrides the ToString() method an returns a string with the correct format.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _data.ToString();
        }
    }
}
