using System.Collections.Generic;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>
/// Static repository of plain-English educational content for technical indicators.
/// Only Moving Averages and Oscillators are filled in this phase;
/// other categories show a "coming soon" message in the UI.
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
                "ATR — since DMA is fast, use ATR-based stops to avoid getting stopped out by noise.",
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
    };
}