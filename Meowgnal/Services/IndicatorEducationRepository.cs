using System.Collections.Generic;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>
/// Static repository of plain-English educational content for technical indicators.
/// Covers all 44 indicators in the registry: Moving Averages, Oscillators,
/// Volatility, Volume, Trend, and Fundamental categories.
/// </summary>
public static class IndicatorEducationRepository
{
    private static readonly Dictionary<string, IndicatorEducation> All = Build();

    /// <summary>Returns the education record for the given indicator id, or null if not yet written.</summary>
    public static IndicatorEducation? Get(string indicatorId)
    {
        if (string.IsNullOrEmpty(indicatorId)) return null;
        return All.TryGetValue(indicatorId.ToUpperInvariant(), out var ed) ? ed : null;
    }

    private static Dictionary<string, IndicatorEducation> Build() => new()
    {
        // ═══════════════════════ MOVING AVERAGES ═══════════════════════

        ["SMA"] = new IndicatorEducation
        {
            IndicatorId = "SMA",
            WhatIsIt = "The Simple Moving Average is the most basic way to smooth out price noise. It just takes the closing prices of the last N candles, adds them up, and divides by N. The result is a smooth line that follows the price but without all the jagged ups and downs.",
            WhenToUse = "SMA shines in slow-moving, long-term trends (daily or weekly charts). It is not great for fast scalping because it reacts too slowly to sudden price moves.",
            BestPairedWith = new List<string>
            {
                "EMA (a faster MA) — use SMA as a long-term filter and EMA for short-term entries.",
                "MACD — the classic 'Golden Cross' and 'Death Cross' signals come from two SMAs (50 and 200).",
                "Volume — an SMA breakout confirmed by high volume is much more reliable."
            },
            CommonTraps = new List<string>
            {
                "Using a very short SMA (like 5 or 10) on choppy markets gives tons of false signals.",
                "Thinking the SMA 'predicts' price — it only describes what already happened, so it always lags.",
                "Ignoring that SMA weights every candle equally; a 200-SMA still gives huge weight to a candle from 200 days ago."
            },
            RecommendedDefaultParams = "Period 20 for short-term swing trades, 50 for medium, 200 for long-term trend. The 200-period SMA is watched by almost every professional trader — when price crosses it, big moves often follow."
        },

        ["EMA"] = new IndicatorEducation
        {
            IndicatorId = "EMA",
            WhatIsIt = "The Exponential Moving Average is like the SMA's more responsive cousin. It also averages past prices, but it gives more weight to recent candles and less weight to older ones. This makes it react faster when the price suddenly changes direction.",
            WhenToUse = "EMA is your go-to for trending markets on 15-minute to 4-hour charts. Day traders love it because it catches trend changes earlier than SMA.",
            BestPairedWith = new List<string>
            {
                "A slower EMA or SMA — two EMAs crossing (like 9/21) is one of the most popular entry signals.",
                "RSI — when price pulls back to the EMA and RSI is not overbought, that is a classic buy setup.",
                "MACD — the MACD line itself is just the difference between two EMAs (12 and 26)."
            },
            CommonTraps = new List<string>
            {
                "Whipsaws in ranging markets — two EMAs will cross back and forth many times, faking you out.",
                "Forgetting that even 'fast' EMAs still lag; they never call the exact top or bottom.",
                "Using the same EMA period for every timeframe. A 9-EMA on a 1-minute chart is very different from a 9-EMA on a daily chart."
            },
            RecommendedDefaultParams = "Period 9 for very fast scalps, 20 for swing trades, 50 for trend confirmation. The 9/21 EMA cross is one of the most backtested and reliable short-term signals in crypto."
        },

        ["WMA"] = new IndicatorEducation
        {
            IndicatorId = "WMA",
            WhatIsIt = "The Weighted Moving Average is similar to the EMA but uses a simple linear weight: the most recent candle gets weight N, the one before gets N-1, and so on. It sits between the SMA (equal weights) and the EMA (exponential decay) in responsiveness.",
            WhenToUse = "Use WMA when you want something snappier than SMA but less 'jumpy' than EMA. It works well on 1-hour and 4-hour charts for medium-length swings.",
            BestPairedWith = new List<string>
            {
                "HMA (Hull MA) — both are weighted averages, and comparing them reveals how much 'momentum' the market has.",
                "RSI — WMA crossovers filtered by RSI extremes produce cleaner entries.",
                "Volume — a WMA breakout on strong volume is a high-conviction trade."
            },
            CommonTraps = new List<string>
            {
                "Confusing WMA with VWMA (volume-weighted) — they look similar but react to completely different inputs.",
                "Using WMA on very low timeframes (1m) where it still produces too much noise.",
                "Expecting WMA to eliminate lag — it reduces lag compared to SMA, but never removes it."
            },
            RecommendedDefaultParams = "Period 20 is a balanced default. Shorter (10) for scalps, longer (50) for trend filters. The linear weighting makes WMA behave nicely in the 20–50 range."
        },

        ["HMA"] = new IndicatorEducation
        {
            IndicatorId = "HMA",
            WhatIsIt = "The Hull Moving Average, created by Alan Hull, is one of the smoothest and most responsive moving averages you can use. It uses a clever formula with weighted averages of weighted averages, then takes a square root of the period. The result is a line that hugs the price tightly without being noisy.",
            WhenToUse = "HMA is excellent for trend-following on 15m to 4h charts. It catches reversals early while staying smooth enough to read at a glance.",
            BestPairedWith = new List<string>
            {
                "A longer SMA or EMA — use HMA as the fast trigger and a 50-SMA as the trend filter.",
                "Stochastic or RSI — HMA tells you the direction, the oscillator tells you if it is overextended.",
                "Volume — HMA direction changes confirmed by volume spikes are very reliable entries."
            },
            CommonTraps = new List<string>
            {
                "Thinking HMA is 'magic' — it still lags, and in sideways markets it will flip colors constantly.",
                "Using a very short HMA (under 10) on volatile coins — the line becomes too erratic.",
                "Ignoring the color-change logic: HMA is most useful when you act on slope changes, not raw crossovers."
            },
            RecommendedDefaultParams = "Period 20 works beautifully for most swing trades. Period 50 for longer trends. Alan Hull himself recommended 20 as a starting point because the square-root math produces the best balance of smoothness and responsiveness at that length."
        },

        ["DEMA"] = new IndicatorEducation
        {
            IndicatorId = "DEMA",
            WhatIsIt = "The Double Exponential Moving Average is literally two EMAs combined in a way that cancels out most of the lag. The formula is: 2 × EMA − EMA-of-EMA. This removes the 'sluggishness' of a single EMA while keeping its smoothness.",
            WhenToUse = "DEMA is best for fast-moving markets where you need early signals — scalping 5m/15m charts, or catching the start of a crypto pump.",
            BestPairedWith = new List<string>
            {
                "MACD — DEMA and MACD together give both direction and momentum confirmation.",
                "ATR — since DEMA is fast, use ATR-based stops to avoid getting stopped out by noise.",
                "Bollinger Bands — a DEMA crossing outside the bands often signals a strong breakout."
            },
            CommonTraps = new List<string>
            {
                "Overtrading: because DEMA reacts so fast, it gives many more signals than SMA — filter carefully.",
                "Using DEMA on very short periods (under 10) — it becomes almost as noisy as raw price.",
                "Forgetting that 'double' does not mean 'twice as good' — it just means 'less lag'."
            },
            RecommendedDefaultParams = "Period 20 is the sweet spot. DEMA was designed to reduce lag, and at period 20 it cuts lag dramatically without becoming too twitchy. For scalping, try 10; for trends, 50."
        },

        ["TEMA"] = new IndicatorEducation
        {
            IndicatorId = "TEMA",
            WhatIsIt = "The Triple Exponential Moving Average goes one step further than DEMA by combining three layers of EMAs: 3×EMA − 3×EMA-of-EMA + EMA-of-EMA-of-EMA. It is one of the fastest, least-laggy moving averages available.",
            WhenToUse = "TEMA is best for very aggressive day traders on 1m to 15m charts who need almost real-time trend detection. It is overkill for swing trading.",
            BestPairedWith = new List<string>
            {
                "A slower MA (50 or 200 SMA) — TEMA gives fast entries, the slow MA confirms the big-picture trend.",
                "Volume — TEMA signals without volume confirmation are often fake.",
                "RSI — since TEMA is so fast, use RSI to filter out overbought/oversold entries."
            },
            CommonTraps = new List<string>
            {
                "Using TEMA on quiet markets — it will give you dozens of false signals per day.",
                "Thinking 'triple' means it should be your only indicator — always pair it with something slower.",
                "Ignoring the fact that TEMA can overshoot price during strong moves and 'wrap around' the candles."
            },
            RecommendedDefaultParams = "Period 20 is the standard. Patrick Mulloy, who invented TEMA, designed it to match the responsiveness of an EMA with half the period. So a 20-period TEMA feels like a 10-period EMA but smoother."
        },

        ["KAMA"] = new IndicatorEducation
        {
            IndicatorId = "KAMA",
            WhatIsIt = "The Kaufman Adaptive Moving Average is a 'smart' MA that automatically speeds up when the market is trending and slows down when the market is choppy. It measures how 'efficient' the price movement is — straight trends get fast response, sideways chop gets slow response.",
            WhenToUse = "KAMA is ideal for markets that alternate between trends and ranges (like crypto). It adapts automatically, so you do not need to switch timeframes or periods manually.",
            BestPairedWith = new List<string>
            {
                "ADX — both measure trend quality; when both agree, you have high-confidence entries.",
                "ATR — KAMA handles direction, ATR handles position sizing and stop distance.",
                "Bollinger Bands — KAMA inside the bands means 'range', outside means 'trend'."
            },
            CommonTraps = new List<string>
            {
                "Expecting instant adaptation — KAMA still takes a few candles to 'notice' a regime change.",
                "Using default parameters on very short timeframes — KAMA needs at least 10 candles of data to work properly.",
                "Forgetting that KAMA flatlines during chop — that is a feature, not a bug; do not force trades when it is flat."
            },
            RecommendedDefaultParams = "Period 10 with fast=2 and slow=30 is Kaufman's original recommendation. The 10-period window measures trend efficiency, and the 2/30 endpoints control how fast the MA can react at its extremes."
        },

        ["VWMA"] = new IndicatorEducation
        {
            IndicatorId = "VWMA",
            WhatIsIt = "The Volume Weighted Moving Average is a moving average where each candle's contribution is multiplied by its volume. High-volume candles pull the line more than low-volume candles. This tells you where the 'real' action happened, not just where price drifted.",
            WhenToUse = "VWMA is perfect for confirming breakouts and spotting accumulation/distribution. When price crosses above VWMA on heavy volume, it is a much stronger signal than a plain MA cross.",
            BestPairedWith = new List<string>
            {
                "A plain SMA of the same period — when VWMA diverges from SMA, volume is shifting the trend.",
                "OBV (On-Balance Volume) — both use volume; when they agree, the move has real conviction.",
                "Volume itself — VWMA without the volume bars below loses half its meaning."
            },
            CommonTraps = new List<string>
            {
                "Using VWMA on very low-liquidity coins — a single big trade can distort the whole average.",
                "Confusing VWMA with VWAP — VWAP resets every day, VWMA is a rolling average.",
                "Ignoring that VWMA reacts to volume spikes, which can be manipulation on small-cap coins."
            },
            RecommendedDefaultParams = "Period 20 matches the standard SMA comparison. The classic setup is a 20-VWMA crossing a 20-SMA: when VWMA goes above SMA, volume is supporting the move; when it goes below, the move is weak."
        },

        // ═══════════════════════ OSCILLATORS ═══════════════════════

        ["RSI"] = new IndicatorEducation
        {
            IndicatorId = "RSI",
            WhatIsIt = "The Relative Strength Index measures how fast and how much the price has been moving up versus down, on a scale from 0 to 100. Above 70 it is considered 'overbought' (price went up too fast), below 30 it is 'oversold' (price fell too fast).",
            WhenToUse = "RSI works best in ranging markets to catch tops and bottoms. In strong trends, RSI can stay overbought/oversold for a long time — so do not fade a strong trend just because RSI is at 75.",
            BestPairedWith = new List<string>
            {
                "Moving averages — only take RSI oversold signals when price is above the 50-SMA (trend filter).",
                "MACD — RSI divergence + MACD cross is one of the most powerful reversal combinations.",
                "Support/resistance levels — an RSI bounce off 30 at a support level is a classic buy."
            },
            CommonTraps = new List<string>
            {
                "Selling just because RSI is above 70 — in a strong uptrend, RSI can stay above 70 for weeks.",
                "Using RSI alone — it is a filter, not a complete strategy.",
                "Ignoring 'divergence' — when price makes a new high but RSI makes a lower high, that is a very reliable reversal signal."
            },
            RecommendedDefaultParams = "Period 14 is the original setting invented by J. Welles Wilder in 1978 and is still the standard today. Shorter (7) for scalps, longer (21) for smoother signals. The 70/30 thresholds are also Wilder's original recommendation."
        },

        ["STOCH"] = new IndicatorEducation
        {
            IndicatorId = "STOCH",
            WhatIsIt = "The Stochastic Oscillator compares the current closing price to the range of prices over the last N periods. It shows where the price sits within its recent range: near the high of the range = high value (near 100), near the low = low value (near 0).",
            WhenToUse = "Stochastic is excellent in sideways, ranging markets for catching swing tops and bottoms. It is less useful in strong trends because it stays overbought/oversold for too long.",
            BestPairedWith = new List<string>
            {
                "RSI — when both oscillators agree on oversold, the reversal is more reliable.",
                "Bollinger Bands — a Stochastic cross at the lower band is a classic mean-reversion buy.",
                "Support/resistance — Stochastic crossovers at key levels are much higher probability."
            },
            CommonTraps = new List<string>
            {
                "Using it in strong trends — Stochastic will stay 'overbought' the entire uptrend and keep telling you to sell.",
                "Confusing %K and %D lines — %K is the fast line, %D is the slow signal line; crossovers matter, not absolute values.",
                "Ignoring the smoothing period — the default %D smoothing of 3 is important; changing it changes the signal timing."
            },
            RecommendedDefaultParams = "Period 14 with %K smoothing of 3 and %D smoothing of 3 is the classic 'slow stochastic' setup, which is much less noisy than the raw 'fast stochastic'. This is what George Lane, the inventor, recommended for daily trading."
        },

        ["STOCHRSI"] = new IndicatorEducation
        {
            IndicatorId = "STOCHRSI",
            WhatIsIt = "Stochastic RSI applies the Stochastic formula to the RSI itself, not to price. This makes it an 'oscillator of an oscillator' — it shows where RSI sits within its own recent range. It moves much faster than plain RSI.",
            WhenToUse = "StochRSI is great for very short-term scalping (1m to 15m) where you need faster signals than regular RSI can provide. It is too noisy for daily charts.",
            BestPairedWith = new List<string>
            {
                "Regular RSI — use plain RSI as the trend filter, StochRSI for precise entries.",
                "EMA — only take StochRSI oversold signals when price is above a rising EMA.",
                "Volume — StochRSI reversals confirmed by volume spikes are much more reliable."
            },
            CommonTraps = new List<string>
            {
                "Using StochRSI on high timeframes — it gives way too many false signals on 4h and daily charts.",
                "Treating it like regular RSI — StochRSI hits 0 and 100 constantly; the 20/80 thresholds are more useful than 30/70.",
                "Forgetting that it is a derivative of RSI — if RSI is flat, StochRSI will be meaningless."
            },
            RecommendedDefaultParams = "Period 14 with %K=3 and %D=3 is the standard. Some scalpers use 7/3/3 for even faster signals. Because StochRSI is already more sensitive than RSI, longer periods (21+) rarely help."
        },

        ["CCI"] = new IndicatorEducation
        {
            IndicatorId = "CCI",
            WhatIsIt = "The Commodity Channel Index measures how far the current price is from its statistical average. Values above +100 mean price is unusually high; below −100 means unusually low. Unlike RSI, CCI has no fixed bounds — it can go to +200 or −200.",
            WhenToUse = "CCI works well for spotting new trends (breakouts above +100 or below −100) and for catching extreme oversold/overbought conditions in ranging markets.",
            BestPairedWith = new List<string>
            {
                "Moving averages — a CCI breakout above +100 that happens above the 50-EMA is a strong trend-continuation buy.",
                "RSI — when both CCI and RSI signal oversold, reversals are more reliable.",
                "Volume — CCI breakouts on high volume often start real trends."
            },
            CommonTraps = new List<string>
            {
                "Shorting just because CCI is above +100 — in strong trends, CCI can stay above +100 for a long time.",
                "Using the same threshold for every market — in crypto, ±200 can be a better threshold than ±100 because of higher volatility.",
                "Confusing CCI with RSI — CCI has no bounds, so 'extreme' readings can go much further than RSI's 0–100 range."
            },
            RecommendedDefaultParams = "Period 20 is the original setting from Donald Lambert, who invented CCI in 1980. He designed it to spot cyclical turns in commodities, and 20 periods captures about one month of daily data — a natural market cycle."
        },

        ["WILLIAMSR"] = new IndicatorEducation
        {
            IndicatorId = "WILLIAMSR",
            WhatIsIt = "Williams %R is very similar to the Stochastic Oscillator but plotted on a scale from 0 to −100 (instead of 0 to 100). Readings above −20 are 'overbought', below −80 are 'oversold'. It tells you where the close is relative to the recent high-low range.",
            WhenToUse = "Williams %R is great for spotting quick tops and bottoms in ranging markets on 15m to 4h charts. It is particularly useful for catching momentum exhaustion.",
            BestPairedWith = new List<string>
            {
                "RSI — both measure overbought/oversold; when they agree, reversals are high probability.",
                "Moving averages — only take %R oversold buys when price is above the 50-SMA.",
                "Volume — a %R reversal with rising volume confirms the turn."
            },
            CommonTraps = new List<string>
            {
                "Confusing the negative scale — %R reads −20 for overbought, not +20. Many beginners read it backwards.",
                "Using it in strong trends — like Stochastic, %R can stay 'overbought' during an entire uptrend.",
                "Ignoring the 'failure swing' pattern — when %R fails to reach a new extreme while price does, that is a powerful reversal signal."
            },
            RecommendedDefaultParams = "Period 14 is the original setting from Larry Williams, the inventor. Some traders prefer 10 for faster scalping or 21 for smoother signals. The −20/−80 thresholds are Williams's original recommendation."
        },

        ["MFI"] = new IndicatorEducation
        {
            IndicatorId = "MFI",
            WhatIsIt = "The Money Flow Index is like RSI, but it also includes volume. It measures buying versus selling pressure, not just price movement. A high MFI means buyers are pushing price up with real volume; a low MFI means sellers are in control.",
            WhenToUse = "MFI is best for confirming breakouts and spotting divergences. If price makes a new high but MFI does not, that rally is probably losing steam — a classic 'bearish divergence'.",
            BestPairedWith = new List<string>
            {
                "RSI — RSI measures price momentum, MFI measures volume-weighted momentum; divergence between them is powerful.",
                "OBV (On-Balance Volume) — both use volume; when they agree, the move has real conviction.",
                "Moving averages — MFI oversold above a rising EMA is a strong buy signal."
            },
            CommonTraps = new List<string>
            {
                "Using MFI on low-liquidity coins — volume data is unreliable, so MFI becomes noisy.",
                "Ignoring divergences — they are MFI's strongest feature; using it only as overbought/oversold wastes its power.",
                "Forgetting that MFI is slower than RSI because volume data adds lag; do not expect instant signals."
            },
            RecommendedDefaultParams = "Period 14 is the standard, matching RSI for easy comparison. The 80/20 thresholds are used instead of RSI's 70/30 because MFI tends to stay in a tighter range due to volume smoothing."
        },

        ["ROC"] = new IndicatorEducation
        {
            IndicatorId = "ROC",
            WhatIsIt = "The Rate of Change simply measures the percentage change between the current price and the price N periods ago. It is one of the purest momentum indicators — positive means price is higher than N bars ago, negative means lower.",
            WhenToUse = "ROC is great for spotting momentum shifts and divergences. It works well on any timeframe and pairs nicely with almost any other indicator.",
            BestPairedWith = new List<string>
            {
                "Moving average — buy when ROC crosses above zero while price is above the 50-SMA.",
                "RSI — ROC shows pure momentum, RSI shows overbought/oversold; together they filter each other.",
                "Bollinger Bands — ROC breakouts outside the bands often start strong trends."
            },
            CommonTraps = new List<string>
            {
                "Choosing the wrong period — a 1-period ROC is just the daily change (too noisy); a 200-period ROC is too slow for most trades.",
                "Ignoring the zero line — ROC crossing zero is a more reliable signal than its absolute value.",
                "Using ROC alone for entries — it measures speed, not direction quality; always pair with a trend filter."
            },
            RecommendedDefaultParams = "Period 10 or 12 is common for swing trading. Period 25 (about one month of trading days) is popular for longer trends. Shorter periods (5) work for scalping but produce many false signals."
        },

        ["TRIX"] = new IndicatorEducation
        {
            IndicatorId = "TRIX",
            WhatIsIt = "TRIX is a triple-smoothed EMA of the price, shown as a percentage rate of change. The triple smoothing removes almost all the noise, leaving only the real underlying trend. It is one of the smoothest momentum indicators available.",
            WhenToUse = "TRIX is excellent for longer-term trend following (daily and weekly charts). It rarely gives false signals, but it is slow — so not good for scalping.",
            BestPairedWith = new List<string>
            {
                "A signal line (9-period EMA of TRIX) — the classic TRIX system uses crossovers with its signal line.",
                "ADX — both filter noise; when both say 'trend', you have high conviction.",
                "MACD — TRIX as a long-term filter, MACD for precise entries."
            },
            CommonTraps = new List<string>
            {
                "Expecting fast signals — triple smoothing means TRIX is very slow; it misses the start of moves.",
                "Using it on short timeframes (under 1h) — the triple smoothing makes it almost flat on fast data.",
                "Ignoring the signal line — TRIX alone is useful, but TRIX + signal line crossovers are much more actionable."
            },
            RecommendedDefaultParams = "Period 15 with a 9-period signal line is the standard setup from Jack Hutson, who invented TRIX. The 15-period triple smoothing removes about 90% of the noise, leaving only genuine trend signals."
        },

        ["ULTIMATE"] = new IndicatorEducation
        {
            IndicatorId = "ULTIMATE",
            WhatIsIt = "The Ultimate Oscillator combines three different timeframes (short, medium, and long) into a single line. The idea is that a real reversal must show up on all three timeframes at once. This reduces the false signals that single-timeframe oscillators produce.",
            WhenToUse = "Ultimate Oscillator is best for spotting major reversals on daily and weekly charts. It is not meant for quick scalps — it is designed to catch big turning points.",
            BestPairedWith = new List<string>
            {
                "Price action — Ultimate Oscillator signals should always be confirmed by candlestick patterns.",
                "Support/resistance — a divergence at a key level is the highest-probability setup.",
                "Volume — a reversal confirmed by rising volume is much more reliable."
            },
            CommonTraps = new List<string>
            {
                "Using it for short-term scalping — it was designed for multi-day swings and bigger.",
                "Ignoring the 'divergence' buy/sell rules — the inventor Larry Williams specifically said to only act on divergences, not on raw overbought/oversold readings.",
                "Forgetting the 7-period short cycle can still give false signals in strong trends."
            },
            RecommendedDefaultParams = "The original 7-14-28 periods from Larry Williams remain the best default. They capture roughly 1 week, 2 weeks, and 1 month of daily data — three natural market cycles that work together to filter noise."
        },

        ["AO"] = new IndicatorEducation
        {
            IndicatorId = "AO",
            WhatIsIt = "The Awesome Oscillator is simply the difference between a 5-period and a 34-period simple moving average of the candle midpoints (high+low)/2. It is displayed as a histogram. Green bars above zero mean short-term momentum is up; red bars below zero mean it is down.",
            WhenToUse = "AO works well for momentum scalping and swing trading. Bill Williams, its inventor, designed specific entry patterns (saucer, twin peaks, zero-line cross) that are still widely used.",
            BestPairedWith = new List<string>
            {
                "Alligator (Williams's other system) — AO was designed to be used together with the Alligator.",
                "Fractals — AO signals confirmed by a fractal breakout are much stronger.",
                "Volume — AO histogram bars with rising volume confirm the momentum shift."
            },
            CommonTraps = new List<string>
            {
                "Using AO without the specific Williams patterns — raw AO histogram crossovers give too many false signals.",
                "Ignoring the 'saucer' setup — the three-bar pattern Williams described is much more reliable than any single crossover.",
                "Forgetting that AO uses midpoints, not closes — it behaves slightly differently than RSI or MACD."
            },
            RecommendedDefaultParams = "AO has no adjustable parameters — the 5 and 34 periods are fixed in the original formula. Bill Williams chose these because 5 captures very short momentum and 34 (a Fibonacci number) captures the medium-term rhythm."
        },

        ["CMO"] = new IndicatorEducation
        {
            IndicatorId = "CMO",
            WhatIsIt = "The Chande Momentum Oscillator measures momentum by taking the difference between the sum of recent gains and the sum of recent losses, divided by their total. It ranges from −100 to +100, with +50 as a common overbought level and −50 as oversold.",
            WhenToUse = "CMO is great for catching strong momentum shifts and divergences. It reacts faster than RSI but smoother than Stochastic, making it a nice middle ground.",
            BestPairedWith = new List<string>
            {
                "Moving average — buy when CMO crosses above zero while price is above the 50-EMA.",
                "RSI — CMO and RSI measure momentum differently; when both agree, signals are stronger.",
                "Bollinger Bands — a CMO breakout outside the bands often starts a real trend."
            },
            CommonTraps = new List<string>
            {
                "Using the wrong thresholds — CMO's natural range is ±100, so +50/−50 is more meaningful than RSI's 70/30.",
                "Confusing CMO with ROC — they look similar but CMO normalizes by total movement, making it more stable.",
                "Ignoring the zero-line cross — it is often a cleaner entry signal than extreme readings."
            },
            RecommendedDefaultParams = "Period 14 is the standard, matching RSI for comparison. Tushar Chande, the inventor, designed it to complement RSI, so using the same period makes the two oscillators directly comparable."
        },

        ["CONNORSRSI"] = new IndicatorEducation
        {
            IndicatorId = "CONNORSRSI",
            WhatIsIt = "Connors RSI is a composite indicator made of three parts: (1) the duration of the current up/down streak, (2) the RSI of price changes, and (3) how the current price change ranks against recent changes. It was designed specifically for mean-reversion strategies.",
            WhenToUse = "Connors RSI is best for short-term mean reversion on stocks and crypto. Values below 10 signal a strong oversold condition that tends to bounce back within a few days.",
            BestPairedWith = new List<string>
            {
                "A long-term trend filter (like 200-SMA) — only buy ConnorsRSI oversold signals when price is above the 200-SMA.",
                "ADX — mean reversion works best when ADX is low (ranging market).",
                "Bollinger Bands — ConnorsRSI oversold at the lower Bollinger Band is a very high-probability setup."
            },
            CommonTraps = new List<string>
            {
                "Using it for trend-following — ConnorsRSI was designed for mean reversion, not trends.",
                "Buying every ConnorsRSI < 10 — always filter with a trend and regime check.",
                "Holding trades too long — mean-reversion trades typically last 2–5 days; holding longer reduces edge."
            },
            RecommendedDefaultParams = "The standard is RSI period 3, up/down streak period 2, and percent-rank period 100. Larry Connors and Caesar Alvarez designed these specific values after testing thousands of combinations for mean reversion."
        },

        ["MACD"] = new IndicatorEducation
        {
            IndicatorId = "MACD",
            WhatIsIt = "The MACD (Moving Average Convergence Divergence) is one of the most popular indicators ever created. It shows the difference between two EMAs (usually 12 and 26) as a line, plus a 'signal line' (9-period EMA of the MACD), plus a histogram showing the distance between the two lines.",
            WhenToUse = "MACD works in almost every market condition. Use crossovers for entries, histogram for momentum shifts, and divergences for powerful reversal signals.",
            BestPairedWith = new List<string>
            {
                "RSI — MACD shows direction and momentum, RSI shows overbought/oversold; together they filter each other.",
                "Moving average — only take MACD buy signals when price is above the 200-SMA for trend confirmation.",
                "Volume — a MACD crossover with rising volume is much more reliable than one on thin volume."
            },
            CommonTraps = new List<string>
            {
                "Taking every crossover — MACD gives many signals in choppy markets, leading to whipsaws.",
                "Ignoring the histogram — the histogram often turns before the MACD line crosses, giving earlier signals.",
                "Forgetting about divergences — price making new highs while MACD makes lower highs is one of the most reliable reversal signals in technical analysis."
            },
            RecommendedDefaultParams = "12/26/9 is the original setting from Gerald Appel in the 1970s and remains the gold standard. Shorter (6/13/5) for scalping, longer (19/39/9) for weekly charts. The 12/26 combination matches roughly two weeks and one month of trading days."
        },

        ["ADX"] = new IndicatorEducation
        {
            IndicatorId = "ADX",
            WhatIsIt = "The Average Directional Index measures how strong a trend is, regardless of direction. A value above 25 means a strong trend (up or down), below 20 means a weak or sideways market. ADX itself never tells you the direction — only the strength.",
            WhenToUse = "ADX is the perfect filter for any trend-following strategy. Only take breakout signals when ADX is rising above 25; only take mean-reversion signals when ADX is below 20.",
            BestPairedWith = new List<string>
            {
                "DI+ and DI− (the directional indicators) — together with ADX they form the full DMI system that tells you direction AND strength.",
                "Moving averages — ADX above 25 confirms that an MA cross has real trend behind it.",
                "Bollinger Bands — when ADX is low, price stays inside the bands; when ADX rises, breakouts become real."
            },
            CommonTraps = new List<string>
            {
                "Confusing ADX with direction — a high ADX can mean a strong downtrend just as easily as a strong uptrend.",
                "Using ADX for entries — it is a filter, not a trigger. It tells you WHEN to trade, not WHAT to trade.",
                "Ignoring the DI lines — without DI+ and DI−, you do not know if the trend is up or down."
            },
            RecommendedDefaultParams = "Period 14 is the original setting from J. Welles Wilder (the same man who invented RSI and ATR). The 25 threshold for 'strong trend' and 20 for 'weak' are also Wilder's original recommendations and remain the standard."
        },

        // ═══════════════════════ VOLATILITY ═══════════════════════

        ["ATR"] = new IndicatorEducation
        {
            IndicatorId = "ATR",
            WhatIsIt = "The Average True Range measures how much the market moves on average per candle. It takes the 'true range' of each candle (the largest of: high-low, high vs previous close, low vs previous close) and averages it over N periods. A high ATR means big swings; a low ATR means quiet, tight trading.",
            WhenToUse = "ATR is essential for setting stop losses and position sizing. In volatile markets (high ATR), your stop needs to be wider to avoid getting shaken out by noise. In calm markets (low ATR), you can use tighter stops.",
            BestPairedWith = new List<string>
            {
                "EMA — use EMA for direction, ATR for stop distance. Example: stop = 2 × ATR below the 20-EMA.",
                "SuperTrend — SuperTrend is literally built on ATR; understanding ATR helps you understand SuperTrend.",
                "Position sizing — risk a fixed dollar amount per trade, then divide by ATR to get the right position size."
            },
            CommonTraps = new List<string>
            {
                "Thinking ATR shows direction — it does not. A rising ATR just means bigger moves, not necessarily upward.",
                "Using a fixed-percentage stop in all markets — a 2% stop works in calm markets but gets hit constantly in volatile ones. ATR adapts to the market.",
                "Ignoring that ATR spikes during news events — a stop based on yesterday's ATR may be too tight after a big candle."
            },
            RecommendedDefaultParams = "Period 14 is the original setting from J. Welles Wilder, who invented ATR alongside RSI. It smooths out single-candle spikes while still reacting quickly to changes in volatility. For very fast markets, some traders use 7–10."
        },

        ["BBANDS"] = new IndicatorEducation
        {
            IndicatorId = "BBANDS",
            WhatIsIt = "Bollinger Bands consist of THREE lines: a middle band (20-period SMA), an upper band (middle + 2 standard deviations), and a lower band (middle − 2 standard deviations). The bands expand when the market is volatile and squeeze together when it is calm. About 95% of price action stays inside the bands under normal conditions.",
            WhenToUse = "Use Bollinger Bands for two main setups: (1) 'The Squeeze' — when bands get very narrow, a big move is coming; (2) Mean reversion — in ranging markets, buy at the lower band and sell at the upper band.",
            BestPairedWith = new List<string>
            {
                "RSI — when price touches the lower band AND RSI is below 30, the bounce probability is much higher.",
                "Keltner Channel — when Bollinger Bands squeeze INSIDE the Keltner Channel, it signals a 'TTM Squeeze' (explosive move incoming).",
                "Volume — a breakout from a squeeze confirmed by above-average volume is far more reliable."
            },
            CommonTraps = new List<string>
            {
                "Selling just because price touches the upper band — in a strong uptrend, price can 'ride the upper band' for many candles. Touching the band is not automatically a reversal signal.",
                "Ignoring the squeeze — the most powerful Bollinger signal is when the bands narrow dramatically. That is when you should prepare for a breakout, not fade the bands.",
                "Using the default 2-standard-deviation width in extremely volatile markets — in crypto, 2.5 or 3 standard deviations may be more appropriate."
            },
            RecommendedDefaultParams = "Period 20 with a 2.0 multiplier is John Bollinger's original and most widely used setting. The 20-period SMA captures roughly one month of trading days, and 2 standard deviations enclose about 95% of price action. For crypto's higher volatility, some traders increase the multiplier to 2.5."
        },

        ["KELTNER"] = new IndicatorEducation
        {
            IndicatorId = "KELTNER",
            WhatIsIt = "The Keltner Channel is an envelope around price, similar to Bollinger Bands but built differently: the middle line is an EMA (usually 20-period), and the outer bands are set at a multiple of ATR (usually 2×ATR) above and below the EMA. While Bollinger Bands use standard deviation, Keltner uses ATR, making it smoother and less reactive to single-candle spikes.",
            WhenToUse = "Keltner Channels are great for trend-following: in an uptrend, price tends to stay above the middle line and near the upper channel. They are also the key component of the 'TTM Squeeze' setup when combined with Bollinger Bands.",
            BestPairedWith = new List<string>
            {
                "Bollinger Bands — when BBands squeeze inside the Keltner Channel, it signals a 'TTM Squeeze': volatility is compressing and a big move is imminent.",
                "ADX — Keltner breakouts are more reliable when ADX is rising above 25 (confirming a real trend).",
                "ATR — since Keltner is built on ATR, watching ATR helps you understand why the channel width changes."
            },
            CommonTraps = new List<string>
            {
                "Treating Keltner like Bollinger Bands — Keltner is smoother and slower to react. It is better for trend-following, not for quick mean-reversion scalps.",
                "Buying every touch of the upper channel — in a strong trend, price can ride the upper channel for a long time. Use it as a trend filter, not a reversal signal.",
                "Ignoring the middle EMA line — it acts as dynamic support/resistance in trending markets and is often the best place to add to a position."
            },
            RecommendedDefaultParams = "Period 20 with a 2.0 ATR multiplier is the standard setting, popularized by John Carter's 'TTM Squeeze' strategy. The 20-period EMA responds quickly enough for swing trading while the 2×ATR width accommodates normal volatility without too many false breakouts."
        },

        ["DONCHIAN"] = new IndicatorEducation
        {
            IndicatorId = "DONCHIAN",
            WhatIsIt = "The Donchian Channel plots the highest high and the lowest low of the last N periods, with a middle line as their average. It creates a simple 'box' around price. When price breaks above the upper line, it means a new N-period high has been made; below the lower line, a new N-period low.",
            WhenToUse = "Donchian Channels are the foundation of the famous 'Turtle Trading' breakout strategy. Buy when price breaks above the 20-period high; sell when it breaks below the 10-period low. They work best in trending markets.",
            BestPairedWith = new List<string>
            {
                "ATR — the original Turtle Traders used ATR for position sizing and stop placement alongside Donchian breakouts.",
                "Volume — a Donchian breakout on above-average volume is much more likely to sustain than one on thin volume.",
                "ADX — only take Donchian breakouts when ADX is above 25, confirming a real trend is present."
            },
            CommonTraps = new List<string>
            {
                "Using Donchian in ranging markets — the channel will generate constant false breakouts when price is just bouncing between support and resistance.",
                "Ignoring the middle line — it is a useful trailing stop level in trending markets. If price closes back below the middle, the trend may be weakening.",
                "Setting the period too short — a 10-period Donchian on a 1-minute chart produces dozens of false signals per hour. Longer periods (20–55) filter out noise."
            },
            RecommendedDefaultParams = "Period 20 is the classic Turtle Trading setting for entries. The original Turtles used 20-period highs for entries and 10-period lows for exits. Richard Donchian, the inventor, originally used a 4-week (20 trading day) window."
        },

        ["STDDEV"] = new IndicatorEducation
        {
            IndicatorId = "STDDEV",
            WhatIsIt = "Standard Deviation measures how spread out the prices are from their average. A low STDDEV means prices are clustered tightly around the mean (calm market); a high STDDEV means prices are scattered far from the mean (volatile market). It is the building block behind Bollinger Bands.",
            WhenToUse = "STDDEV is best used as a volatility filter: when STDDEV is unusually low, the market is 'coiling' and a big move may be coming. When STDDEV is very high, the market is chaotic and stops need to be wider.",
            BestPairedWith = new List<string>
            {
                "Bollinger Bands — BBands are literally SMA ± (multiplier × STDDEV). Understanding STDDEV helps you understand why bands widen and narrow.",
                "ATR — both measure volatility but differently: ATR uses high-low range, STDDEV uses close-to-mean distance. They complement each other.",
                "Moving averages — plot STDDEV alongside a 20-SMA to see when price is 'stretching' too far from its average."
            },
            CommonTraps = new List<string>
            {
                "Confusing STDDEV with direction — STDDEV tells you HOW MUCH price is moving, not WHICH WAY. A high STDDEV can happen in both crashes and rallies.",
                "Using STDDEV alone as a trading signal — it is a measurement tool, not a trigger. Combine it with trend indicators for actionable signals.",
                "Comparing STDDEV values across different assets — a STDDEV of 500 on BTC is normal, but on a $0.01 altcoin it would be extreme. Always compare relative to the asset's price."
            },
            RecommendedDefaultParams = "Period 20 is the standard, matching the Bollinger Bands middle line. This captures roughly one month of daily data. Shorter periods (10) react faster but are noisier; longer periods (50) are smoother but slower to detect volatility changes."
        },

        ["ULCER"] = new IndicatorEducation
        {
            IndicatorId = "ULCER",
            WhatIsIt = "The Ulcer Index, created by Peter Martin, measures the depth and duration of drawdowns (declines from a peak). Unlike standard deviation which treats upside and downside volatility equally, the Ulcer Index only cares about DOWNSIDE pain — how far and how long the price falls from its recent high. Higher values mean more 'ulcer-inducing' drawdowns.",
            WhenToUse = "The Ulcer Index is perfect for evaluating risk: compare strategies by their Ulcer Index to see which one gives you less 'pain'. It is also useful for timing entries — when the Ulcer Index is very low, the asset has been stable and may be ready for a breakout.",
            BestPairedWith = new List<string>
            {
                "Max Drawdown analysis — Ulcer Index gives a continuous measure of drawdown pain, while Max Drawdown gives the single worst event. Together they paint the full risk picture.",
                "Sharpe/Sortino ratios — the Ulcer Index complements return-based metrics by focusing purely on downside risk.",
                "Moving averages — a rising Ulcer Index while price is above the 200-SMA suggests the uptrend is becoming 'bumpy' and less reliable."
            },
            CommonTraps = new List<string>
            {
                "Expecting the Ulcer Index to predict direction — it measures past pain, not future moves. It is a risk assessment tool, not a timing tool.",
                "Comparing Ulcer Index across very different timeframes — a daily Ulcer Index and a 1-minute Ulcer Index are not comparable.",
                "Ignoring that the Ulcer Index is slow to react — it takes several candles of sustained decline before the index rises significantly."
            },
            RecommendedDefaultParams = "Period 14 is the standard setting from Peter Martin's original design. It captures about three weeks of daily data, which is long enough to capture meaningful drawdowns but short enough to react to changes. For crypto's faster moves, some traders use 7–10."
        },

        // ═══════════════════════ VOLUME ═══════════════════════

        ["VOLSMA"] = new IndicatorEducation
        {
            IndicatorId = "VOLSMA",
            WhatIsIt = "The Volume Moving Average is simply the average trading volume over the last N candles. It creates a smooth line that shows the 'normal' level of activity. When current volume spikes well above this average, something important is happening.",
            WhenToUse = "VOLSMA is your go-to tool for confirming breakouts and spotting unusual activity. A breakout on 2× average volume is far more trustworthy than one on normal volume. It also helps identify 'quiet before the storm' periods when volume drops below average.",
            BestPairedWith = new List<string>
            {
                "Price action — a breakout above resistance is only convincing if volume is at least 1.5× the VOLSMA.",
                "OBV — VOLSMA shows the 'level' of volume, OBV shows the 'direction' of volume flow. Together they confirm conviction.",
                "Bollinger Bands — a Bollinger squeeze breakout confirmed by a volume spike above VOLSMA is one of the highest-probability setups."
            },
            CommonTraps = new List<string>
            {
                "Treating volume spikes as always bullish — a huge volume candle can be a selling climax just as easily as a buying frenzy. Check the candle direction.",
                "Using VOLSMA on very low-liquidity coins — a single whale trade can make volume look 10× normal, creating false signals.",
                "Ignoring the time-of-day effect — volume naturally varies by session. A 2× spike during low-liquidity hours may be less meaningful than a 1.5× spike during peak hours."
            },
            RecommendedDefaultParams = "Period 20 is the standard, matching the common SMA period. It captures about one month of daily volume, smoothing out single-day anomalies. For intraday trading, shorter periods (10) react faster to volume shifts."
        },

        ["VWAP"] = new IndicatorEducation
        {
            IndicatorId = "VWAP",
            WhatIsIt = "VWAP (Volume Weighted Average Price) is the average price weighted by volume — it tells you the 'true' average price at which the asset traded, giving more weight to prices where more volume occurred. Institutional traders use VWAP as a benchmark: buying below VWAP means getting a 'good deal', selling above means a 'premium'.",
            WhenToUse = "VWAP is most powerful on intraday timeframes (1m to 1h). Price above VWAP = bullish bias; price below VWAP = bearish bias. Many day traders only take longs above VWAP and shorts below it. It also acts as dynamic support/resistance.",
            BestPairedWith = new List<string>
            {
                "EMA — when VWAP and a short-term EMA (like 9 or 20) are close together, that zone becomes a very strong support/resistance area.",
                "Volume — VWAP bounces are more reliable when accompanied by a volume spike, showing institutional interest at that level.",
                "RSI — a pullback to VWAP with RSI in neutral territory (40-60) is a classic trend-continuation entry."
            },
            CommonTraps = new List<string>
            {
                "Using VWAP on weekly/monthly charts — VWAP is designed for intraday and resets each session. On longer timeframes it loses its meaning.",
                "Assuming VWAP always acts as support — in a strong downtrend, VWAP becomes resistance. Always respect the trend direction.",
                "Confusing VWAP with VWMA — VWAP resets daily and is cumulative from session start; VWMA is a rolling average that never resets."
            },
            RecommendedDefaultParams = "VWAP has no period parameter — it is calculated cumulatively from the start of each trading session. This is by design: institutional traders benchmark against the session VWAP, not a rolling average."
        },

        ["OBV"] = new IndicatorEducation
        {
            IndicatorId = "OBV",
            WhatIsIt = "On-Balance Volume (OBV) is a running total of volume: on up-candles, the volume is ADDED to the total; on down-candles, it is SUBTRACTED. The result is a line that shows whether volume is flowing INTO or OUT OF an asset. A rising OBV means buyers are in control; a falling OBV means sellers dominate.",
            WhenToUse = "OBV is best for spotting divergences: if price makes a new high but OBV does not, the rally is hollow (no volume backing). If price is flat but OBV is rising, accumulation is happening and a breakout may be coming.",
            BestPairedWith = new List<string>
            {
                "Price action — the most powerful OBV signal is divergence: price up, OBV down = weakening rally; price down, OBV up = hidden accumulation.",
                "MFI — both use volume, but MFI is bounded (0-100) while OBV is unbounded. MFI shows overbought/oversold, OBV shows cumulative flow.",
                "Moving averages — apply a 20-SMA to the OBV line itself to smooth it and spot trend changes more clearly."
            },
            CommonTraps = new List<string>
            {
                "Looking at the absolute OBV number — the actual value is meaningless. Only the DIRECTION and SLOPE of the OBV line matter.",
                "Using OBV on low-volume coins — if daily volume is tiny, a single trade can swing OBV dramatically, creating false signals.",
                "Expecting OBV to time entries precisely — OBV is a confirmation tool, not a timing tool. It tells you IF a move has conviction, not exactly WHEN it will happen."
            },
            RecommendedDefaultParams = "OBV has no period parameter — it is a cumulative indicator that runs from the start of the data. This is by design: the power of OBV is in its long-term accumulation/distribution pattern, not short-term fluctuations."
        },

        ["CMF"] = new IndicatorEducation
        {
            IndicatorId = "CMF",
            WhatIsIt = "The Chaikin Money Flow (CMF) measures buying and selling pressure by looking at where the close falls within each candle's high-low range, weighted by volume. If the close is near the high, it counts as buying pressure; near the low, selling pressure. The result oscillates around zero: above zero = buyers in control, below zero = sellers in control.",
            WhenToUse = "CMF is great for confirming trends and spotting accumulation/distribution. A sustained CMF above zero during an uptrend confirms the move has real buying behind it. A CMF divergence (price up, CMF down) warns of a potential reversal.",
            BestPairedWith = new List<string>
            {
                "OBV — both measure volume flow but differently: CMF is bounded and rate-based, OBV is cumulative. When both agree, the signal is very strong.",
                "Price action — a CMF cross above zero while price breaks above resistance is a high-conviction buy signal.",
                "ADX — CMF breakouts are more reliable when ADX confirms a trend is forming (ADX > 25)."
            },
            CommonTraps = new List<string>
            {
                "Using CMF on very low-volume coins — CMF relies on volume data; if volume is unreliable, CMF becomes noise.",
                "Treating CMF like an overbought/oversold oscillator — CMF is a flow indicator, not a mean-reversion tool. Sustained readings above zero are normal in strong trends.",
                "Ignoring the zero line — the most reliable CMF signal is the cross through zero, not the extreme readings."
            },
            RecommendedDefaultParams = "Period 20 is Marc Chaikin's original setting. It captures about one month of daily data, smoothing out single-day noise while still reacting to shifts in buying/selling pressure. Shorter periods (10) are used for faster signals in day trading."
        },

        ["FORCEINDEX"] = new IndicatorEducation
        {
            IndicatorId = "FORCEINDEX",
            WhatIsIt = "The Force Index, created by Dr. Alexander Elder, measures the 'force' behind a price move by multiplying the price change by the volume. A big price move on big volume = strong force; a big price move on tiny volume = weak force. The raw Force Index is then smoothed with an EMA.",
            WhenToUse = "The Force Index is excellent for confirming the strength of a move. A breakout backed by a high Force Index is likely to sustain; one with low Force Index is likely to fail. It also helps identify exhaustion: when Force Index peaks and starts falling while price is still rising, the move is losing steam.",
            BestPairedWith = new List<string>
            {
                "EMA — Elder recommended smoothing the Force Index with a 13-period EMA. The smoothed version is much more usable for signals.",
                "Volume — the raw Force Index IS volume × price change, so watching volume bars alongside helps you understand why the Force Index is rising or falling.",
                "RSI — Force Index shows the 'power' of a move, RSI shows the 'extension'. Together they help distinguish strong trends from overextended ones."
            },
            CommonTraps = new List<string>
            {
                "Using the raw (unsmoothed) Force Index — it is extremely noisy. Always apply at least a 2-period EMA smoothing.",
                "Comparing Force Index values across different assets — Force Index is proportional to price and volume, so a Force Index of 1000 on BTC is very different from 1000 on a small-cap coin.",
                "Ignoring the zero line — the most reliable Force Index signal is the cross through zero, which indicates a shift from buying to selling pressure or vice versa."
            },
            RecommendedDefaultParams = "Period 13 is Dr. Alexander Elder's original recommendation from his book 'Trading for a Living'. The 13-period EMA smoothing removes single-candle noise while preserving the underlying force signal. For day trading, some use 2–5 periods for faster signals."
        },

        ["ADL"] = new IndicatorEducation
        {
            IndicatorId = "ADL",
            WhatIsIt = "The Accumulation/Distribution Line (A/D Line) is a cumulative volume indicator that measures whether money is flowing INTO (accumulation) or OUT OF (distribution) an asset. It looks at where the close falls within the candle's high-low range: a close near the high adds to the line (accumulation), a close near the low subtracts from it (distribution).",
            WhenToUse = "The A/D Line is best for spotting divergences: if price is making new highs but the A/D Line is not, the rally is being 'distributed' (smart money is selling into strength). If price is falling but A/D is rising, accumulation is happening and a reversal may be near.",
            BestPairedWith = new List<string>
            {
                "OBV — both are cumulative volume indicators but calculate differently: A/D uses the close's position within the high-low range, OBV uses candle direction. When both agree, the volume signal is very strong.",
                "CMF — CMF is essentially the rate-of-change version of the A/D Line. A/D shows the cumulative picture, CMF shows the current pressure.",
                "Price action — the most powerful A/D signal is divergence at key support/resistance levels."
            },
            CommonTraps = new List<string>
            {
                "Looking at the absolute A/D value — like OBV, only the direction and slope of the A/D Line matter, not the actual number.",
                "Using A/D on low-liquidity coins — a single large trade can swing the A/D Line dramatically, creating false divergence signals.",
                "Confusing A/D with OBV — they look similar but can diverge. A/D focuses on WHERE the close is within the range; OBV focuses on WHETHER the candle was up or down."
            },
            RecommendedDefaultParams = "The A/D Line has no period parameter — it is a cumulative indicator, just like OBV. This is by design: the power of A/D is in its long-term accumulation/distribution pattern. The indicator was developed by Marc Chaikin in the 1970s."
        },

        // ═══════════════════════ TREND ═══════════════════════

        ["AROON"] = new IndicatorEducation
        {
            IndicatorId = "AROON",
            WhatIsIt = "The Aroon indicator has two lines: Aroon Up and Aroon Down. Aroon Up measures how many periods have passed since the highest high; Aroon Down measures how many periods since the lowest low. Both range from 0 to 100. Aroon Up = 100 means a new high was made THIS candle; Aroon Up = 0 means no new high in the entire lookback period.",
            WhenToUse = "Aroon is excellent for detecting the START of a new trend. When Aroon Up crosses above 70 while Aroon Down drops below 30, a new uptrend is likely forming. The crossover of the two lines is the key signal.",
            BestPairedWith = new List<string>
            {
                "ADX — Aroon tells you WHEN a trend starts, ADX tells you HOW STRONG it is. Use Aroon for entry timing, ADX for confirmation.",
                "Moving averages — an Aroon Up crossover confirmed by price crossing above the 20-EMA is a high-probability long entry.",
                "Volume — Aroon crossovers backed by above-average volume are more reliable than those on thin volume."
            },
            CommonTraps = new List<string>
            {
                "Using Aroon in ranging markets — both lines will constantly bounce between 0 and 100, generating dozens of false crossovers.",
                "Only looking at one line — the SIGNAL is the relationship between Aroon Up and Aroon Down, not the absolute value of either one alone.",
                "Ignoring the 50 level — when both lines are hovering around 50, the market is directionless. Wait for a decisive cross above 70 or below 30."
            },
            RecommendedDefaultParams = "Period 25 is Tushar Chande's original setting. It captures about one month of daily data, which is long enough to identify meaningful highs/lows but short enough to react to new trends. For faster signals, some traders use 14."
        },

        ["SAR"] = new IndicatorEducation
        {
            IndicatorId = "SAR",
            WhatIsIt = "Parabolic SAR (Stop And Reverse) places dots above or below the price. In an uptrend, dots appear BELOW the candles; in a downtrend, ABOVE. The dots accelerate toward the price as the trend continues, creating a natural trailing stop level. When the dots flip sides, it signals a trend reversal.",
            WhenToUse = "Parabolic SAR is best used as a trailing stop in trending markets. Enter when the dots flip below price (uptrend), and stay in the trade as long as the dots remain below. Exit when the dots flip above. It works best in strong, sustained trends.",
            BestPairedWith = new List<string>
            {
                "ADX — Parabolic SAR generates many false signals in choppy markets. Only use SAR when ADX is above 25, confirming a real trend exists.",
                "ATR — use ATR to set your initial stop distance, then let SAR manage the trailing stop. This combines ATR's volatility awareness with SAR's trend-following logic.",
                "EMA — a SAR flip that occurs while price is above the 50-EMA is more reliable than one against the trend."
            },
            CommonTraps = new List<string>
            {
                "Using SAR in ranging markets — this is the #1 mistake. SAR will flip constantly in chop, generating many losing trades. Always filter with ADX or a trend indicator.",
                "Treating every SAR flip as a trade signal — in strong trends, SAR flips are reliable; in weak trends, they are noise. Context matters.",
                "Ignoring the acceleration factor — the default AF of 0.02 means SAR starts slow and speeds up. In very fast markets, the SAR may lag initially and catch up quickly."
            },
            RecommendedDefaultParams = "The standard settings are AF start = 0.02 and AF max = 0.2, from J. Welles Wilder's original design. The acceleration factor starts at 0.02 and increases by 0.02 with each new extreme, up to a maximum of 0.2. These values work well across most markets and timeframes."
        },

        ["SUPERTREND"] = new IndicatorEducation
        {
            IndicatorId = "SUPERTREND",
            WhatIsIt = "SuperTrend is a trend-following indicator based on ATR. It draws a single line that sits below the price in an uptrend and above the price in a downtrend. When price closes above the line, the trend flips bullish; when it closes below, the trend flips bearish. The line acts as a dynamic support/resistance and trailing stop.",
            WhenToUse = "SuperTrend is excellent for trend-following and trailing stops. In a trending market, stay long as long as price is above the SuperTrend line, and short when below. It works best on 1h, 4h, and daily charts where trends are well-defined.",
            BestPairedWith = new List<string>
            {
                "ADX — SuperTrend signals are more reliable when ADX is above 25. In weak trends (ADX < 20), SuperTrend will generate many false flips.",
                "EMA 200 — only take SuperTrend bullish flips when price is above the 200-EMA (long-term trend filter). This dramatically reduces false signals.",
                "RSI — a SuperTrend flip confirmed by RSI crossing above 50 (for longs) or below 50 (for shorts) adds momentum confirmation."
            },
            CommonTraps = new List<string>
            {
                "Using SuperTrend in ranging markets — like SAR, SuperTrend will flip back and forth constantly in chop. Always check ADX or market structure first.",
                "Entering on every flip without confirmation — a SuperTrend flip alone is not enough. Wait for a candle close, volume confirmation, or alignment with a higher-timeframe trend.",
                "Using too short a period — a 5-period SuperTrend on a 1-minute chart will generate dozens of false signals per hour. Longer periods (10–20) filter out noise."
            },
            RecommendedDefaultParams = "Period 10 with an ATR multiplier of 3 is the most widely used setting. The 10-period ATR captures short-term volatility, and the 3× multiplier gives enough room for normal price fluctuations without getting stopped out by noise. For longer-term trading, period 20 with multiplier 3 works well."
        },

        ["ICHIMOKU"] = new IndicatorEducation
        {
            IndicatorId = "ICHIMOKU",
            WhatIsIt = "Ichimoku Kinko Hyo ('one-glance equilibrium chart') is a complete trading system in five lines: (1) Tenkan-sen (Conversion Line): average of the last 9 periods' high/low — shows short-term momentum. (2) Kijun-sen (Base Line): average of the last 26 periods' high/low — shows medium-term trend. (3) Senkou Span A (Leading Span A): average of Tenkan and Kijun, plotted 26 periods ahead. (4) Senkou Span B (Leading Span B): average of the last 52 periods' high/low, plotted 26 periods ahead. (5) Chikou Span (Lagging Span): the current close, plotted 26 periods back. The area between Senkou Span A and B is called the 'Cloud' (Kumo) — it acts as dynamic support/resistance.",
            WhenToUse = "Ichimoku is best on 4h and daily charts for swing and position trading. The key signals are: price above the Cloud = bullish; price below = bearish; Tenkan crossing above Kijun = buy signal; price breaking through the Cloud = trend change. The Cloud's thickness shows the strength of support/resistance.",
            BestPairedWith = new List<string>
            {
                "RSI — when price breaks above the Cloud and RSI is above 50, the breakout has momentum backing. RSI divergence near the Cloud edges warns of potential reversals.",
                "Volume — a Cloud breakout on above-average volume is far more reliable. Thin-volume breakouts often fail and fall back into the Cloud.",
                "Moving averages — the 200-SMA can confirm the long-term trend direction, while Ichimoku handles the medium-term timing."
            },
            CommonTraps = new List<string>
            {
                "Using Ichimoku on very short timeframes (1m, 5m) — the Cloud becomes extremely noisy and generates constant false signals. Ichimoku was designed for daily and weekly charts.",
                "Ignoring the Chikou Span — many beginners overlook it, but the Chikou Span is a powerful confirmation tool: if the Chikou is above the price from 26 periods ago, the uptrend is confirmed.",
                "Trading inside the Cloud — the Cloud represents a 'no-trade zone'. Price inside the Cloud means the market is directionless. Wait for a clear break above or below."
            },
            RecommendedDefaultParams = "The original settings are 9/26/52, created by Goichi Hosoda in the 1930s. Tenkan = 9 (about 1.5 weeks), Kijun = 26 (about 1 month), Senkou Span B = 52 (about 2 months). These were based on Japanese trading weeks of 6 days. Despite crypto trading 24/7, the original 9/26/52 settings remain the most widely used and tested."
        },

        ["VORTEX"] = new IndicatorEducation
        {
            IndicatorId = "VORTEX",
            WhatIsIt = "The Vortex Indicator has two lines: VI+ (positive vortex) and VI− (negative vortex). VI+ measures the strength of upward movement by summing the distance from each candle's low to the next candle's high; VI− measures downward movement from each high to the next low. When VI+ crosses above VI−, upward momentum is taking over; when VI− crosses above VI+, downward momentum dominates.",
            WhenToUse = "The Vortex Indicator is designed to catch the START of a new trend. The crossover of VI+ and VI− is the primary signal. It works well on 1h, 4h, and daily charts for swing trading. Combine it with a trend filter for best results.",
            BestPairedWith = new List<string>
            {
                "ADX — Vortex crossovers are more reliable when ADX is rising, confirming that a trend is actually forming rather than just chop.",
                "Moving averages — a VI+ crossover above the 20-EMA adds trend confirmation. Avoid taking Vortex signals against the EMA direction.",
                "Volume — Vortex crossovers backed by a volume spike are more likely to sustain than those on declining volume."
            },
            CommonTraps = new List<string>
            {
                "Using Vortex in ranging markets — the two lines will cross back and forth constantly, generating many false signals. Always check if the market is trending first.",
                "Ignoring the distance between VI+ and VI− — a wide gap between the two lines indicates a strong trend; a narrow gap suggests the trend is weakening and a crossover may be coming.",
                "Taking every crossover as a trade — wait for the crossover to be confirmed by a candle close and, ideally, by volume or a trend filter."
            },
            RecommendedDefaultParams = "Period 14 is the standard setting from the Vortex Indicator's creators, Etienne Botes and Douglas Siepman. The 14-period window captures enough price action to identify meaningful trend shifts while filtering out single-candle noise. For faster signals, some traders use 7–10."
        },

        ["CHOP"] = new IndicatorEducation
        {
            IndicatorId = "CHOP",
            WhatIsIt = "The Choppiness Index measures whether the market is trending or 'choppy' (moving sideways in a tight range). It ranges from 0 to 100. Values above 61.8 indicate a choppy, range-bound market; values below 38.2 indicate a strong trend. The 38.2 and 61.8 thresholds are Fibonacci levels, chosen deliberately by the inventor.",
            WhenToUse = "The Choppiness Index is a FILTER, not a trigger. Use it to decide WHICH type of strategy to apply: when CHOP is above 61.8, use mean-reversion strategies (buy low, sell high); when CHOP is below 38.2, use trend-following strategies (buy breakouts, ride the trend).",
            BestPairedWith = new List<string>
            {
                "ADX — both measure trend quality but differently: ADX measures trend STRENGTH, CHOP measures market TYPE. When both agree (ADX > 25 AND CHOP < 38.2), the trend is very strong.",
                "Bollinger Band Width — both detect 'squeezes'. When CHOP is high AND BB Width is narrow, a breakout is imminent.",
                "Any trend-following strategy — use CHOP as a gate: only take trend-following signals when CHOP < 61.8. This simple filter dramatically reduces false signals."
            },
            CommonTraps = new List<string>
            {
                "Using CHOP as a directional indicator — it does NOT tell you if the market is going up or down, only whether it is trending or chopping. You need a separate indicator for direction.",
                "Ignoring the 61.8 threshold — this is the key level. Above 61.8, trend-following strategies will get chopped up. Below 38.2, mean-reversion strategies will get run over.",
                "Using CHOP on very short timeframes — on 1-minute charts, CHOP fluctuates wildly and becomes unreliable. It works best on 1h and above."
            },
            RecommendedDefaultParams = "Period 14 is the standard setting from E.W. Dreiss, the Australian econometrician who created the Choppiness Index. The 14-period window provides a good balance between responsiveness and noise filtering. The 38.2/61.8 Fibonacci thresholds are integral to the indicator's design and should not be changed."
        },

        // ═══════════════════════ FUNDAMENTAL ═══════════════════════

        ["FEARGREED"] = new IndicatorEducation
        {
            IndicatorId = "FEARGREED",
            WhatIsIt = "The Fear & Greed Index measures the overall emotional state of the crypto market on a scale from 0 (Extreme Fear) to 100 (Extreme Greed). It combines multiple factors: volatility, market momentum, social media sentiment, Bitcoin dominance, and trading volume. Unlike technical indicators, it is NOT calculated from candle data — it is fetched from the alternative.me API and updated daily.",
            WhenToUse = "The Fear & Greed Index is a contrarian sentiment tool: when the index is below 25 (Extreme Fear), the market is likely oversold and a bounce may be coming; when above 75 (Extreme Greed), the market is euphoric and a correction is likely. Use it as a macro filter, not a precise entry signal.",
            BestPairedWith = new List<string>
            {
                "RSI — when Fear & Greed shows 'Extreme Fear' AND RSI is below 30, the oversold signal is confirmed by both sentiment and price momentum.",
                "BTC Dominance — Fear & Greed combined with BTC Dominance helps identify market phases: high fear + rising BTC dominance = 'flight to safety' (bear market); low fear + falling dominance = altseason.",
                "Moving averages — use Fear & Greed as a macro filter: only take long signals from your MA strategy when Fear & Greed is below 50 (buying fear, not greed)."
            },
            CommonTraps = new List<string>
            {
                "LIMITED HISTORY: The Fear & Greed API only provides the last 100 days of data. This means backtesting strategies that use Fear & Greed beyond ~3 months is not possible. Treat backtest results involving this indicator with caution.",
                "Assuming 'Extreme Greed' means 'sell now' — in a strong bull market, the index can stay above 75 for weeks or months. It is a warning, not a timing signal.",
                "Using it as a standalone indicator — Fear & Greed is a sentiment gauge, not a trading system. Always combine it with technical analysis for actual entries and exits."
            },
            RecommendedDefaultParams = "Fear & Greed has no period parameter — it is a daily index fetched from the alternative.me API. The value updates once per day. In Meowgnal, the data is cached for 1 hour to avoid excessive API calls. Note: only the last 100 days of history are available."
        },

        ["BTCDOM"] = new IndicatorEducation
        {
            IndicatorId = "BTCDOM",
            WhatIsIt = "BTC Dominance measures Bitcoin's share of the total cryptocurrency market capitalization, expressed as a percentage. When BTC Dominance is rising, money is flowing into Bitcoin (often a sign of market uncertainty or 'flight to safety'). When it is falling while BTC price is stable or rising, money is rotating into altcoins ('altseason'). This is NOT calculated from candles — it is fetched from the CoinGecko API.",
            WhenToUse = "BTC Dominance is a macro cycle indicator. Use it to identify market phases: rising BTC Dominance + rising BTC price = early bull market (BTC leads); falling BTC Dominance + stable BTC price = altseason (alts outperform). It is most useful on daily and weekly timeframes.",
            BestPairedWith = new List<string>
            {
                "BTC Price — the classic 'altseason' setup: BTC Dominance falls while BTC price holds steady or rises. This means money is rotating from BTC into altcoins.",
                "Fear & Greed Index — combining BTC Dominance with Fear & Greed helps identify market phases: high fear + rising dominance = bear market; low fear + falling dominance = euphoric altseason.",
                "ETH/BTC pair — ETH/BTC is the most direct altcoin proxy. When BTC Dominance falls AND ETH/BTC rises, altseason is confirmed."
            },
            CommonTraps = new List<string>
            {
                "LIMITED HISTORY: The CoinGecko API only provides the CURRENT snapshot of BTC Dominance, not historical data. This means you CANNOT backtest strategies that use BTC Dominance — it only works for live/forward analysis.",
                "Assuming falling BTC Dominance is always bullish for alts — if BTC Dominance falls because BTC is crashing, alts will crash too. Always check BTC price direction alongside dominance.",
                "Ignoring stablecoin effects — BTC Dominance can be distorted by stablecoin market cap changes. A rise in USDT/USDC market cap can lower BTC Dominance without any real rotation happening."
            },
            RecommendedDefaultParams = "BTC Dominance has no period parameter — it is a real-time percentage fetched from the CoinGecko API. In Meowgnal, the value is cached for 5 minutes. IMPORTANT: CoinGecko's free API only returns the current snapshot, so historical backtesting with this indicator is not supported."
        },

        ["FUNDING"] = new IndicatorEducation
        {
            IndicatorId = "FUNDING",
            WhatIsIt = "The Funding Rate is a mechanism unique to perpetual futures contracts. It is a periodic payment between long and short traders that keeps the perpetual contract price close to the spot price. When funding is POSITIVE, longs pay shorts (the market is bullish-leaning); when NEGATIVE, shorts pay longs (bearish-leaning). Extreme funding rates signal overcrowded positioning. This is NOT calculated from candles — it is fetched from Binance Futures or Hyperliquid APIs.",
            WhenToUse = "Funding Rate is a sentiment and positioning tool. Extreme positive funding (> 0.1% per 8h) means longs are overcrowded and a squeeze-down is possible; extreme negative funding means shorts are overcrowded and a squeeze-up is possible. Use it as a contrarian signal at extremes, and as a trend confirmation at moderate levels.",
            BestPairedWith = new List<string>
            {
                "Funding Rate + RSI — when funding is extremely positive AND RSI is above 70, the market is both overleveraged and overbought. This is a high-probability reversal setup.",
                "Funding Rate + Open Interest — when both funding AND OI are rising, new money is flowing into longs (strong bullish conviction). When funding rises but OI falls, longs are taking profit (weakening conviction).",
                "Funding Rate + Price action — a funding rate reset (from positive to near-zero) after a strong move often marks the end of the move. Watch for funding normalization as an exit signal."
            },
            CommonTraps = new List<string>
            {
                "LIMITED HISTORY: Binance provides ~100 days of funding rate history (every 8 hours). Hyperliquid provides hourly data but with a shorter window. Backtesting funding-based strategies beyond a few months is not feasible.",
                "Assuming positive funding means 'buy' — positive funding means longs are ALREADY crowded. At extreme levels, it is actually a bearish signal (longs are vulnerable to a squeeze).",
                "Ignoring the funding interval — Binance funding is every 8 hours, Hyperliquid is hourly. A 0.01% hourly rate on Hyperliquid is equivalent to 0.08% per 8h on Binance. Always normalize before comparing."
            },
            RecommendedDefaultParams = "Funding Rate has no period parameter — it is fetched directly from the exchange API (Binance Futures or Hyperliquid). Binance updates every 8 hours; Hyperliquid updates hourly. In Meowgnal, the data is cached for 30 minutes. Note: only ~100 days of history are available from Binance; Hyperliquid history is more limited."
        },

        ["OI"] = new IndicatorEducation
        {
            IndicatorId = "OI",
            WhatIsIt = "Open Interest (OI) is the total number of outstanding (unclosed) futures contracts, expressed in USD. When OI rises, new money is entering the market (new positions are being opened). When OI falls, positions are being closed (money is leaving). OI is NOT calculated from candles — it is fetched from Binance Futures or Hyperliquid APIs.",
            WhenToUse = "Open Interest is a trend-strength and liquidation-risk indicator. Rising OI + rising price = strong bullish trend (new longs entering). Rising OI + falling price = strong bearish trend (new shorts entering). Falling OI + rising price = trend weakening (shorts covering, not new buying). Very high OI relative to volume signals liquidation risk.",
            BestPairedWith = new List<string>
            {
                "OI + Funding Rate — the most powerful combination: rising OI + rising funding = aggressive long accumulation (bullish); rising OI + falling funding = aggressive short accumulation (bearish). Divergences between OI and funding signal potential reversals.",
                "OI + Volume — a breakout with rising OI AND rising volume is a 'real' breakout backed by new positions. A breakout with rising price but falling OI is likely a short-covering rally that will fade.",
                "OI + Price action — a sudden spike in OI followed by a sharp price move often indicates a liquidation cascade. Watch for OI drops after large candles as a signal that the move may be exhausted."
            },
            CommonTraps = new List<string>
            {
                "LIMITED HISTORY: Binance provides OI history at 5-minute, 1-hour, and 1-day intervals, but only for a limited window (~30 days at 1h granularity). Hyperliquid only provides the CURRENT snapshot — no history at all. Backtesting OI-based strategies is very limited.",
                "Confusing OI with volume — Volume counts every trade (including closes); OI only counts OPEN positions. A high-volume day can actually DECREASE OI if traders are closing positions.",
                "Assuming high OI is always bullish — very high OI means many leveraged positions that can be liquidated. A sudden price move against the crowd can trigger a cascade of liquidations, amplifying the move."
            },
            RecommendedDefaultParams = "Open Interest has no period parameter — it is a snapshot value fetched from the exchange API. Binance provides historical OI at 1-hour intervals (limited to ~500 data points). Hyperliquid provides only the current value. In Meowgnal, the data is cached for 30 minutes. IMPORTANT: due to limited history, OI is best used as a live analysis tool, not a backtesting indicator."
        },
    };
}