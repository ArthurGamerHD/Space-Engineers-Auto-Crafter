namespace IngameScript
{
    public partial class Program
    {
        public static string MetricFormat(double input)
        {
            if (input >= 100)
                return MetricFormat((int)input);
            
            return input.ToString("0.##");
        }
        
        public static string MetricFormat(int input)
        {
            if (input >= 1000000000)
                // Congratulations, you've successfully created a singularity
                return (input / 1000000000d).ToString("0.00") + "G"; 
            if (input >= 1000000)
                return (input / 1000000d).ToString("0.00") + "M";
            if (input >= 10000)
                return (input / 1000d).ToString("0.00") + "k";
            
            return input.ToString();
        }
    }
}