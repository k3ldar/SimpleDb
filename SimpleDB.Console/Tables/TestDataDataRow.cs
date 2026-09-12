namespace SimpleDB.Console.Tables
{
    [Table("TestData")]
    internal class TestDataDataRow : TableRowDefinition
    {
        private string _author;
        private string _data;

        public string Author
        {
            get => _author;

            set
            {
                if (_data == value)
                    return;

                _author = value;
                Update();
            }
        }

        public string Data
        {
            get => _data;

            set
            {
                if (_data == value)
                    return;

                _data = value;
                Update();
            }
        }
    }
}
