namespace Level_2026.Models
{
    public class Observation
    {
        public required string Line { get; set; }
        public required string From { get; set; }
        public required string To { get; set; }
        public double Dh { get; set; }
        public double Dist { get; set; }
    }
}