namespace Level_2026.Models
{
    public class Observation
    {
        public string Line { get; set; } = "";
        public bool ShowLine { get; set; } = false;
        public string From { get; set; } = "";
        public string To { get; set; } = "";

        public double Dh { get; set; }
        public double Dist { get; set; }
    }
}