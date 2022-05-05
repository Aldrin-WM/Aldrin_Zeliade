using System;
using Zeliade.Finance.Common.Product;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Instruments
{
    public interface ICollateralScheme
    {
    }

        
    public class NoCollateral : ICollateralScheme
    {
        private const string XllName = "NoCollateral";
        [WorksheetFunction(XllName + ".New")]
        public NoCollateral() { }
    }

    public class CashCollateral : ICollateralScheme
    {
        private const string XllName = "CashCollateral";
        
        public Currency Currency { get; private set; }

        public CashCollateral(Currency currency)
        {
            Currency = currency;
        }

        [WorksheetFunction(XllName + ".New")]
        public CashCollateral(string currency)
        {
            Currency = new Currency(currency);
        }
    }

    public class SecurityCollateral : ICollateralScheme
    {
        private const string XllName = "SecurityCollateral";

        public SingleNameTicker Security { get; private set;}

        [WorksheetFunction(XllName + ".New")]
        public SecurityCollateral(SingleNameTicker security)
        {
            Security = security ?? throw new ArgumentNullException(nameof(security));
        }
    }


}
