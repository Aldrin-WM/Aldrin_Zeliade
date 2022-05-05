using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Product;
using Zeliade.Common;
using System.Collections;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
  
    public class DataSheetContainer : IEnumerable<Tuple<Symbol, DataQuoteSheet>>
    {
        private const string XllName = "DataSheetContainer";

        private readonly Dictionary<Symbol, DataQuoteSheet> _data;
        private readonly DateTime _asof;

        public DateTime Asof { get { return _asof; } }

        [WorksheetFunction(XllName + ".New")]
        public DataSheetContainer(DateTime asof)
        {
            _data = new Dictionary<Symbol, DataQuoteSheet>();
            _asof = asof;
        }

        [WorksheetFunction(XllName + ".AddSheet")]
        public DataSheetContainer AddSheet(Symbol ticker, DataQuoteSheet sheet)
        {
            Require.ArgumentNotNull(ticker, "ticker");
            Require.ArgumentNotNull(sheet, "sheet");
            Require.ArgumentRange(ValueRange.Equals(_asof), sheet.SpotDate, "sheet.SpotDate");

            if (_data.ContainsKey(ticker))
            {
                throw new ArgumentException(string.Format("The sheet for symbol {0} is already registered !", ticker));
            }
            _data.Add(ticker, sheet);
            return this;
        }

        public DataQuoteSheet Get(Symbol ticker)
        {
            DataQuoteSheet output = null;
            if (!_data.TryGetValue(ticker, out output))
            {
                throw new ArgumentException(string.Format("The sheet for symbol {0} is not registered !", ticker));
            }
            return output;
        }

        public IEnumerator<Tuple<Symbol, DataQuoteSheet>> GetEnumerator()
        {
            var l = _data.Select(k => Tuple.Create(k.Key, k.Value)).ToList();
            return l.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
