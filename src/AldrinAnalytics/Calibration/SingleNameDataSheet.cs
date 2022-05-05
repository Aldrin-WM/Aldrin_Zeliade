using AldrinAnalytics.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    public class SingleNameDataSheet : DataQuoteSheet
    {
        private const string XllName = "SingleNameDataSheet";

        private readonly Dictionary<string, SingleNameTicker> _tickers;

        [WorksheetFunction(XllName+".New")]
        public SingleNameDataSheet(DateTime quoteDate) : base(quoteDate)
        {
            _tickers = new Dictionary<string, SingleNameTicker>();
        }

        public override DataQuoteSheet AddData(IInstrument quote)
        {
            var sn = Require.ArgumentIsInstanceOf<SingleNameSecurity>(quote, "quote");
            if (_tickers.ContainsKey(sn.SingleName.Name))
            {
                throw new ArgumentException(string.Format("The single name security {0} is already registered in the sheet !", sn.SingleName.Name));
            }
            _tickers.Add(sn.SingleName.Name, sn.SingleName);
            return base.AddData(quote);
        }

        [WorksheetFunction(XllName+ ".GetTicker")]
        public SingleNameTicker GetTicker(string ticker)
        {
            if (!_tickers.ContainsKey(ticker))
            {
                throw new ArgumentException(string.Format("The single name security {0} is not registered in the sheet !", ticker));
            }
            return _tickers[ticker];
        }

    }
}
