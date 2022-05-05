using System;
using System.Runtime.Serialization;
using Zeliade.Finance.Common.Product;

namespace AldrinAnalytics.Instruments
{
    [DataContract]
    public class Ticker : Symbol, IUnderlyer
    {
        [DataMember]
        public Currency ReferenceCurrency { get; private set; }

        public string IdentifierName
        {
            get { return Name; }
        }

        public Currency Currency { get { return ReferenceCurrency; } }

        public Ticker(string name, string refCurrency)
            : base(name)
        {
            ReferenceCurrency = new Currency(refCurrency);
        }

        public override int GetHashCode()
        {
            //var baseHash = base.GetHashCode();
            //var ccyHash = ReferenceCurrency.GetHashCode();
            //int hash = 17;
            //const int p2 = 23;
            //hash = hash * p2 + baseHash;
            //hash = hash * p2 + ccyHash;
            //return hash;

            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
                return obj.GetHashCode() == GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", base.ToString(), ReferenceCurrency);
        }


    }

    public interface IHaveUnderlying
    {
        Ticker Underlying { get; }
    }

    public interface IDependOnSymbol
    {
        bool DependOn(Symbol s);
    }
}
