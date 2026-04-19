public enum ScaleType
{
    OneSixth,   // 1/6
    OneEighth,  // 1/8
    OneTenth    // 1/10
}

public class ScaleCalculator
{
    public ScaleType TranslateToScale(int scale)
    {
        if (scale == 6)
            return ScaleType.OneSixth;
        else if (scale == 8)
            return ScaleType.OneEighth;
        else if (scale == 10)
            return ScaleType.OneTenth;
        
        throw new NotSupportedException($"{scale} scale is not supported");
    }

    private readonly Dictionary<ScaleType, double> ScaleMultipliers = new()
    {
        { ScaleType.OneSixth, 1.0 },
        { ScaleType.OneEighth, 0.7 },
        { ScaleType.OneTenth, 0.5 }
    };


    public double Convert(double size, ScaleType from, ScaleType to)
    {
        double fromFactor = ScaleMultipliers[from];
        double toFactor = ScaleMultipliers[to];

        double baseSize = size / fromFactor;
        return baseSize * toFactor;
    }

    public double FromOneSixth(double size, ScaleType to)
    {
        return Convert(size, ScaleType.OneSixth, to);
    }
    public double ToOneSixth(double size, ScaleType from)
    {
        return Convert(size, from, ScaleType.OneSixth);
    }

    public double GetFactor(ScaleType from, ScaleType to)
    {
        return ScaleMultipliers[to] / ScaleMultipliers[from];
    }
}