using System;
using System.Collections.Generic;
using System.Linq;
using Meowgnal.Models;

namespace Meowgnal.Engine;

// Computes technical indicator values from OHLCV bars.
public static class IndicatorCalculator
{
    public static List<double> SMA(List<Bar> bars, int period)
    {
        var result = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            if (i < period - 1) { result.Add(double.NaN); continue; }
            double sum = 0;
            for (int j = i - period + 1; j <= i; j++) sum += (double)bars[j].Close;
            result.Add(sum / period);
        }
        return result;
    }

    public static List<double> EMA(List<Bar> bars, int period)
    {
        var result = new List<double>();
        double k = 2.0 / (period + 1);
        double ema = (double)bars[0].Close;
        for (int i = 0; i < bars.Count; i++)
        {
            if (i == 0) result.Add(ema);
            else
            {
                ema = (double)bars[i].Close * k + ema * (1 - k);
                result.Add(i < period - 1 ? double.NaN : ema);
            }
        }
        return result;
    }

    public static List<double> RSI(List<Bar> bars, int period = 14)
    {
        var result = new List<double>();
        double avgGain = 0, avgLoss = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            if (i == 0) { result.Add(double.NaN); continue; }
            double change = (double)(bars[i].Close - bars[i - 1].Close);
            double gain = change > 0 ? change : 0;
            double loss = change < 0 ? -change : 0;
            if (i < period) { result.Add(double.NaN); continue; }
            if (i == period)
            {
                double sumGain = 0, sumLoss = 0;
                for (int j = 1; j <= period; j++)
                {
                    double ch = (double)(bars[j].Close - bars[j - 1].Close);
                    if (ch > 0) sumGain += ch; else sumLoss -= ch;
                }
                avgGain = sumGain / period;
                avgLoss = sumLoss / period;
            }
            else
            {
                avgGain = (avgGain * (period - 1) + gain) / period;
                avgLoss = (avgLoss * (period - 1) + loss) / period;
            }
            double rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
            result.Add(100 - 100 / (1 + rs));
        }
        return result;
    }

    public static (List<double> MACD, List<double> Signal, List<double> Histogram) MACD(List<Bar> bars, int fast = 12, int slow = 26, int signal = 9)
    {
        var emaFast = EMA(bars, fast);
        var emaSlow = EMA(bars, slow);
        var macdLine = new List<double>();
        for (int i = 0; i < bars.Count; i++)
            macdLine.Add(double.IsNaN(emaFast[i]) || double.IsNaN(emaSlow[i]) ? double.NaN : emaFast[i] - emaSlow[i]);

        var signalLine = new List<double>();
        double k = 2.0 / (signal + 1);
        double ema = macdLine.FirstOrDefault(x => !double.IsNaN(x));
        for (int i = 0; i < macdLine.Count; i++)
        {
            if (double.IsNaN(macdLine[i])) { signalLine.Add(double.NaN); continue; }
            ema = macdLine[i] * k + ema * (1 - k);
            signalLine.Add(i < slow - 1 + signal - 1 ? double.NaN : ema);
        }

        var histogram = new List<double>();
        for (int i = 0; i < macdLine.Count; i++)
            histogram.Add(double.IsNaN(macdLine[i]) || double.IsNaN(signalLine[i]) ? double.NaN : macdLine[i] - signalLine[i]);

        return (macdLine, signalLine, histogram);
    }

    public static List<double> ATR(List<Bar> bars, int period = 14)
    {
        var result = new List<double>();
        double atr = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            if (i == 0) { result.Add(double.NaN); continue; }
            double tr = Math.Max((double)(bars[i].High - bars[i].Low),
                Math.Max(Math.Abs((double)(bars[i].High - bars[i - 1].Close)),
                         Math.Abs((double)(bars[i].Low - bars[i - 1].Close))));
            if (i < period) { result.Add(double.NaN); continue; }
            if (i == period)
            {
                double sum = 0;
                for (int j = 1; j <= period; j++)
                    sum += Math.Max((double)(bars[j].High - bars[j].Low),
                        Math.Max(Math.Abs((double)(bars[j].High - bars[j - 1].Close)),
                                 Math.Abs((double)(bars[j].Low - bars[j - 1].Close))));
                atr = sum / period;
            }
            else atr = (atr * (period - 1) + tr) / period;
            result.Add(atr);
        }
        return result;
    }

    public static (List<double> Upper, List<double> Middle, List<double> Lower) BBANDS(List<Bar> bars, int period = 20, double multiplier = 2)
    {
        var middle = SMA(bars, period);
        var upper = new List<double>();
        var lower = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            if (double.IsNaN(middle[i])) { upper.Add(double.NaN); lower.Add(double.NaN); continue; }
            double sumSq = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                double diff = (double)bars[j].Close - middle[i];
                sumSq += diff * diff;
            }
            double std = Math.Sqrt(sumSq / period);
            upper.Add(middle[i] + multiplier * std);
            lower.Add(middle[i] - multiplier * std);
        }
        return (upper, middle, lower);
    }

    public static (List<double> K, List<double> D) STOCH(List<Bar> bars, int period = 14, int smooth = 3)
    {
        var kList = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            if (i < period - 1) { kList.Add(double.NaN); continue; }
            double high = (double)bars[i].High, low = (double)bars[i].Low;
            for (int j = i - period + 1; j <= i; j++)
            {
                if ((double)bars[j].High > high) high = (double)bars[j].High;
                if ((double)bars[j].Low < low) low = (double)bars[j].Low;
            }
            double k = high == low ? 50 : ((double)bars[i].Close - low) / (high - low) * 100;
            kList.Add(k);
        }
        var dList = SMAFromList(kList, smooth);
        return (kList, dList);
    }

    private static List<double> SMAFromList(List<double> values, int period)
    {
        var result = new List<double>();
        for (int i = 0; i < values.Count; i++)
        {
            if (i < period - 1 || double.IsNaN(values[i])) { result.Add(double.NaN); continue; }
            double sum = 0; int count = 0;
            for (int j = i - period + 1; j <= i; j++)
                if (!double.IsNaN(values[j])) { sum += values[j]; count++; }
            result.Add(count == period ? sum / period : double.NaN);
        }
        return result;
    }

    public static List<double> ADX(List<Bar> bars, int period = 14)
    {
        var result = new List<double>();
        if (bars.Count < period * 2) { for (int i = 0; i < bars.Count; i++) result.Add(double.NaN); return result; }

        var trList = new List<double>();
        var plusDM = new List<double>();
        var minusDM = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            if (i == 0) { trList.Add(0); plusDM.Add(0); minusDM.Add(0); continue; }
            double tr = Math.Max((double)(bars[i].High - bars[i].Low),
                Math.Max(Math.Abs((double)(bars[i].High - bars[i - 1].Close)),
                         Math.Abs((double)(bars[i].Low - bars[i - 1].Close))));
            double upMove = (double)(bars[i].High - bars[i - 1].High);
            double downMove = (double)(bars[i - 1].Low - bars[i].Low);
            trList.Add(tr);
            plusDM.Add(upMove > downMove && upMove > 0 ? upMove : 0);
            minusDM.Add(downMove > upMove && downMove > 0 ? downMove : 0);
        }

        var atr = ATR(bars, period);
        var dxPlus = new List<double>();
        var dxMinus = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            if (double.IsNaN(atr[i]) || atr[i] == 0) { dxPlus.Add(double.NaN); dxMinus.Add(double.NaN); continue; }
            dxPlus.Add(100 * plusDM[i] / atr[i]);
            dxMinus.Add(100 * minusDM[i] / atr[i]);
        }

        var smoothPlus = SMAFromList(dxPlus, period);
        var smoothMinus = SMAFromList(dxMinus, period);
        var dx = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            if (double.IsNaN(smoothPlus[i]) || double.IsNaN(smoothMinus[i]) || (smoothPlus[i] + smoothMinus[i]) == 0) { dx.Add(double.NaN); continue; }
            dx.Add(100 * Math.Abs(smoothPlus[i] - smoothMinus[i]) / (smoothPlus[i] + smoothMinus[i]));
        }
        return SMAFromList(dx, period);
    }

    public static List<double> VOLSMA(List<Bar> bars, int period = 20)
    {
        var result = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            if (i < period - 1) { result.Add(double.NaN); continue; }
            double sum = 0;
            for (int j = i - period + 1; j <= i; j++) sum += (double)bars[j].Volume;
            result.Add(sum / period);
        }
        return result;
    }

    public static List<double> VWAP(List<Bar> bars)
    {
        var result = new List<double>();
        double cumVol = 0, cumPV = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            double typical = ((double)bars[i].High + (double)bars[i].Low + (double)bars[i].Close) / 3;
            cumVol += (double)bars[i].Volume;
            cumPV += typical * (double)bars[i].Volume;
            result.Add(cumVol == 0 ? double.NaN : cumPV / cumVol);
        }
        return result;
    }
}