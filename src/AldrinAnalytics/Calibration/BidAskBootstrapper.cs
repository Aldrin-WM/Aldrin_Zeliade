using System;
using System.Collections.Generic;
using Zeliade.Finance.Common.Calibration;

namespace AldrinAnalytics.Calibration
{
    public abstract class BidAskBootstrapper<T> : IGenericBootstrapper<T> where T:class
    {

        public Dictionary<Type, T> Bootstrap(DataQuoteSheet sheet)
        {
            var output = new Dictionary<Type, T>();
            AddCurve<MidQuote>(sheet, output);
            AddCurve<BidQuote>(sheet, output);
            AddCurve<AskQuote>(sheet, output);

            return output;
        }

        private void AddCurve<Q>(DataQuoteSheet sheet, Dictionary<Type, T> dico) where Q : IDataQuote
        {
            var midCurve = InternalBootstrap<Q>(sheet);
            if (midCurve != null)
                dico.Add(typeof(Q), midCurve);
            else
            {
                // TODO WARNING
            }
        }

        protected abstract T InternalBootstrap<Q>(DataQuoteSheet sheet) where Q : IDataQuote;

        public T Bootstrap<Q>(DataQuoteSheet sheet) where Q : IDataQuote
        {
            return  InternalBootstrap<Q>(sheet);
        }
    }
}
