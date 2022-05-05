using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Mrc;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{

    public class RepoCurveBootstrapper : BidAskBootstrapper<IRepoCurve>
    {
        private const string XllName = "RepoCurveBootstrapper";

        [WorksheetFunction(XllName + ".New")]
        public RepoCurveBootstrapper()
        { }

        protected override IRepoCurve InternalBootstrap<Q>(DataQuoteSheet sheet) 
        {
            Require.ArgumentNotNull(sheet, "sheet");
            var pillars = new List<DateTime>();
            var rates = new List<double>();
            Ticker ticker = null;
            IDayCountFraction dcf = null;
            foreach (var item in sheet.Data)
            {
                var repo = item as RepoRate;
                if (repo == null)
                    throw new ArgumentException(string.Format("The sheet should contains only instruments of type {0} but contains also {1}", typeof(RepoRate).Name, repo.GetType()));

                Q repoQuote = default(Q);
                try
                {
                    repoQuote = repo.GetQuote<Q>();
                }
                catch (Exception e)
                {
                    return null;
                }

                var T = repo.DayCount.Count(sheet.SpotDate, repo.Pillar);
                var R = Math.Log(1 + repoQuote.Value * T) / T;

                rates.Add(R);
                pillars.Add(repo.Pillar);
                ticker = repo.Underlying; // TODO CHECK UNIQUENESS
                dcf = repo.DayCount; // TODO CHECK UNIQUENESS
                // TODO TAKE INTO ACCOUNT COMPOUNDING
            }

            var curve = new RepoCurve(ticker
                , sheet.SpotDate
                , pillars
                , rates
                , dcf);

            curve.BaseSheet = sheet;

            return curve;
        }

    }
}
