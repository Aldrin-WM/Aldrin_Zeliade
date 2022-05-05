using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{



    public class Deltas
    {
        private const string XllName = "Deltas";

        //[WorksheetFunction(XllName + ".SetFullPillars")]
        //public static IBumpSetter SetFullPillars(IBumpSetter mkt
        //    , string[] ticker
        //    , string[] type
        //    , double[] left
        //    , double[] right
        //    , string[] bumpPolicy
        //    , IFinDiffMethod[] finDiffMethod
        //    )
        //{
        //    InternalSetGeneric(typeof(FullPillarBump<>), mkt, ticker, type, left, right, bumpPolicy, finDiffMethod);
        //    return mkt;
        //}

        //[WorksheetFunction(XllName + ".SetFlat")]
        //public static IBumpSetter SetFlat(IBumpSetter mkt
        //    , string[] ticker
        //    , string[] type
        //    , double[] left
        //    , double[] right
        //    , string[] bumpPolicy
        //    , IFinDiffMethod[] finDiffMethod
        //    ) 
        //{
        //    InternalSetGeneric(typeof(FlatBump<>), mkt, ticker, type, left, right, bumpPolicy, finDiffMethod);
        //    return mkt;           
        //}

        [WorksheetFunction(XllName + ".SetGeneric")]
        public static void SetGeneric(IBumpSetter mkt
            , string[] ticker
            , string[] setType
            , string[] type
            , double[] left
            , double[] right
            , string[] bumpPolicy
            , IFinDiffMethod[] finDiffMethod
            , BasketSet baskets
            )
        {
            Require.ArgumentNotNull(mkt, "mkt");
            Require.ArgumentNotNull(setType, "setType");
            Require.ArgumentNotNull(ticker, "ticker");
            Require.ArgumentNotNull(type, "type");
            Require.ArgumentNotNull(left, "left");
            Require.ArgumentNotNull(right, "right");
            Require.ArgumentNotNull(bumpPolicy, "bumpPolicy");
            Require.ArgumentNotNull(finDiffMethod, "finDiffMethod");
            Require.ArgumentEqualArrayLength(ticker, setType, "ticker", "setType");
            Require.ArgumentEqualArrayLength(ticker, type, "ticker", "type");
            Require.ArgumentEqualArrayLength(ticker, left, "ticker", "left");
            Require.ArgumentEqualArrayLength(ticker, right, "ticker", "right");
            Require.ArgumentEqualArrayLength(ticker, bumpPolicy, "ticker", "bumpPolicy");
            Require.ArgumentEqualArrayLength(ticker, finDiffMethod, "ticker", "finDiffMethod");

            var registered = mkt.RegisteredTicker.ToDictionary(x => x.Name);

            for (int i = 0; i < ticker.Length; i++)
            {
                var typ = GetInstrumentType(type[i]);
                if (typ == null)
                {
                    throw new ArgumentException(string.Format("The instrument type {0} is not supported !", type[i]));
                }

                QuoteBumpType bp;
                if (!Enum.TryParse(bumpPolicy[i], out bp))
                {
                    throw new ArgumentException(string.Format("The coding {0} is not valid for enumeration {1}", bumpPolicy[i], typeof(QuoteBumpType).Name));
                }

                IBumpSheetTypeSet sheetBumps = GetBumpSetType(setType[i], right[i]
                    , left[i], bp, finDiffMethod[i], typ);
               
                if (ticker[i] == "All")
                {
                    foreach (var symb in registered)
                    {
                        mkt.SetBump(symb.Value, sheetBumps);
                    }
                }
                else if (registered.ContainsKey(ticker[i]))
                {
                    Symbol symb;
                    if (!registered.TryGetValue(ticker[i], out symb))
                    {
                        throw new ArgumentException(string.Format("The symbol {0} is not registered in the market {1} !", ticker[i], mkt.GetType().Name));
                    }
                    mkt.SetBump(symb, sheetBumps);
                }
                else if (baskets.Contains(ticker[i]))
                {
                    Require.ArgumentIsInstanceOf<BasketBump>(sheetBumps, "sheetBumps");
                    // SI le symbole est enregistré dans le BasketSet, verification si sheetBumps est BasketBump + registration
                    var basket = baskets.GetBasket(ticker[i]);
                    mkt.SetBump(basket, sheetBumps);
                }
                else
                {
                    throw new ArgumentException(string.Format("The symbol {0} is both not registered in the market {1} and not registered as a basket !", ticker[i], mkt.GetType().Name));
                }
            }

        }

        private static Type GetInstrumentType(string name)
        {
            // ATTENTION, MECANISME A REMPLACER !!!!

            var nss = new[]
            {
                "AldrinAnalytics.Calibration.{0}, AldrinAnalytics, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
                ,"Zeliade.Finance.Common.Calibration.RateCurves.Instruments.{0}, Zeliade.Finance.Common, Version=1.8.0.0, Culture=neutral, PublicKeyToken=null"
            };

            foreach (var item in nss)
            {
                var typ = Type.GetType(string.Format(item, name));
                if (typ != null)
                    return typ;
            }
            return null;
        }

        private static IBumpSheetTypeSet GetBumpSetType(string name, double right
            , double left, QuoteBumpType bp, IFinDiffMethod finDiff, Type instrumentTyp)
        {
            Type setTyp;
            switch (name)
            {
                case "FlatBump": setTyp = typeof(FlatBump<>); break;
                case "FullPillarAndUnderlyingBump": setTyp = typeof(FullPillarAndUnderlyingBump<>); break;
                case "FullPillarBump": setTyp = typeof(FullPillarBump<>); break;
                case "FullTenorBump": setTyp = typeof(FullTenorBump<>); break;
                case "SinglePillarBump": setTyp = typeof(SinglePillarBump<>); break;
                case "BasketBump": setTyp = typeof(BasketBump); break;
                default: throw new ArgumentException(string.Format("The bump set type {0} is not supported !", name));
            }
            
            if (setTyp==typeof(BasketBump))
            {
                return new BasketBump(right, left, bp, finDiff);
            }
            else
                return setTyp
                        .MakeGenericType(instrumentTyp)
                        .GetConstructor(new Type[] { typeof(double), typeof(double), typeof(QuoteBumpType), typeof(IFinDiffMethod) })
                        .Invoke(new object[] { right, left, bp, finDiff }) as IBumpSheetTypeSet;

        }

    }
}
