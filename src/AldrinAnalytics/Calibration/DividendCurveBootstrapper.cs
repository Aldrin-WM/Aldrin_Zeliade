using System;
using System.Linq;
using System.Collections.Generic;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{

    public class DividendCurveBootstrapper : BidAskBootstrapper<IDividendCurve>
    {

        private const string XllName = "DividendCurveBootstrapper";

        [WorksheetFunction(XllName + ".New")]
        public DividendCurveBootstrapper()
        { }

        protected override IDividendCurve InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            Require.ArgumentNotNull(sheet, "sheet");
            var data = new List<DividendData>();

            // TODO : PLUS RESTRICTIF !!!
            var sheetDiv = sheet.Data.Where(x => x is DividendEstimate).Cast<DividendEstimate>().ToList();
            var sheetCoarse = sheet.Data.Where(x => x is DividendCoarse).Cast<DividendCoarse>().ToList();
            var sheetAllIn = sheet.Data.Where(x => x is AllIn).Cast<AllIn>().ToList();
            var sheetAllInCoarse = sheet.Data.Where(x => x is AllInCoarse).Cast<AllInCoarse>().ToList();

            Ensure.That(sheetDiv.Count == sheetAllIn.Count
                , Error.Msg("The input sheet should contain the same number of dividend estimates and allins but has {0} divs and {1} allins", sheetDiv.Count, sheetAllIn.Count));
            Ensure.That(sheetCoarse.Count == sheetAllInCoarse.Count
                , Error.Msg("The input sheet should contain the same number of dividend coarses and allins coarses but has {0} divs coarses and {1} allins coarses", sheetCoarse.Count, sheetAllInCoarse.Count));

            sheetDiv.Sort(GetComparison<DividendEstimate>());
            sheetCoarse.Sort(GetComparison<DividendCoarse>());
            sheetAllIn.Sort(GetComparison<AllIn>());
            sheetAllInCoarse.Sort(GetComparison<AllInCoarse>());


            for (int i = 0; i < sheetDiv.Count; i++)
            {
                var div = sheetDiv[i] as DividendEstimate;
                var allin = sheetAllIn[i] as AllIn;

                if (!div.ExDate.Equals(allin.Pillar))
                    throw new ArgumentException(string.Format("Inconsistent DividendCoarse and AllInCoarse found : got different pillars {0} and {1} at index {2}", div.ExDate, allin.Pillar, i));

                // Exctract quote according to the market context
                Q divQuote = default(Q);
                Q allIn = default(Q);
                try
                {
                    divQuote = div.GetQuote<Q>();
                    allIn = allin.GetQuote<Q>();
                }
                catch (Exception e)
                {
                    return null;
                }

                var bean = new DividendData()
                {
                    GrossAmount = divQuote.Value,
                    AllIn = allIn.Value,
                    PaymentCurrency = div.Ccy,
                    PaymentDate = div.PaymentDate,
                    ExDate = div.ExDate,
                    Ticker = div.Underlying,
                };

                data.Add(bean);
            }

            var lastDate = data.Count>0 ? data.Last().ExDate : DateTime.MinValue;

            for (int i = 0; i < sheetCoarse.Count; i++)
            {
                var div = sheetCoarse[i] as DividendCoarse;
                var allin = sheetAllInCoarse[i] as AllInCoarse;

                if (!div.ExDate.Equals(allin.ExDate))
                    throw new ArgumentException(string.Format("Inconsistent DividendCoarse and AllInCoarse found : got different ex date {0} and {1} at index {2}", div.ExDate, allin.ExDate, i));

                // Exctract quote according to the market context
                Q divQuote = default(Q);
                Q allIn = default(Q);
                try
                {
                    divQuote = div.GetQuote<Q>();
                    allIn = allin.GetQuote<Q>();
                }
                catch (Exception e)
                {
                    return null;
                }

                if (div.ExDate < lastDate)
                    continue;

                var bean = new DividendData()
                {
                    GrossAmount = divQuote.Value,
                    AllIn = allIn.Value,
                    PaymentCurrency = div.Ccy,
                    PaymentDate = div.ExDate,
                    ExDate = div.ExDate,
                    Ticker = div.Underlying,
                };

                data.Add(bean);
            }

            var curve = new DividendCurve(sheet.SpotDate, data);
            return curve;
        }



        private Comparison<T> GetComparison<T>() where T : IHavePillar
        {
            return (x, y) =>
            {
                if (x.Pillar < y.Pillar)
                    return -1;
                else if (x.Pillar == y.Pillar)
                    return 0;
                else
                    return 1;
            };
        }

        //private Comparison<T> GetTenorComparison<T>() where T : IHaveTenor
        //{
        //    return (x, y) =>
        //    {
        //        if (x.Tenor.YearDuration < y.Tenor.YearDuration)
        //            return -1;
        //        else if (x.Tenor.YearDuration == y.Tenor.YearDuration)
        //            return 0;
        //        else
        //            return 1;
        //    };
        //}

    }
}
