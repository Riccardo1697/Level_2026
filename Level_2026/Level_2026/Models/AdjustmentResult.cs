public class AdjustmentResult
{
    public Dictionary<string, double> Heights { get; set; } = new();

    public List<Residual> Residuals { get; set; } = new();

    public double Sigma0 { get; set; }

    public int UnknownCount { get; set; }

    public int UsedObservations { get; set; }
}

public class Residual
{
    public string Line { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    public double Dh { get; set; }
    public double Dist { get; set; }

    public double V { get; set; }
    public double W { get; set; }
}