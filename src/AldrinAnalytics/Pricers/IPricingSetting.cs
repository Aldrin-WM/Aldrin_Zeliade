using System;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
    public interface IPricingSetting
    {
    }

    public class MonteCarloPricingSetting : IPricingSetting
    {
        private const string XllNAme = "MonteCarloPricingSetting";

        public int BlockSize { get; private set; }
        public int PathNumber { get; private set; }
        public double ConfidenceLevel { get; private set; }
        public double HalfInterval { get; private set; }
        public bool TargetInterval { get; private set; }

        [WorksheetFunction(XllNAme+ ".New")]
        public MonteCarloPricingSetting(int blockSize, int pathNumber, double confidenceLevel, double halfInterval, bool targetInterval)
        {
            BlockSize = blockSize;
            PathNumber = pathNumber;
            ConfidenceLevel = confidenceLevel;
            HalfInterval = halfInterval;
            TargetInterval = targetInterval;
        }
    }
}
