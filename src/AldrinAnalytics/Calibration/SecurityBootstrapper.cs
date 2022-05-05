using System;
using Zeliade.Finance.Common.Calibration;
using System.Linq;
using AldrinAnalytics.Pricers;
using System.Collections.Generic;
using Zeliade.Common;

namespace AldrinAnalytics.Calibration
{
    public class SecurityBootstrapper<T> : BidAskBootstrapper<T> where T: Security
    {      

        protected override T InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            Require.ArgumentNotNull(sheet, "sheet");
            Require.ArgumentNotNull(sheet.Data, "sheet.Data");
            Require.ArgumentListCount(1, sheet.Data, "sheet.Data");
            Require.ArgumentIsInstanceOf<T>(sheet.Data.First(), "sheet.Data.First()");

            var mid = sheet.Data.First().Quotes.Where(x => x is Q).ToList();
            if (mid.Count > 0)
            {
                var qref = (T)sheet.Data.First();
                var qcpy = (T)qref.DeepCopy();
                qcpy.ClearQuotes();
                qcpy.AddQuote(qref.GetQuote<Q>());
                return qcpy;
            }
            else
            {
                return null;
            }
        }

    }
}
