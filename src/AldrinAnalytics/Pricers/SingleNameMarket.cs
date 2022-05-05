using System;
using System.Collections.Generic;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
using Zeliade.Finance.Common.Calibration;
#endif

namespace AldrinAnalytics.Pricers
{
    public interface ITickerDictionary
    {
        SingleNameTicker GetTicker(string ticker);
    }

    public class SingleNameMarket : GenericMarket<SingleNameTicker, SingleNameSecurity>, ITickerDictionary
    {
        private const string XllName = "SingleNameMarket";

        private readonly Dictionary<string, SingleNameSecurity> _data;

        [WorksheetFunction(XllName + ".New")]
        public SingleNameMarket(DateTime marketDate)
            : base(marketDate)
        {
            _data = new Dictionary<string, SingleNameSecurity>();
        }

        [WorksheetFunction(XllName + ".Get")]
        public SingleNameSecurity Get(string ticker)
        {
            SingleNameSecurity security = null;
            if (!_data.TryGetValue(ticker, out security))
            {
                throw new ArgumentException(string.Format("The single name {0} is not registered in the single name market !", security.SingleName));

            }
            return security;
        }

        [WorksheetFunction(XllName + ".GetTicker")]
        public SingleNameTicker GetTicker(string ticker)
        {
            SingleNameSecurity security = null;
            if (!_data.TryGetValue(ticker, out security))
            {
                throw new ArgumentException(string.Format("The single name {0} is not registered in the single name market !", ticker));

            }
            return security.SingleName;
        }

        public bool Contains(string name)
        {
            return _data.ContainsKey(name);
        }

        [WorksheetFunction(XllName + ".AddSecurity")]
        public SingleNameMarket AddSecurity(SingleNameSecurity security)
        {
            Require.ArgumentNotNull(security, "security");

            if (_data.ContainsKey(security.SingleName.Name))
            {
                throw new ArgumentException(string.Format("The single name {0} is already registered in the single name market !", security.SingleName.Name));
            }
            _data.Add(security.SingleName.Name, security);

            var sheet = new DataQuoteSheet(MarketDate, new IInstrument[] { security });
            AddSheet(security.SingleName, sheet, new SecurityBootstrapper<SingleNameSecurity>());
            return this;
        }
       
    }
}
