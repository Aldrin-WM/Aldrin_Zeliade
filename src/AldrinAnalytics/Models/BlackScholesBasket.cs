using System;
using System.Linq;
using AldrinAnalytics.Instruments;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Models
{
    public class BlackScholesBasket : ISingleTickerModel
    {
        private const string XllName = "BlackScholesBasket";
        public SecurityBasket Basket { get; private set; }
        public double[,] Covariance { get; private set; }
        public int FactorNumber { get; private set; }

        public Ticker Underlying { get { return Basket; } }


        [WorksheetFunction(XllName+".New")]
        public BlackScholesBasket(string[] snTickers, double[] vols, double[,] globalCorrel, SecurityBasket underlying, int factorNumber)
        {
            Require.ArgumentNotNull(snTickers, nameof(snTickers));
            Require.ArgumentNotNull(vols, nameof(vols));
            Require.ArgumentNotNull(globalCorrel, nameof(globalCorrel));
            Require.ArgumentNotNull(underlying, nameof(underlying));

            Require.Argument(snTickers.Length == vols.Length, nameof(vols), Error.Msg("The size of array {0} ({1}) should equlas the size of array {2} ({3})", nameof(vols), vols.Length,  nameof(snTickers), snTickers.Length));
            Require.Argument(snTickers.Length == globalCorrel.GetLength(0), nameof(globalCorrel), Error.Msg("The row number of array {0} ({1}) should equlas the size of array {2} ({3})", nameof(globalCorrel), globalCorrel.GetLength(0), nameof(snTickers), snTickers.Length));
            Require.Argument(snTickers.Length == globalCorrel.GetLength(1), nameof(globalCorrel), Error.Msg("The cols number of array {0} ({1}) should equlas the size of array {2} ({3})", nameof(globalCorrel), globalCorrel.GetLength(1), nameof(snTickers), snTickers.Length));
            
            FactorNumber = factorNumber;

            var dico = Enumerable.Range(0, snTickers.Length).ToDictionary(i => snTickers[i], i => i);
            
            var size = underlying.Content.Count;
            Covariance = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                var sni = underlying.Content[i];
                if (!dico.ContainsKey(sni.Name))
                    throw new ArgumentException(string.Format("The single name {0} is required by the basket but not given in the input covariance !", sni.Name));
                    
                for (int j = 0; j <= i; j++)
                {
                    var snj = underlying.Content[j];
                    if (!dico.ContainsKey(snj.Name))
                        throw new ArgumentException(string.Format("The single name {0} is required by the basket but not given in the input covariance !", sni.Name));
                    Covariance[i, j] = vols[dico[sni.Name]] *vols[dico[snj.Name]] *globalCorrel[dico[sni.Name], dico[snj.Name]];
                    Covariance[j, i] = Covariance[i, j];
                }
            }

            Basket = underlying;
        }

    }
}
