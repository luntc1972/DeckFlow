`ClearsFloorBar` shipped with this exact signature:

```csharp
public static bool ClearsFloorBar(
    int n,
    double commanderP25,
    double corpusP25,
    double commanderMean,
    double corpusMean,
    double corpusStdDev,
    int minDeckCount,
    double ratioLow,
    double ratioHigh,
    double zThreshold,
    double absoluteFloorGap)
```

Chosen bar: a commander-role row clears only when `n >= minDeckCount`, its 25th-percentile role count diverges from the corpus 25th percentile by at least `ratioHigh`x or at most `ratioLow`x, and its mean differs from the corpus mean with `|z| >= zThreshold`.

When `corpusP25 == 0`, the multiplicative ratio test is replaced by `Math.Abs(commanderP25 - corpusP25) >= absoluteFloorGap`; the documented default gap is `2.0` cards because a one-card floor difference is not worth acting on.

`ClearsBar` survived this plan unchanged so the existing caller and build stayed green while wave 1 carries both bars in parallel.
