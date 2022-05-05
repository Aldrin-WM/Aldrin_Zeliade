using System;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Common;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using Zeliade.Finance.Mrc;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class HistoFixings
    {
        private const string XllName = "HistoFixings";

        [WorksheetFunction(XllName+ ".GetEquityFixings")]
        public static HistoricalFixings GetEquityFixings(DateTime[] dates
            , string[] tickers
            , double[] fixing
            , ITickerDictionary tickerToSecurity
            )
        {
            Require.ArgumentNotNull(dates, nameof(dates));
            Require.ArgumentNotNull(tickers, nameof(tickers));
            Require.ArgumentNotNull(fixing, nameof(fixing));
            Require.ArgumentNotNull(tickerToSecurity, nameof(tickerToSecurity));
            Require.ArgumentNotEmpty(dates, nameof(dates));
            Require.ArgumentEqualArrayLength(dates, tickers, nameof(dates), nameof(tickers));
            Require.ArgumentEqualArrayLength(dates, fixing, nameof(dates), nameof(fixing));

            var output = new HistoricalFixings();

            for (int i = 0; i < dates.Length; i++)
            {
                var sheet = output.DailySheet(dates[i]);
                if (sheet==null)
                {
                    sheet = new DataQuoteSheet(dates[i]);
                    output.AddSheet(sheet);
                }

                var snTicker = tickerToSecurity.GetTicker(tickers[i]);
                var security = SingleNameSecurity.New(dates[i], fixing[i], fixing[i], fixing[i], snTicker);
                sheet.AddData(security);
            }

            return output;
        }

        [WorksheetFunction(XllName + ".GetOvernightFixings")]
        public static HistoricalFixings GetOvernightFixings(DateTime[] dates
            , string[] currency
            , double[] fixing
            )
        {
            Require.ArgumentNotNull(dates, nameof(dates));
            Require.ArgumentNotNull(currency, nameof(currency));
            Require.ArgumentNotNull(fixing, nameof(fixing));
            Require.ArgumentNotEmpty(dates, nameof(dates));
            Require.ArgumentEqualArrayLength(dates, currency, nameof(dates), nameof(currency));
            Require.ArgumentEqualArrayLength(dates, fixing, nameof(dates), nameof(fixing));

            var output = new HistoricalFixings();

            for (int i = 0; i < dates.Length; i++)
            {
                var sheet = output.DailySheet(dates[i]);
                if (sheet == null)
                {
                    sheet = new DataQuoteSheet(dates[i]);
                    output.AddSheet(sheet);
                }

                var ois = new OvernightIndexSwapQuote(dates[i],
                    Periods.Get("1D"),
                    0,
                    BusinessCenters.None,
                    BusinessDayConventions.None,
                    DayCountConventions.Get(DayCountConventions.Codings.Thirty360),
                    DayCountConventions.Get(DayCountConventions.Codings.Actual360),
                    currency[i]);

                // TODO CHECK VALUES
                ois.AddQuote(new BidQuote(dates[i], fixing[i]));
                ois.AddQuote(new AskQuote(dates[i], fixing[i]));
                ois.AddQuote(new MidQuote(dates[i], fixing[i]));

                sheet.AddData(ois);
            }

            return output;

        }

        [WorksheetFunction(XllName + ".GetLiborFixings")]
        public static HistoricalFixings GetLiborFixings(DateTime[] dates
            , string[] currency
            , string[] tenor
            , double[] fixing
            )
        {
            Require.ArgumentNotNull(dates, nameof(dates));
            Require.ArgumentNotNull(currency, nameof(currency));
            Require.ArgumentNotNull(tenor, nameof(tenor));
            Require.ArgumentNotNull(fixing, nameof(fixing));
            Require.ArgumentNotEmpty(dates, nameof(dates));
            Require.ArgumentEqualArrayLength(dates, currency, nameof(dates), nameof(currency));
            Require.ArgumentEqualArrayLength(dates, tenor, nameof(dates), nameof(tenor));
            Require.ArgumentEqualArrayLength(dates, fixing, nameof(dates), nameof(fixing));

            var output = new HistoricalFixings();

            for (int i = 0; i < dates.Length; i++)
            {
                var sheet = output.DailySheet(dates[i]);
                if (sheet == null)
                {
                    sheet = new DataQuoteSheet(dates[i]);
                    output.AddSheet(sheet);
                }

                var p = Periods.Get((string)tenor[i]);
                var libor = DepositQuote.StandardQuote(dates[i], p, 0
                    , BusinessCenters.None
                    , BusinessDayConventions.None
                    , DayCountConventions.Get(DayCountConventions.Codings.Actual360)
                    , currency[i]);

                // TODO CHECK VALUES
                libor.AddQuote(new BidQuote(dates[i], fixing[i]));
                libor.AddQuote(new AskQuote(dates[i], fixing[i]));
                libor.AddQuote(new MidQuote(dates[i], fixing[i]));

                sheet.AddData(libor);
            }

            return output;
        }

        [WorksheetFunction(XllName + ".GetFxFixings")]
        public static HistoricalFixings GetFxFixings(DateTime[] dates
            , string[] from
            , string[] to
            , double[] fixing
            )
        {
            Require.ArgumentNotNull(dates, nameof(dates));
            Require.ArgumentNotNull(from, nameof(from));
            Require.ArgumentNotNull(to, nameof(to));
            Require.ArgumentNotNull(fixing, nameof(fixing));
            Require.ArgumentNotEmpty(dates, nameof(dates));
            Require.ArgumentEqualArrayLength(dates, from, nameof(dates), nameof(from));
            Require.ArgumentEqualArrayLength(dates, to, nameof(dates), nameof(to));
            Require.ArgumentEqualArrayLength(dates, fixing, nameof(dates), nameof(fixing));

            var output = new HistoricalFixings();

            for (int i = 0; i < dates.Length; i++)
            {
                var sheet = output.DailySheet(dates[i]);
                if (sheet == null)
                {
                    sheet = new DataQuoteSheet(dates[i]);
                    output.AddSheet(sheet);
                }

                var ccyPair = new CurrencyPair(from[i], to[i]);
                var q = CurrencyPairSecurity.New(dates[i], fixing[i], fixing[i], fixing[i], ccyPair);

                sheet.AddData(q);
            }

            return output;
        }

    }
}
