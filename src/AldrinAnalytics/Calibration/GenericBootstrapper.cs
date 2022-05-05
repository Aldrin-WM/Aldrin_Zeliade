using System;
using System.Collections.Generic;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Calibration;

namespace AldrinAnalytics.Calibration
{
    public interface IGenericBootstrapper<T>
    {
        Dictionary<Type, T> Bootstrap(DataQuoteSheet sheet);
        T Bootstrap<Q>(DataQuoteSheet sheet) where Q : IDataQuote;
    }
}
