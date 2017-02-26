namespace Galvanika
{
    public class ProgramData
    {
        public ProgramData(int Key, string Code, string Operator, string AEM, string Bit, string Input, string Marker, string Output)
        {
            this.Key = Key;
            this.Code = Code;
            this.Operator = Operator;
            this.AEM = AEM;
            this.Bit = Bit;
            this.Input = Input;
            this.Marker = Marker;
            this.Output = Output;
        }
        public int Key { get; set; }
        public string Code { get; set; }
        public string Operator { get; set; }
        public string AEM { get; set; }
        public string Bit { get; set; }
        public string Input { get; set; }
        public string Marker { get; set; }
        public string Output { get; set; }
    }
    public class MemoryData
    {
        public MemoryData(string Address, string NameVariable, string Type, string Value, string CurrentValue)
        {
            this.Address = Address;
            this.NameVariable = NameVariable;
            this.Type = Type;
            this.Value = Value;
            this.CurrentValue = CurrentValue;
        }
        public string Address { get; set; }
        public string NameVariable { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string CurrentValue { get; set; }
    }
    public class MyTimers
    {
        public MyTimers(string Address, int Time, int EndTime, int Value)
        {
            this.Address = Address;
            this.Time = Time;
            this.EndTime = EndTime;
            this.Value = Value;
        }
        public string Address { get; set; }
        public int Time { get; set; }
        public int EndTime { get; set; }
        public int Value { get; set; }
    }
}