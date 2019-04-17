namespace FlashLFQ
{
    public class Ms1ScanInfo
    {
        public readonly int OneBasedScanNumber;
        public readonly int ZeroBasedMs1ScanIndex;
        public readonly double RetentionTime;
        public readonly double Tic;
        public readonly double? InjectionTime;

        public Ms1ScanInfo(int oneBasedScanNumber, int zeroBasedMs1ScanIndex, double retentionTime, double tic, double? injectionTime)
        {
            OneBasedScanNumber = oneBasedScanNumber;
            ZeroBasedMs1ScanIndex = zeroBasedMs1ScanIndex;
            RetentionTime = retentionTime;
            Tic = tic;
            InjectionTime = injectionTime;
        }

        public override string ToString()
        {
            return ZeroBasedMs1ScanIndex + "; " + OneBasedScanNumber + "; " + RetentionTime;
        }
    }
}
